using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Application.Services.Availability;
using FiveAndOne.Application.Services.Bookings;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Facilities;

/// <summary>Sports, reservation rules, hours, closures, pricing and manual blocks of a facility (stories 09, 13-16, 19).</summary>
public interface IFacilityConfigService
{
    Task<FacilityConfigurationResponse> GetConfigurationAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default);
    Task<FacilityResponse> SaveSportsAsync(Guid userId, Guid? businessId, Guid facilityId, SaveFacilitySportsRequest request, CancellationToken cancellationToken = default);
    Task<FacilityResponse> SaveRulesAsync(Guid userId, Guid? businessId, Guid facilityId, FacilityRulesRequest request, CancellationToken cancellationToken = default);

    Task<WeeklyHoursResponse> GetHoursAsync(Guid userId, Guid? businessId, Guid facilityId, Guid? sportId, CancellationToken cancellationToken = default);
    Task<WeeklyHoursResponse> SaveHoursAsync(Guid userId, Guid? businessId, Guid facilityId, WeeklyHoursRequest request, CancellationToken cancellationToken = default);
    Task DeleteSportHoursAsync(Guid userId, Guid? businessId, Guid facilityId, Guid sportId, CancellationToken cancellationToken = default);

    Task<List<ScheduleExceptionResponse>> GetExceptionsAsync(Guid userId, Guid? businessId, Guid facilityId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<ScheduleExceptionResponse> AddExceptionAsync(Guid userId, Guid? businessId, Guid facilityId, ScheduleExceptionRequest request, CancellationToken cancellationToken = default);
    Task DeleteExceptionAsync(Guid userId, Guid? businessId, Guid facilityId, Guid exceptionId, CancellationToken cancellationToken = default);

    Task<List<PriceRuleDto>> GetPricingAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default);
    Task<List<PriceRuleDto>> SavePricingAsync(Guid userId, Guid? businessId, Guid facilityId, SavePricingRequest request, CancellationToken cancellationToken = default);
    Task<PricePreviewResponse> PreviewPriceAsync(Guid userId, Guid? businessId, Guid facilityId, Guid sportId, DateOnly date, TimeOnly startTime, int durationMinutes, CancellationToken cancellationToken = default);

    Task<List<BlockResponse>> GetBlocksAsync(Guid userId, Guid? businessId, Guid? facilityId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<BlockResponse> AddBlockAsync(Guid userId, Guid? businessId, Guid facilityId, CreateBlockRequest request, CancellationToken cancellationToken = default);
    Task DeleteBlockAsync(Guid userId, Guid? businessId, Guid facilityId, Guid blockId, CancellationToken cancellationToken = default);
}

public class FacilityConfigService : IFacilityConfigService
{
    private static readonly int[] AllowedSteps = { 5, 10, 15, 20, 30, 60 };
    private const int MaxWindowsPerDay = 4;

    private readonly ApplicationDbContext _db;
    private readonly IBusinessAccess _access;
    private readonly IFacilityService _facilities;
    private readonly IBookingLifecycle _lifecycle;
    private readonly IAuditService _audit;

    public FacilityConfigService(ApplicationDbContext db, IBusinessAccess access, IFacilityService facilities, IBookingLifecycle lifecycle, IAuditService audit)
    {
        _db = db;
        _access = access;
        _facilities = facilities;
        _lifecycle = lifecycle;
        _audit = audit;
    }

    // =====================================================================
    // Overview
    // =====================================================================

    public async Task<FacilityConfigurationResponse> GetConfigurationAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var (context, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.None, cancellationToken);
        var exceptions = await _db.FacilityScheduleExceptions.AsNoTracking()
            .Where(x => x.FacilityId == facility.Id && x.EndDate >= LocalTime.Today(facility.TimeZone))
            .OrderBy(x => x.StartDate).ToListAsync(cancellationToken);

        return new FacilityConfigurationResponse
        {
            FacilityId = facility.Id,
            TimeZone = facility.TimeZone,
            Rules = new FacilityRulesRequest
            {
                DurationOptions = facility.DurationOptions.OrderBy(x => x).ToArray(),
                BufferMinutes = facility.BufferMinutes,
                SlotStepMinutes = facility.SlotStepMinutes,
                MinAdvanceMinutes = facility.MinAdvanceMinutes,
                MaxAdvanceDays = facility.MaxAdvanceDays,
                CancellationCutoffHours = facility.CancellationCutoffHours,
                CancellationPolicyNote = facility.CancellationPolicyNote,
                RequiresOnlinePayment = facility.RequiresOnlinePayment
            },
            EffectiveCancellationCutoffHours = facility.CancellationCutoffHours ?? context.Business.CancellationCutoffHours,
            Sports = FacilityResponse.MapSports(facility),
            FacilityHours = ToWeekly(facility.OperatingHours.Where(h => h.SportId == null), null),
            SportHours = facility.OperatingHours.Where(h => h.SportId != null)
                .GroupBy(h => h.SportId).Select(g => ToWeekly(g, g.Key)).ToList(),
            PriceRules = facility.PriceRules.OrderByDescending(r => r.Priority).Select(ToDto).ToList(),
            UpcomingExceptions = exceptions.Select(ToResponse).ToList()
        };
    }

    // =====================================================================
    // Sports (story 09) and reservation rules (story 16)
    // =====================================================================

    public async Task<FacilityResponse> SaveSportsAsync(Guid userId, Guid? businessId, Guid facilityId, SaveFacilitySportsRequest request, CancellationToken cancellationToken = default)
    {
        var (context, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.ManageFacilities, cancellationToken);

        var errors = new Dictionary<string, string[]>();
        var ids = request.Sports.Select(s => s.SportId).ToList();
        if (ids.Distinct().Count() != ids.Count)
        {
            errors["sports"] = new[] { "Each sport can only be listed once." };
        }

        var sports = await _db.Sports.Where(s => ids.Contains(s.Id)).ToListAsync(cancellationToken);
        var current = facility.Sports.Select(s => s.SportId).ToHashSet();
        if (sports.Count != ids.Distinct().Count() || sports.Any(s => !s.IsActive && !current.Contains(s.Id)))
        {
            errors["sports"] = new[] { "One or more selected sports do not exist or are no longer available." };
        }

        for (var i = 0; i < request.Sports.Count; i++)
        {
            var durationError = ValidateDurations(request.Sports[i].DurationOptions, allowNull: true);
            if (durationError is not null) errors[$"sports[{i}].durationOptions"] = new[] { durationError };
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException("One or more validation errors occurred.", errors);
        }

        foreach (var existing in facility.Sports.Where(s => !ids.Contains(s.SportId)).ToList())
        {
            facility.Sports.Remove(existing);
            foreach (var h in facility.OperatingHours.Where(h => h.SportId == existing.SportId).ToList()) facility.OperatingHours.Remove(h);
            foreach (var r in facility.PriceRules.Where(r => r.SportId == existing.SportId).ToList()) facility.PriceRules.Remove(r);
        }

        foreach (var settings in request.Sports)
        {
            var row = facility.Sports.FirstOrDefault(s => s.SportId == settings.SportId);
            if (row is null)
            {
                row = new FacilitySport { FacilityId = facility.Id, SportId = settings.SportId, Sport = sports.First(s => s.Id == settings.SportId) };
                facility.Sports.Add(row);
            }

            row.DurationOptions = settings.DurationOptions is { Length: > 0 } d ? d.Distinct().OrderBy(x => x).ToArray() : null;
            row.BufferMinutes = settings.BufferMinutes;
        }

        FacilityService.EnsureStillComplete(facility);
        facility.UpdatedAt = DateTime.UtcNow;
        _audit.Log(userId, "facility.sports_updated", "Facility", facility.Id.ToString(), null, businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(await _facilities.LoadDetailsAsync(facility.Id, cancellationToken));
    }

    public async Task<FacilityResponse> SaveRulesAsync(Guid userId, Guid? businessId, Guid facilityId, FacilityRulesRequest request, CancellationToken cancellationToken = default)
    {
        var (context, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.ManageAvailability, cancellationToken);

        var errors = new Dictionary<string, string[]>();
        var durationError = ValidateDurations(request.DurationOptions, allowNull: false);
        if (durationError is not null) errors["durationOptions"] = new[] { durationError };
        if (!AllowedSteps.Contains(request.SlotStepMinutes))
            errors["slotStepMinutes"] = new[] { $"Slot start interval must be one of: {string.Join(", ", AllowedSteps)} minutes." };
        if (errors.Count > 0)
        {
            throw new RequestValidationException("One or more validation errors occurred.", errors);
        }

        facility.DurationOptions = request.DurationOptions.Distinct().OrderBy(x => x).ToArray();
        facility.BufferMinutes = request.BufferMinutes;
        facility.SlotStepMinutes = request.SlotStepMinutes;
        facility.MinAdvanceMinutes = request.MinAdvanceMinutes;
        facility.MaxAdvanceDays = request.MaxAdvanceDays;
        facility.CancellationCutoffHours = request.CancellationCutoffHours;
        facility.CancellationPolicyNote = Text.NullIfEmpty(request.CancellationPolicyNote);
        facility.RequiresOnlinePayment = request.RequiresOnlinePayment;
        facility.UpdatedAt = DateTime.UtcNow;

        _audit.Log(userId, "facility.rules_updated", "Facility", facility.Id.ToString(),
            $"Durations {string.Join("/", facility.DurationOptions)}, buffer {facility.BufferMinutes}", businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(await _facilities.LoadDetailsAsync(facility.Id, cancellationToken));
    }

    // =====================================================================
    // Weekly hours (story 14)
    // =====================================================================

    public async Task<WeeklyHoursResponse> GetHoursAsync(Guid userId, Guid? businessId, Guid facilityId, Guid? sportId, CancellationToken cancellationToken = default)
    {
        var (_, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.None, cancellationToken);
        return ToWeekly(facility.OperatingHours.Where(h => h.SportId == sportId), sportId);
    }

    public async Task<WeeklyHoursResponse> SaveHoursAsync(Guid userId, Guid? businessId, Guid facilityId, WeeklyHoursRequest request, CancellationToken cancellationToken = default)
    {
        var (context, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.ManageAvailability, cancellationToken);

        if (request.SportId.HasValue && facility.Sports.All(s => s.SportId != request.SportId))
        {
            throw RequestValidationException.For("sportId", "This sport is not offered at this facility.");
        }

        var days = ValidateWeek(request.Days);

        foreach (var row in facility.OperatingHours.Where(h => h.SportId == request.SportId).ToList())
        {
            facility.OperatingHours.Remove(row);
        }

        foreach (var day in days)
        {
            foreach (var window in day.Windows)
            {
                facility.OperatingHours.Add(new FacilityOperatingHours
                {
                    FacilityId = facility.Id,
                    SportId = request.SportId,
                    DayOfWeek = day.DayOfWeek,
                    OpensAt = window.OpensAt!.Value,
                    ClosesAt = window.ClosesAt!.Value
                });
            }
        }

        FacilityService.EnsureStillComplete(facility);
        facility.UpdatedAt = DateTime.UtcNow;
        _audit.Log(userId, "facility.hours_updated", "Facility", facility.Id.ToString(), request.SportId is null ? "Facility hours" : $"Sport {request.SportId}", businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return ToWeekly(facility.OperatingHours.Where(h => h.SportId == request.SportId), request.SportId);
    }

    public async Task DeleteSportHoursAsync(Guid userId, Guid? businessId, Guid facilityId, Guid sportId, CancellationToken cancellationToken = default)
    {
        var (context, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.ManageAvailability, cancellationToken);
        foreach (var row in facility.OperatingHours.Where(h => h.SportId == sportId).ToList())
        {
            facility.OperatingHours.Remove(row);
        }

        _audit.Log(userId, "facility.sport_hours_removed", "Facility", facility.Id.ToString(), $"Sport {sportId}", businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // =====================================================================
    // Exceptions and temporary closures (story 15)
    // =====================================================================

    public async Task<List<ScheduleExceptionResponse>> GetExceptionsAsync(Guid userId, Guid? businessId, Guid facilityId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);
        var facility = await _access.GetFacilityAsync(context, facilityId, cancellationToken);

        var start = from ?? LocalTime.Today(facility.TimeZone);
        var query = _db.FacilityScheduleExceptions.AsNoTracking().Where(x => x.FacilityId == facility.Id && x.EndDate >= start);
        if (to.HasValue) query = query.Where(x => x.StartDate <= to.Value);

        var rows = await query.OrderBy(x => x.StartDate).ToListAsync(cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<ScheduleExceptionResponse> AddExceptionAsync(Guid userId, Guid? businessId, Guid facilityId, ScheduleExceptionRequest request, CancellationToken cancellationToken = default)
    {
        var (context, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.ManageAvailability, cancellationToken);

        var startDate = request.StartDate!.Value;
        var endDate = request.EndDate ?? startDate;
        var errors = new Dictionary<string, string[]>();
        if (endDate < startDate) errors["endDate"] = new[] { "End date can't be before the start date." };
        if (endDate.DayNumber - startDate.DayNumber > 366) errors["endDate"] = new[] { "An exception can cover at most one year." };
        if (startDate < LocalTime.Today(facility.TimeZone)) errors["startDate"] = new[] { "Exceptions can't start in the past." };
        if (!request.IsClosed)
        {
            if (request.OpensAt is null) errors["opensAt"] = new[] { "Opening time is required for special hours." };
            if (request.ClosesAt is null) errors["closesAt"] = new[] { "Closing time is required for special hours." };
            if (request.OpensAt.HasValue && request.OpensAt == request.ClosesAt) errors["closesAt"] = new[] { "Closing time must differ from opening time." };
        }
        if (errors.Count > 0)
        {
            throw new RequestValidationException("One or more validation errors occurred.", errors);
        }

        var exception = new FacilityScheduleException
        {
            FacilityId = facility.Id,
            StartDate = startDate,
            EndDate = endDate,
            IsClosed = request.IsClosed,
            OpensAt = request.IsClosed ? null : request.OpensAt,
            ClosesAt = request.IsClosed ? null : request.ClosesAt,
            Reason = Text.NullIfEmpty(request.Reason),
            CreatedByUserId = userId
        };

        // Reservations that would end up outside the new hours must be resolved explicitly.
        var (rangeStart, _) = LocalTime.DayRangeUtc(startDate, facility.TimeZone);
        var (_, rangeEnd) = LocalTime.DayRangeUtc(endDate.AddDays(1), facility.TimeZone);
        var candidates = await _lifecycle.FindActiveAsync(facility.Id, rangeStart, rangeEnd, cancellationToken);

        var conflicts = new List<Domain.Entities.Bookings.Booking>();
        foreach (var booking in candidates)
        {
            var localDate = DateOnly.FromDateTime(LocalTime.ToLocal(booking.StartsAt, facility.TimeZone));
            if (localDate < startDate || localDate > endDate)
            {
                continue;
            }

            if (exception.IsClosed)
            {
                conflicts.Add(booking);
                continue;
            }

            var windows = ScheduleEngine.ToUtc(localDate, new[] { new LocalWindow(exception.OpensAt!.Value, exception.ClosesAt!.Value) }, facility.TimeZone);
            if (!ScheduleEngine.FitsInWindows(windows, booking.StartsAt, booking.EndsAt))
            {
                conflicts.Add(booking);
            }
        }

        await _lifecycle.ResolveConflictsAsync(conflicts, request.ConflictResolution, facility.TimeZone, userId,
            exception.Reason ?? (exception.IsClosed ? "Facility closed" : "Opening hours changed"), cancellationToken);

        _db.FacilityScheduleExceptions.Add(exception);
        _audit.Log(userId, exception.IsClosed ? "facility.closure_added" : "facility.special_hours_added", "Facility", facility.Id.ToString(),
            $"{startDate:yyyy-MM-dd}..{endDate:yyyy-MM-dd}; {conflicts.Count} conflict(s), resolution {request.ConflictResolution?.ToString() ?? "none"}",
            businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(exception);
    }

    public async Task DeleteExceptionAsync(Guid userId, Guid? businessId, Guid facilityId, Guid exceptionId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageAvailability, cancellationToken);
        var facility = await _access.GetFacilityAsync(context, facilityId, cancellationToken);

        var exception = await _db.FacilityScheduleExceptions.FirstOrDefaultAsync(x => x.Id == exceptionId && x.FacilityId == facility.Id, cancellationToken)
            ?? throw new NotFoundException("Exception not found.");

        _db.FacilityScheduleExceptions.Remove(exception);
        _audit.Log(userId, "facility.exception_removed", "Facility", facility.Id.ToString(), $"{exception.StartDate:yyyy-MM-dd}..{exception.EndDate:yyyy-MM-dd}", businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // =====================================================================
    // Pricing (story 13)
    // =====================================================================

    public async Task<List<PriceRuleDto>> GetPricingAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var (_, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.None, cancellationToken);
        return facility.PriceRules.OrderBy(r => r.SportId).ThenByDescending(r => r.Priority).Select(ToDto).ToList();
    }

    public async Task<List<PriceRuleDto>> SavePricingAsync(Guid userId, Guid? businessId, Guid facilityId, SavePricingRequest request, CancellationToken cancellationToken = default)
    {
        var (context, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.ManagePricing, cancellationToken);

        var errors = new Dictionary<string, string[]>();
        var sportIds = facility.Sports.Select(s => s.SportId).ToHashSet();
        for (var i = 0; i < request.Rules.Count; i++)
        {
            var rule = request.Rules[i];
            var key = $"rules[{i}]";
            if (rule.SportId.HasValue && !sportIds.Contains(rule.SportId.Value))
                errors[$"{key}.sportId"] = new[] { "This sport is not offered at this facility." };
            if (rule.DurationMinutes is <= 0 or > 1440)
                errors[$"{key}.durationMinutes"] = new[] { "Duration must be between 1 and 1440 minutes." };
            if ((rule.StartTime is null) != (rule.EndTime is null))
                errors[$"{key}.startTime"] = new[] { "Give both a start and an end time, or neither (all day)." };
            if (rule.StartTime.HasValue && rule.StartTime == rule.EndTime)
                errors[$"{key}.endTime"] = new[] { "End time must differ from the start time." };
            if (!Enum.IsDefined(rule.PriceType))
                errors[$"{key}.priceType"] = new[] { "Price type must be PerReservation or PerHour." };
            var currency = (rule.Currency ?? "EUR").Trim().ToUpperInvariant();
            if (currency != "EUR")
                errors[$"{key}.currency"] = new[] { "Only EUR is supported for now." };
        }

        if (request.Rules.Count > 100)
            errors["rules"] = new[] { "A facility can have at most 100 price rules." };
        if (errors.Count > 0)
        {
            throw new RequestValidationException("One or more validation errors occurred.", errors);
        }

        // Replace the whole set ("Save pricing"). Existing reservations keep their price snapshot.
        foreach (var existing in facility.PriceRules.ToList())
        {
            facility.PriceRules.Remove(existing);
        }

        foreach (var rule in request.Rules)
        {
            facility.PriceRules.Add(new FacilityPriceRule
            {
                FacilityId = facility.Id,
                SportId = rule.SportId,
                DurationMinutes = rule.DurationMinutes,
                DayOfWeek = rule.DayOfWeek,
                StartTime = rule.StartTime,
                EndTime = rule.EndTime,
                PriceType = rule.PriceType,
                Amount = Math.Round(rule.Amount, 2),
                Currency = "EUR",
                Priority = rule.Priority,
                Label = Text.NullIfEmpty(rule.Label)
            });
        }

        FacilityService.EnsureStillComplete(facility);
        facility.UpdatedAt = DateTime.UtcNow;
        _audit.Log(userId, "facility.pricing_updated", "Facility", facility.Id.ToString(), $"{request.Rules.Count} rule(s)", businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return facility.PriceRules.Select(ToDto).ToList();
    }

    public async Task<PricePreviewResponse> PreviewPriceAsync(Guid userId, Guid? businessId, Guid facilityId, Guid sportId, DateOnly date, TimeOnly startTime, int durationMinutes, CancellationToken cancellationToken = default)
    {
        var (_, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.None, cancellationToken);
        var quote = PricingEngine.Quote(facility.PriceRules, sportId, durationMinutes, date.DayOfWeek, startTime);
        var rule = quote is null ? null : facility.PriceRules.First(r => r.Id == quote.RuleId);

        return new PricePreviewResponse
        {
            SportId = sportId,
            Date = date,
            StartTime = startTime,
            DurationMinutes = durationMinutes,
            Price = quote?.Amount,
            Currency = quote?.Currency,
            AppliedRuleId = quote?.RuleId,
            AppliedRuleLabel = rule?.Label
        };
    }

    // =====================================================================
    // Manual blocks (story 19)
    // =====================================================================

    public async Task<List<BlockResponse>> GetBlocksAsync(Guid userId, Guid? businessId, Guid? facilityId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);

        var fromUtc = (from ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);
        var toUtc = (to ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(2);

        var query = _db.AvailabilityBlocks.AsNoTracking()
            .Where(b => b.Facility.BusinessId == context.BusinessId && b.EndsAt > fromUtc && b.StartsAt < toUtc);
        if (facilityId.HasValue) query = query.Where(b => b.FacilityId == facilityId.Value);

        var blocks = await query.Include(b => b.Facility).OrderBy(b => b.StartsAt).ToListAsync(cancellationToken);
        return blocks.Select(ToResponse).ToList();
    }

    public async Task<BlockResponse> AddBlockAsync(Guid userId, Guid? businessId, Guid facilityId, CreateBlockRequest request, CancellationToken cancellationToken = default)
    {
        var (context, facility) = await LoadAsync(userId, businessId, facilityId, BusinessPermissions.ManageAvailability, cancellationToken);

        if (request.SportId.HasValue && facility.Sports.All(s => s.SportId != request.SportId))
        {
            throw RequestValidationException.For("sportId", "This sport is not offered at this facility.");
        }

        var date = request.Date!.Value;
        var startsAt = LocalTime.ToUtc(date, request.StartTime!.Value, facility.TimeZone)
            ?? throw RequestValidationException.For("startTime", "This time doesn't exist on that date (daylight-saving change).");
        var endDate = request.EndTime!.Value <= request.StartTime.Value ? date.AddDays(1) : date;
        var endsAt = LocalTime.ToUtc(endDate, request.EndTime.Value, facility.TimeZone)
            ?? throw RequestValidationException.For("endTime", "This time doesn't exist on that date (daylight-saving change).");

        if (endsAt <= DateTime.UtcNow)
        {
            throw RequestValidationException.For("endTime", "A block must end in the future.");
        }

        // Overlapping blocks: editing the existing one is clearer than stacking blocks.
        var overlappingBlock = await _db.AvailabilityBlocks.AsNoTracking()
            .Where(b => b.FacilityId == facility.Id && b.StartsAt < endsAt && b.EndsAt > startsAt
                        && (b.SportId == null || request.SportId == null || b.SportId == request.SportId))
            .FirstOrDefaultAsync(cancellationToken);
        if (overlappingBlock is not null)
        {
            throw new ConflictException("This time overlaps another block on this facility.", "block_overlap",
                new { blocks = new[] { ToResponse(overlappingBlock, facility) } });
        }

        var bookings = await _lifecycle.FindActiveAsync(facility.Id, startsAt, endsAt, cancellationToken);
        var conflicts = request.SportId.HasValue ? bookings.Where(b => b.SportId == request.SportId).ToList() : bookings;
        await _lifecycle.ResolveConflictsAsync(conflicts, request.ConflictResolution, facility.TimeZone, userId,
            $"Time blocked ({request.Reason})", cancellationToken);

        var block = new AvailabilityBlock
        {
            FacilityId = facility.Id,
            SportId = request.SportId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Reason = request.Reason,
            InternalNote = Text.NullIfEmpty(request.InternalNote),
            CreatedByUserId = userId
        };

        _db.AvailabilityBlocks.Add(block);
        _audit.Log(userId, "facility.time_blocked", "Facility", facility.Id.ToString(),
            $"{startsAt:u}..{endsAt:u} ({request.Reason}); {conflicts.Count} conflict(s)", businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(block, facility);
    }

    public async Task DeleteBlockAsync(Guid userId, Guid? businessId, Guid facilityId, Guid blockId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageAvailability, cancellationToken);
        var facility = await _access.GetFacilityAsync(context, facilityId, cancellationToken);

        var block = await _db.AvailabilityBlocks.FirstOrDefaultAsync(b => b.Id == blockId && b.FacilityId == facility.Id, cancellationToken)
            ?? throw new NotFoundException("Block not found.");

        _db.AvailabilityBlocks.Remove(block);
        _audit.Log(userId, "facility.block_removed", "Facility", facility.Id.ToString(), $"{block.StartsAt:u}..{block.EndsAt:u}", businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private async Task<(BusinessContext Context, Facility Facility)> LoadAsync(Guid userId, Guid? businessId, Guid facilityId,
        BusinessPermissions permission, CancellationToken cancellationToken)
    {
        var context = await _access.GetAsync(userId, businessId, permission, cancellationToken);
        var facility = await _db.Facilities
            .Include(f => f.Sports).ThenInclude(s => s.Sport)
            .Include(f => f.OperatingHours)
            .Include(f => f.PriceRules)
            .Include(f => f.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == facilityId && f.BusinessId == context.BusinessId, cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        return (context, facility);
    }

    private static string? ValidateDurations(int[]? durations, bool allowNull)
    {
        if (durations is null || durations.Length == 0)
        {
            return allowNull ? null : "Add at least one duration.";
        }

        if (durations.Length > 8) return "At most 8 durations.";
        if (durations.Any(d => d is < 15 or > 720 || d % 5 != 0))
            return "Each duration must be 15-720 minutes, in steps of 5.";
        return null;
    }

    private static List<DayHoursDto> ValidateWeek(List<DayHoursDto> days)
    {
        var errors = new Dictionary<string, string[]>();
        if (days.Select(d => d.DayOfWeek).Distinct().Count() != days.Count)
        {
            errors["days"] = new[] { "Each day can only appear once." };
        }

        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            if (day.IsClosed)
            {
                day.Windows.Clear();
                continue;
            }

            if (day.Windows.Count == 0)
            {
                errors[$"days[{i}].windows"] = new[] { "Add opening hours or mark the day as closed." };
                continue;
            }

            if (day.Windows.Count > MaxWindowsPerDay)
            {
                errors[$"days[{i}].windows"] = new[] { $"At most {MaxWindowsPerDay} opening periods per day." };
                continue;
            }

            if (day.Windows.Any(w => w.OpensAt == w.ClosesAt))
            {
                errors[$"days[{i}].windows"] = new[] { "Closing time must differ from opening time." };
                continue;
            }

            // Minutes from the day's start; overnight windows continue past 1440.
            var ranges = day.Windows
                .Select(w => (Start: Minutes(w.OpensAt!.Value), End: Minutes(w.ClosesAt!.Value) <= Minutes(w.OpensAt!.Value)
                    ? Minutes(w.ClosesAt!.Value) + 1440 : Minutes(w.ClosesAt!.Value)))
                .OrderBy(r => r.Start).ToList();

            if (ranges.Count(r => r.End > 1440) > 1 || ranges.Zip(ranges.Skip(1)).Any(p => p.Second.Start < p.First.End))
            {
                errors[$"days[{i}].windows"] = new[] { "Opening periods on the same day can't overlap." };
            }
        }

        // An overnight window must end before the next day's first opening.
        foreach (var day in days.Where(d => !d.IsClosed))
        {
            var overnight = day.Windows.Where(w => w.ClosesAt <= w.OpensAt).Select(w => Minutes(w.ClosesAt!.Value)).DefaultIfEmpty(-1).Max();
            if (overnight < 0) continue;

            var next = days.FirstOrDefault(d => d.DayOfWeek == (DayOfWeek)(((int)day.DayOfWeek + 1) % 7));
            if (next is not null && !next.IsClosed && next.Windows.Any(w => Minutes(w.OpensAt!.Value) < overnight))
            {
                errors[$"days.{day.DayOfWeek}"] = new[] { $"{day.DayOfWeek}'s late hours run into {next.DayOfWeek}'s opening hours." };
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException("Check the opening hours.", errors);
        }

        return days;
    }

    private static int Minutes(TimeOnly time) => time.Hour * 60 + time.Minute;

    internal static WeeklyHoursResponse ToWeekly(IEnumerable<FacilityOperatingHours> rows, Guid? sportId)
    {
        var list = rows.ToList();
        var order = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

        var days = order.Select(d =>
        {
            var windows = list.Where(h => h.DayOfWeek == d).OrderBy(h => h.OpensAt)
                .Select(h => new TimeWindowDto { OpensAt = h.OpensAt, ClosesAt = h.ClosesAt }).ToList();
            return new DayHoursDto { DayOfWeek = d, IsClosed = windows.Count == 0, Windows = windows };
        }).ToList();

        return new WeeklyHoursResponse { SportId = sportId, Days = days, Summary = Summarize(days) };
    }

    /// <summary>"Mon–Fri 08:00–23:00, Sat 09:00–22:00, Sun closed".</summary>
    private static string Summarize(List<DayHoursDto> days)
    {
        static string Label(DayHoursDto d) => d.IsClosed
            ? "closed"
            : string.Join(", ", d.Windows.Select(w => $"{w.OpensAt:HH\\:mm}–{w.ClosesAt:HH\\:mm}"));

        var parts = new List<string>();
        var i = 0;
        while (i < days.Count)
        {
            var j = i;
            while (j + 1 < days.Count && Label(days[j + 1]) == Label(days[i])) j++;
            var name = i == j ? Short(days[i].DayOfWeek) : $"{Short(days[i].DayOfWeek)}–{Short(days[j].DayOfWeek)}";
            parts.Add($"{name} {Label(days[i])}");
            i = j + 1;
        }

        return string.Join(", ", parts);
    }

    private static string Short(DayOfWeek day) => day.ToString()[..3];

    private static PriceRuleDto ToDto(FacilityPriceRule r) => new()
    {
        Id = r.Id,
        SportId = r.SportId,
        DurationMinutes = r.DurationMinutes,
        DayOfWeek = r.DayOfWeek,
        StartTime = r.StartTime,
        EndTime = r.EndTime,
        PriceType = r.PriceType,
        Amount = r.Amount,
        Currency = r.Currency,
        Priority = r.Priority,
        Label = r.Label
    };

    private static ScheduleExceptionResponse ToResponse(FacilityScheduleException x) => new()
    {
        Id = x.Id,
        StartDate = x.StartDate,
        EndDate = x.EndDate,
        IsClosed = x.IsClosed,
        OpensAt = x.OpensAt,
        ClosesAt = x.ClosesAt,
        Reason = x.Reason,
        CreatedAt = x.CreatedAt
    };

    private static BlockResponse ToResponse(AvailabilityBlock b) => ToResponse(b, b.Facility);

    private static BlockResponse ToResponse(AvailabilityBlock b, Facility facility) => new()
    {
        Id = b.Id,
        FacilityId = b.FacilityId,
        FacilityName = facility.Name,
        SportId = b.SportId,
        StartsAt = b.StartsAt,
        EndsAt = b.EndsAt,
        LocalStartsAt = LocalTime.ToLocal(b.StartsAt, facility.TimeZone),
        LocalEndsAt = LocalTime.ToLocal(b.EndsAt, facility.TimeZone),
        Reason = b.Reason,
        InternalNote = b.InternalNote,
        CreatedAt = b.CreatedAt
    };
}
