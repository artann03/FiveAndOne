using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Bookings;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Services.Availability;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Application.Services.Payments;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Bookings;

/// <summary>Reservations from the business side (stories 20, 35, 36, 40) and the admin's controlled actions (story 48).</summary>
public interface IBusinessReservationService
{
    Task<PagedResponse<BusinessReservationResponse>> ListAsync(Guid userId, Guid? businessId, BusinessReservationQuery query, CancellationToken cancellationToken = default);
    Task<BusinessReservationResponse> GetAsync(Guid userId, Guid? businessId, Guid bookingId, CancellationToken cancellationToken = default);
    Task<BusinessReservationResponse> CreateExternalAsync(Guid userId, Guid? businessId, ExternalReservationRequest request, CancellationToken cancellationToken = default);
    Task<BusinessReservationResponse> UpdateExternalAsync(Guid userId, Guid? businessId, Guid bookingId, ExternalReservationRequest request, CancellationToken cancellationToken = default);
    Task<BusinessReservationResponse> CancelAsync(Guid userId, Guid? businessId, Guid bookingId, string reason, CancellationToken cancellationToken = default);
    Task<BusinessReservationResponse> SetOutcomeAsync(Guid userId, Guid? businessId, Guid bookingId, BookingStatus outcome, CancellationToken cancellationToken = default);
    Task<BusinessReservationResponse> RescheduleAsync(Guid userId, Guid? businessId, Guid bookingId, RescheduleReservationRequest request, CancellationToken cancellationToken = default);
    Task<BusinessReservationResponse> UpdateNotesAsync(Guid userId, Guid? businessId, Guid bookingId, string? notes, CancellationToken cancellationToken = default);

    // Admin
    Task<PagedResponse<BusinessReservationResponse>> AdminListAsync(Guid? businessId, BusinessReservationQuery query, CancellationToken cancellationToken = default);
    Task<BusinessReservationResponse> AdminGetAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<BusinessReservationResponse> AdminCancelAsync(Guid actorId, Guid bookingId, string reason, CancellationToken cancellationToken = default);
}

public class BusinessReservationService : IBusinessReservationService
{
    // Dashboard date filters are Kosovo local days (one market time zone).
    private const string MarketTimeZone = "Europe/Belgrade";

    private readonly ApplicationDbContext _db;
    private readonly IBusinessAccess _access;
    private readonly IAvailabilityService _availability;
    private readonly ReservationSupport _support;
    private readonly IBookingLifecycle _lifecycle;
    private readonly IBookingNotifier _notifier;
    private readonly IReservationPaymentService _payments;
    private readonly IAuditService _audit;

    public BusinessReservationService(ApplicationDbContext db, IBusinessAccess access, IAvailabilityService availability, ReservationSupport support,
        IBookingLifecycle lifecycle, IBookingNotifier notifier, IReservationPaymentService payments, IAuditService audit)
    {
        _db = db;
        _access = access;
        _availability = availability;
        _support = support;
        _lifecycle = lifecycle;
        _notifier = notifier;
        _payments = payments;
        _audit = audit;
    }

    // =====================================================================
    // List / detail (story 40)
    // =====================================================================

    public async Task<PagedResponse<BusinessReservationResponse>> ListAsync(Guid userId, Guid? businessId, BusinessReservationQuery query, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageReservations, cancellationToken);
        return await SearchAsync(context.BusinessId, query, cancellationToken);
    }

    public async Task<BusinessReservationResponse> GetAsync(Guid userId, Guid? businessId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageReservations, cancellationToken);
        await GetOwnedAsync(context, bookingId, cancellationToken);
        return await MapAsync(bookingId, includeHistory: true, cancellationToken);
    }

    // =====================================================================
    // External reservations (story 20)
    // =====================================================================

    public async Task<BusinessReservationResponse> CreateExternalAsync(Guid userId, Guid? businessId, ExternalReservationRequest request, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageReservations, cancellationToken);
        var facility = await LoadFacilityAsync(context, request.FacilityId!.Value, cancellationToken);
        var (startsAt, endsAt, blockedUntil) = ResolveTimes(facility, request.SportId!.Value, request.Date!.Value, request.StartTime!.Value, request.EndTime!.Value);

        await EnsureFreeAsync(facility, request.SportId.Value, startsAt, endsAt, blockedUntil, null, cancellationToken);

        var localStart = LocalTime.ToLocal(startsAt, facility.TimeZone);
        var durationMinutes = (int)(endsAt - startsAt).TotalMinutes;
        var quote = PricingEngine.Quote(facility.PriceRules, request.SportId.Value, durationMinutes, localStart.DayOfWeek, TimeOnly.FromDateTime(localStart));

        var booking = new Booking
        {
            BookingNumber = await _support.NewBookingNumberAsync(cancellationToken),
            Source = BookingSource.External,
            ExternalChannel = request.Channel,
            BusinessId = context.BusinessId,
            FacilityId = facility.Id,
            SportId = request.SportId.Value,
            ContactName = Text.NullIfEmpty(request.CustomerName),
            ContactPhone = Text.NormalizePhone(request.CustomerPhone),
            ContactEmail = Text.NullIfEmpty(request.CustomerEmail)?.ToLowerInvariant(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            BlockedUntil = blockedUntil,
            Price = request.Price ?? quote?.Amount ?? 0,
            Currency = quote?.Currency ?? "EUR",
            Status = BookingStatus.Confirmed,
            PaymentStatus = PaymentStatus.NotRequired,
            InternalNotes = Text.NullIfEmpty(request.InternalNotes),
            CreatedByUserId = userId
        };

        _db.Bookings.Add(booking);
        _lifecycle.AddHistory(booking, BookingEventType.Created, null, BookingStatus.Confirmed, userId, $"External ({request.Channel})");
        _audit.Log(userId, "reservation.external_created", "Booking", booking.Id.ToString(), booking.BookingNumber, businessId: context.BusinessId);
        await _support.SaveWithSlotGuardAsync(cancellationToken);

        return await MapAsync(booking.Id, includeHistory: true, cancellationToken);
    }

    public async Task<BusinessReservationResponse> UpdateExternalAsync(Guid userId, Guid? businessId, Guid bookingId, ExternalReservationRequest request, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageReservations, cancellationToken);
        var booking = await GetOwnedAsync(context, bookingId, cancellationToken);

        if (booking.Source != BookingSource.External)
        {
            throw new ConflictException("Only external reservations can be edited this way. Use reschedule for SportX reservations.", "not_external");
        }
        if (booking.Status != BookingStatus.Confirmed)
        {
            throw new ConflictException("Only confirmed reservations can be edited.", "invalid_status");
        }

        var facility = await LoadFacilityAsync(context, request.FacilityId!.Value, cancellationToken);
        var (startsAt, endsAt, blockedUntil) = ResolveTimes(facility, request.SportId!.Value, request.Date!.Value, request.StartTime!.Value, request.EndTime!.Value);
        await EnsureFreeAsync(facility, request.SportId.Value, startsAt, endsAt, blockedUntil, booking.Id, cancellationToken);

        var oldStart = booking.StartsAt;
        var oldEnd = booking.EndsAt;
        booking.FacilityId = facility.Id;
        booking.SportId = request.SportId.Value;
        booking.StartsAt = startsAt;
        booking.EndsAt = endsAt;
        booking.BlockedUntil = blockedUntil;
        booking.ExternalChannel = request.Channel;
        booking.ContactName = Text.NullIfEmpty(request.CustomerName);
        booking.ContactPhone = Text.NormalizePhone(request.CustomerPhone);
        booking.ContactEmail = Text.NullIfEmpty(request.CustomerEmail)?.ToLowerInvariant();
        if (request.Price.HasValue) booking.Price = request.Price.Value;
        booking.InternalNotes = Text.NullIfEmpty(request.InternalNotes);
        booking.UpdatedAt = DateTime.UtcNow;

        _lifecycle.AddHistory(booking, oldStart != startsAt || oldEnd != endsAt ? BookingEventType.Rescheduled : BookingEventType.Updated,
            null, null, userId, null, oldStart, oldEnd);
        _audit.Log(userId, "reservation.external_updated", "Booking", booking.Id.ToString(), booking.BookingNumber, businessId: context.BusinessId);
        await _support.SaveWithSlotGuardAsync(cancellationToken);

        return await MapAsync(booking.Id, includeHistory: true, cancellationToken);
    }

    // =====================================================================
    // Status changes (story 35)
    // =====================================================================

    public async Task<BusinessReservationResponse> CancelAsync(Guid userId, Guid? businessId, Guid bookingId, string reason, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageReservations, cancellationToken);
        var booking = await GetOwnedAsync(context, bookingId, cancellationToken);

        if (booking.EndsAt <= DateTime.UtcNow)
        {
            throw new ConflictException("This reservation has already ended. Mark it completed or no-show instead.", "reservation_ended");
        }

        await _lifecycle.ChangeStatusAsync(booking, BookingStatus.CancelledByBusiness, userId, reason, cancellationToken);
        await _payments.RefundIfPaidAsync(booking, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await MapAsync(booking.Id, includeHistory: true, cancellationToken);
    }

    public async Task<BusinessReservationResponse> SetOutcomeAsync(Guid userId, Guid? businessId, Guid bookingId, BookingStatus outcome, CancellationToken cancellationToken = default)
    {
        if (outcome is not (BookingStatus.Completed or BookingStatus.NoShow))
        {
            throw new BadRequestException("Outcome must be Completed or NoShow.");
        }

        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageReservations, cancellationToken);
        var booking = await GetOwnedAsync(context, bookingId, cancellationToken);

        if (booking.StartsAt > DateTime.UtcNow)
        {
            throw new ConflictException("A reservation can only be marked completed or no-show once it has started.", "reservation_not_started");
        }

        await _lifecycle.ChangeStatusAsync(booking, outcome, userId, null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await MapAsync(booking.Id, includeHistory: true, cancellationToken);
    }

    public async Task<BusinessReservationResponse> RescheduleAsync(Guid userId, Guid? businessId, Guid bookingId, RescheduleReservationRequest request, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageReservations, cancellationToken);
        var booking = await GetOwnedAsync(context, bookingId, cancellationToken);
        if (booking.Status != BookingStatus.Confirmed)
        {
            throw new ConflictException("Only confirmed reservations can be changed.", "invalid_status");
        }

        var facility = await LoadFacilityAsync(context, booking.FacilityId, cancellationToken);
        var duration = request.DurationMinutes ?? booking.DurationMinutes;
        var startsAt = DateTime.SpecifyKind(request.StartsAt!.Value.ToUniversalTime(), DateTimeKind.Utc);
        var endsAt = startsAt.AddMinutes(duration);
        var rules = SportRules.For(facility, facility.Sports.FirstOrDefault(s => s.SportId == booking.SportId));
        var blockedUntil = endsAt.AddMinutes(rules.BufferMinutes);

        if (startsAt < DateTime.UtcNow)
        {
            throw RequestValidationException.For("startsAt", "The new time must be in the future.");
        }

        // The business can move a reservation to any free time, also outside the online slot grid.
        await EnsureFreeAsync(facility, booking.SportId, startsAt, endsAt, blockedUntil, booking.Id, cancellationToken);

        var oldStart = booking.StartsAt;
        var oldEnd = booking.EndsAt;
        booking.StartsAt = startsAt;
        booking.EndsAt = endsAt;
        booking.BlockedUntil = blockedUntil;
        booking.UpdatedAt = DateTime.UtcNow;

        _lifecycle.AddHistory(booking, BookingEventType.Rescheduled, null, null, userId, Text.NullIfEmpty(request.Reason), oldStart, oldEnd);
        await _notifier.RescheduledAsync(booking, oldStart, byBusiness: true, cancellationToken);
        _audit.Log(userId, "reservation.rescheduled_by_business", "Booking", booking.Id.ToString(), $"{oldStart:u} -> {startsAt:u}", businessId: context.BusinessId);
        await _support.SaveWithSlotGuardAsync(cancellationToken);

        return await MapAsync(booking.Id, includeHistory: true, cancellationToken);
    }

    public async Task<BusinessReservationResponse> UpdateNotesAsync(Guid userId, Guid? businessId, Guid bookingId, string? notes, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageReservations, cancellationToken);
        var booking = await GetOwnedAsync(context, bookingId, cancellationToken);
        booking.InternalNotes = Text.NullIfEmpty(notes);
        booking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await MapAsync(booking.Id, includeHistory: true, cancellationToken);
    }

    // =====================================================================
    // Admin (story 48): actions go through the same lifecycle rules and are audited
    // =====================================================================

    public Task<PagedResponse<BusinessReservationResponse>> AdminListAsync(Guid? businessId, BusinessReservationQuery query, CancellationToken cancellationToken = default) =>
        SearchAsync(businessId, query, cancellationToken);

    public Task<BusinessReservationResponse> AdminGetAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        MapAsync(bookingId, includeHistory: true, cancellationToken);

    public async Task<BusinessReservationResponse> AdminCancelAsync(Guid actorId, Guid bookingId, string reason, CancellationToken cancellationToken = default)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken) ?? throw new NotFoundException("Reservation not found.");

        await _lifecycle.ChangeStatusAsync(booking, BookingStatus.CancelledByBusiness, actorId, $"[Platform admin] {reason}", cancellationToken);
        await _payments.RefundIfPaidAsync(booking, cancellationToken);
        _audit.Log(actorId, "reservation.cancelled_by_admin", "Booking", booking.Id.ToString(), reason, businessId: booking.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return await MapAsync(booking.Id, includeHistory: true, cancellationToken);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private async Task<PagedResponse<BusinessReservationResponse>> SearchAsync(Guid? businessId, BusinessReservationQuery query, CancellationToken cancellationToken)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize, 200);
        var now = DateTime.UtcNow;
        var q = _db.Bookings.AsNoTracking();

        if (businessId.HasValue) q = q.Where(b => b.BusinessId == businessId.Value);
        if (query.FacilityId.HasValue) q = q.Where(b => b.FacilityId == query.FacilityId);
        if (query.SportId.HasValue) q = q.Where(b => b.SportId == query.SportId);
        if (query.Status.HasValue) q = q.Where(b => b.Status == query.Status);
        if (query.Source.HasValue) q = q.Where(b => b.Source == query.Source);
        // Dates are Kosovo local days; one market time zone for the whole dashboard.
        if (query.From.HasValue)
        {
            var fromUtc = LocalTime.DayRangeUtc(query.From.Value, MarketTimeZone).Start;
            q = q.Where(b => b.StartsAt >= fromUtc);
        }
        if (query.To.HasValue)
        {
            var toUtc = LocalTime.DayRangeUtc(query.To.Value, MarketTimeZone).End;
            q = q.Where(b => b.StartsAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var pattern = $"%{Text.EscapeLike(term)}%";
            var phone = Text.NormalizePhone(term);
            q = q.Where(b => EF.Functions.ILike(b.BookingNumber, pattern)
                             || (b.ContactName != null && EF.Functions.ILike(b.ContactName, pattern))
                             || (b.ContactEmail != null && EF.Functions.ILike(b.ContactEmail, pattern))
                             || (phone != null && phone.Length >= 4 && b.ContactPhone != null && b.ContactPhone.Contains(phone)));
        }

        var scope = (query.Scope ?? (query.From.HasValue || query.To.HasValue ? "all" : "upcoming")).ToLowerInvariant();
        q = scope switch
        {
            "past" => q.Where(b => b.EndsAt <= now),
            "upcoming" => q.Where(b => b.EndsAt > now),
            _ => q
        };

        var total = await q.CountAsync(cancellationToken);
        var ordered = scope == "past" ? q.OrderByDescending(b => b.StartsAt) : q.OrderBy(b => b.StartsAt);
        var ids = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(b => b.Id).ToListAsync(cancellationToken);

        var items = new List<BusinessReservationResponse>();
        foreach (var id in ids)
        {
            items.Add(await MapAsync(id, includeHistory: false, cancellationToken));
        }

        return PagedResponse<BusinessReservationResponse>.Create(items, page, pageSize, total);
    }

    private async Task<Facility> LoadFacilityAsync(BusinessContext context, Guid facilityId, CancellationToken cancellationToken)
    {
        var facility = await _db.Facilities.AsNoTracking().Where(f => f.Id == facilityId && f.BusinessId == context.BusinessId)
            .WithSchedule().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        if (context.Business.Status == BusinessStatus.Inactive)
        {
            // Story 44: after the grace period no new reservations are taken (history stays visible).
            throw new ForbiddenException("Your subscription has expired. Renew it to take new reservations.", "subscription_expired");
        }

        return facility;
    }

    private static (DateTime Start, DateTime End, DateTime BlockedUntil) ResolveTimes(Facility facility, Guid sportId, DateOnly date, TimeOnly start, TimeOnly end)
    {
        var facilitySport = facility.Sports.FirstOrDefault(s => s.SportId == sportId)
            ?? throw RequestValidationException.For("sportId", "This sport is not offered at this facility.");

        var startsAt = LocalTime.ToUtc(date, start, facility.TimeZone)
            ?? throw RequestValidationException.For("startTime", "This time doesn't exist on that date (daylight-saving change).");
        var endsAt = LocalTime.ToUtc(end <= start ? date.AddDays(1) : date, end, facility.TimeZone)
            ?? throw RequestValidationException.For("endTime", "This time doesn't exist on that date (daylight-saving change).");

        if ((endsAt - startsAt).TotalHours > 24)
        {
            throw RequestValidationException.For("endTime", "A reservation can last at most 24 hours.");
        }

        var buffer = SportRules.For(facility, facilitySport).BufferMinutes;
        return (startsAt, endsAt, endsAt.AddMinutes(buffer));
    }

    /// <summary>Conflict check before saving (story 20): other reservations and manual blocks.</summary>
    private async Task EnsureFreeAsync(Facility facility, Guid sportId, DateTime startsAt, DateTime endsAt, DateTime blockedUntil, Guid? excludeBookingId, CancellationToken cancellationToken)
    {
        var busy = (await _availability.LoadBusyAsync(new[] { facility.Id }, startsAt.AddDays(-1), blockedUntil.AddDays(1), excludeBookingId, cancellationToken))[facility.Id];
        if (!ScheduleEngine.Collides(busy, sportId, startsAt, endsAt, blockedUntil))
        {
            return;
        }

        var conflicts = await _db.Bookings.AsNoTracking()
            .Where(b => b.FacilityId == facility.Id && b.Id != excludeBookingId
                        && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                        && b.StartsAt < blockedUntil && b.BlockedUntil > startsAt)
            .ToListAsync(cancellationToken);
        var blocks = await _db.AvailabilityBlocks.AsNoTracking()
            .Where(b => b.FacilityId == facility.Id && b.StartsAt < endsAt && b.EndsAt > startsAt && (b.SportId == null || b.SportId == sportId))
            .Select(b => new { b.Id, b.StartsAt, b.EndsAt, b.Reason })
            .ToListAsync(cancellationToken);

        throw new ConflictException("This time conflicts with another reservation or a blocked period.", "slot_unavailable",
            new { conflicts = _lifecycle.ToConflicts(conflicts, facility.TimeZone), blocks });
    }

    private async Task<Booking> GetOwnedAsync(BusinessContext context, Guid bookingId, CancellationToken cancellationToken) =>
        await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.BusinessId == context.BusinessId, cancellationToken)
        ?? throw new NotFoundException("Reservation not found.");

    private async Task<BusinessReservationResponse> MapAsync(Guid bookingId, bool includeHistory, CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new NotFoundException("Reservation not found.");
        var facility = await _db.Facilities.AsNoTracking().Include(f => f.Business).Include(f => f.Images)
            .FirstAsync(f => f.Id == booking.FacilityId, cancellationToken);
        var sport = await _db.Sports.AsNoTracking().Where(s => s.Id == booking.SportId).Select(s => s.Name).FirstAsync(cancellationToken);

        var response = new BusinessReservationResponse
        {
            ExternalChannel = booking.ExternalChannel,
            InternalNotes = booking.InternalNotes,
            CustomerUserId = booking.CustomerUserId,
            CreatedByUserId = booking.CreatedByUserId
        };
        ReservationSupport.Fill(response, booking, facility, sport);

        if (includeHistory)
        {
            response.History = await _db.BookingHistory.AsNoTracking()
                .Where(h => h.BookingId == booking.Id)
                .OrderBy(h => h.ChangedAt)
                .Select(h => new BookingHistoryResponse
                {
                    EventType = h.EventType, FromStatus = h.FromStatus, ToStatus = h.ToStatus, OldStartsAt = h.OldStartsAt,
                    NewStartsAt = h.NewStartsAt, ChangedByUserId = h.ChangedByUserId, Reason = h.Reason, ChangedAt = h.ChangedAt
                })
                .ToListAsync(cancellationToken);
        }

        return response;
    }
}
