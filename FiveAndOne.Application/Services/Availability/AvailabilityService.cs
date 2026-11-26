using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Availability;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Availability;

/// <summary>Shared facility queries.</summary>
public static class FacilityQueries
{
    /// <summary>
    /// Facilities the public may see and book: published, business active (not suspended or unpaid
    /// after grace) and the owner account active.
    /// </summary>
    public static IQueryable<Facility> Public(this IQueryable<Facility> facilities) => facilities.Where(f =>
        f.Status == FacilityStatus.Published
        && f.Business.Status == BusinessStatus.Active
        && f.Business.Owner.Status == UserStatus.Active);

    /// <summary>Everything the schedule engine needs.</summary>
    public static IQueryable<Facility> WithSchedule(this IQueryable<Facility> facilities) => facilities
        .Include(f => f.Business)
        .Include(f => f.Sports).ThenInclude(s => s.Sport)
        .Include(f => f.OperatingHours)
        .Include(f => f.ScheduleExceptions)
        .Include(f => f.PriceRules)
        .AsSplitQuery();

    public static IQueryable<Facility> ByIdOrSlug(this IQueryable<Facility> facilities, string idOrSlug) =>
        Guid.TryParse(idOrSlug, out var id)
            ? facilities.Where(f => f.Id == id)
            : facilities.Where(f => f.Slug == idOrSlug.ToLowerInvariant());
}

public interface IAvailabilityService
{
    Task<Dictionary<Guid, List<BusyInterval>>> LoadBusyAsync(IReadOnlyCollection<Guid> facilityIds, DateTime fromUtc, DateTime toUtc,
        Guid? excludeBookingId = null, CancellationToken cancellationToken = default);

    DaySlotsResponse BuildDay(Facility facility, Guid sportId, DateOnly date, int? durationMinutes, IReadOnlyCollection<BusyInterval> busy, bool includeUnavailable = true);

    Task<DaySlotsResponse> GetPublicDayAsync(string idOrSlug, Guid sportId, DateOnly date, int? durationMinutes, CancellationToken cancellationToken = default);
    Task<CalendarResponse> GetPublicCalendarAsync(string idOrSlug, Guid sportId, DateOnly from, int days, int? durationMinutes, CancellationToken cancellationToken = default);
    Task<DaySlotsResponse> GetBusinessDayAsync(Guid userId, Guid? businessId, Guid facilityId, Guid sportId, DateOnly date, int? durationMinutes, CancellationToken cancellationToken = default);

    /// <summary>The next free slots for a facility/sport, starting now (search cards).</summary>
    List<SlotResponse> NextAvailable(Facility facility, Guid sportId, DateOnly fromDate, int daysToScan, int count, IReadOnlyCollection<BusyInterval> busy);
}

public class AvailabilityService : IAvailabilityService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessAccess _access;

    public AvailabilityService(ApplicationDbContext db, IBusinessAccess access)
    {
        _db = db;
        _access = access;
    }

    public async Task<Dictionary<Guid, List<BusyInterval>>> LoadBusyAsync(IReadOnlyCollection<Guid> facilityIds, DateTime fromUtc, DateTime toUtc,
        Guid? excludeBookingId = null, CancellationToken cancellationToken = default)
    {
        var bookings = await _db.Bookings.AsNoTracking()
            .Where(b => facilityIds.Contains(b.FacilityId)
                        && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                        && b.StartsAt < toUtc && b.BlockedUntil > fromUtc
                        && (excludeBookingId == null || b.Id != excludeBookingId))
            .Select(b => new { b.FacilityId, b.StartsAt, b.BlockedUntil })
            .ToListAsync(cancellationToken);

        var blocks = await _db.AvailabilityBlocks.AsNoTracking()
            .Where(b => facilityIds.Contains(b.FacilityId) && b.StartsAt < toUtc && b.EndsAt > fromUtc)
            .Select(b => new { b.FacilityId, b.StartsAt, b.EndsAt, b.SportId })
            .ToListAsync(cancellationToken);

        var result = facilityIds.ToDictionary(id => id, _ => new List<BusyInterval>());
        foreach (var b in bookings) result[b.FacilityId].Add(new BusyInterval(b.StartsAt, b.BlockedUntil, null, false));
        foreach (var b in blocks) result[b.FacilityId].Add(new BusyInterval(b.StartsAt, b.EndsAt, b.SportId, true));
        return result;
    }

    public DaySlotsResponse BuildDay(Facility facility, Guid sportId, DateOnly date, int? durationMinutes, IReadOnlyCollection<BusyInterval> busy, bool includeUnavailable = true)
    {
        var facilitySport = facility.Sports.FirstOrDefault(s => s.SportId == sportId)
            ?? throw new NotFoundException("This sport is not offered at this facility.", "sport_not_offered");

        var rules = SportRules.For(facility, facilitySport);
        var duration = durationMinutes ?? rules.DurationOptions.Min();
        if (!rules.DurationOptions.Contains(duration))
        {
            throw RequestValidationException.For("durationMinutes",
                $"Choose one of the available durations: {string.Join(", ", rules.DurationOptions.OrderBy(x => x))} minutes.", "invalid_duration");
        }

        var localWindows = ScheduleEngine.WindowsFor(date, sportId, facility.OperatingHours.ToList(), facility.ScheduleExceptions.ToList());
        var windows = ScheduleEngine.ToUtc(date, localWindows, facility.TimeZone);
        var candidates = ScheduleEngine.Slots(windows, duration, rules, sportId, busy, DateTime.UtcNow);

        var response = new DaySlotsResponse
        {
            FacilityId = facility.Id,
            SportId = sportId,
            Date = date,
            TimeZone = facility.TimeZone,
            DurationMinutes = duration,
            DurationOptions = rules.DurationOptions.OrderBy(x => x).ToArray(),
            BufferMinutes = rules.BufferMinutes,
            IsClosed = windows.Count == 0
        };

        foreach (var slot in candidates)
        {
            if (!slot.Available && !includeUnavailable)
            {
                continue;
            }

            var localStart = LocalTime.ToLocal(slot.StartUtc, facility.TimeZone);
            var quote = PricingEngine.Quote(facility.PriceRules, sportId, duration, localStart.DayOfWeek, TimeOnly.FromDateTime(localStart));
            if (quote is null)
            {
                response.PricingMissing = true;
            }

            response.Slots.Add(new SlotResponse
            {
                StartsAt = slot.StartUtc,
                EndsAt = slot.EndUtc,
                LocalStartsAt = localStart,
                LocalEndsAt = LocalTime.ToLocal(slot.EndUtc, facility.TimeZone),
                // A slot without a price can't be booked (the business hasn't finished pricing).
                Available = slot.Available && quote is not null,
                UnavailableReason = slot.Available && quote is null ? "no_price" : slot.Reason,
                Price = quote?.Amount,
                Currency = quote?.Currency
            });
        }

        return response;
    }

    public async Task<DaySlotsResponse> GetPublicDayAsync(string idOrSlug, Guid sportId, DateOnly date, int? durationMinutes, CancellationToken cancellationToken = default)
    {
        var facility = await _db.Facilities.AsNoTracking().Public().ByIdOrSlug(idOrSlug).WithSchedule().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        var busy = await LoadDayBusyAsync(facility, date, cancellationToken);
        return BuildDay(facility, sportId, date, durationMinutes, busy);
    }

    public async Task<CalendarResponse> GetPublicCalendarAsync(string idOrSlug, Guid sportId, DateOnly from, int days, int? durationMinutes, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 62);
        var facility = await _db.Facilities.AsNoTracking().Public().ByIdOrSlug(idOrSlug).WithSchedule().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        var (rangeStart, _) = LocalTime.DayRangeUtc(from, facility.TimeZone);
        var (_, rangeEnd) = LocalTime.DayRangeUtc(from.AddDays(days), facility.TimeZone);
        var busy = (await LoadBusyAsync(new[] { facility.Id }, rangeStart, rangeEnd, null, cancellationToken))[facility.Id];

        var response = new CalendarResponse { FacilityId = facility.Id, SportId = sportId, TimeZone = facility.TimeZone };
        for (var i = 0; i < days; i++)
        {
            var date = from.AddDays(i);
            var day = BuildDay(facility, sportId, date, durationMinutes, busy);
            response.DurationMinutes = day.DurationMinutes;
            response.Days.Add(new CalendarDayResponse
            {
                Date = date,
                IsOpen = !day.IsClosed,
                AvailableSlots = day.Slots.Count(s => s.Available)
            });
        }

        return response;
    }

    public async Task<DaySlotsResponse> GetBusinessDayAsync(Guid userId, Guid? businessId, Guid facilityId, Guid sportId, DateOnly date, int? durationMinutes, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);
        var facility = await _db.Facilities.AsNoTracking().Where(f => f.Id == facilityId && f.BusinessId == context.BusinessId)
            .WithSchedule().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        var busy = await LoadDayBusyAsync(facility, date, cancellationToken);
        return BuildDay(facility, sportId, date, durationMinutes, busy);
    }

    public List<SlotResponse> NextAvailable(Facility facility, Guid sportId, DateOnly fromDate, int daysToScan, int count, IReadOnlyCollection<BusyInterval> busy)
    {
        var result = new List<SlotResponse>();
        if (facility.Sports.All(s => s.SportId != sportId))
        {
            return result;
        }

        for (var i = 0; i < daysToScan && result.Count < count; i++)
        {
            var day = BuildDay(facility, sportId, fromDate.AddDays(i), null, busy, includeUnavailable: false);
            result.AddRange(day.Slots.Take(count - result.Count));
        }

        return result;
    }

    private async Task<List<BusyInterval>> LoadDayBusyAsync(Facility facility, DateOnly date, CancellationToken cancellationToken)
    {
        // The day plus the next one: overnight windows run past midnight.
        var (start, _) = LocalTime.DayRangeUtc(date, facility.TimeZone);
        var (_, end) = LocalTime.DayRangeUtc(date.AddDays(1), facility.TimeZone);
        return (await LoadBusyAsync(new[] { facility.Id }, start, end, null, cancellationToken))[facility.Id];
    }
}
