using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Dtos.Bookings;
using FiveAndOne.Application.Services.Availability;
using FiveAndOne.Application.Services.Bookings;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Application.Services.Subscriptions;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FiveAndOne.Application.Services.Dashboard;

public class DashboardOverviewResponse
{
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = default!;
    public BusinessStatus BusinessStatus { get; set; }

    public SubscriptionSummary Subscription { get; set; } = new();

    public int TotalFacilities { get; set; }
    public int PublishedFacilities { get; set; }
    public int DraftFacilities { get; set; }
    public int HiddenOrSuspendedFacilities { get; set; }

    public int ReservationsToday { get; set; }
    public int ReservationsNext7Days { get; set; }
    public int SportXNext7Days { get; set; }
    public int ExternalNext7Days { get; set; }

    /// <summary>Next reservations (empty list = "No upcoming reservations.").</summary>
    public List<BusinessReservationResponse> UpcomingReservations { get; set; } = new();

    /// <summary>Which quick actions make sense right now.</summary>
    public bool CanAddFacility { get; set; }
    public bool CanTakeReservations { get; set; }
}

public class SubscriptionSummary
{
    public bool HasActiveSubscription { get; set; }
    public string? PlanName { get; set; }
    public SubscriptionStatus? Status { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? GraceEndsAt { get; set; }
    public int MaxFacilities { get; set; }
}

public class AnalyticsQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? FacilityId { get; set; }
}

public class AnalyticsResponse
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    /// <summary>Reservations that took place or are planned (cancelled/expired excluded).</summary>
    public int TotalReservations { get; set; }
    public int SportXReservations { get; set; }
    public int ExternalReservations { get; set; }
    public int CancelledReservations { get; set; }
    public int NoShows { get; set; }
    public double BookedHours { get; set; }

    /// <summary>Booked hours / open hours, in percent (only where opening hours are known).</summary>
    public double? UtilizationPercent { get; set; }

    public Dictionary<BookingStatus, int> ByStatus { get; set; } = new();
    public List<FacilityAnalytics> ByFacility { get; set; } = new();
    public List<DayAnalytics> ByDay { get; set; } = new();
    public List<SportAnalytics> BySport { get; set; } = new();

    /// <summary>Local hour of day (0-23) → reservations starting in it.</summary>
    public Dictionary<int, int> ByHourOfDay { get; set; } = new();
}

public class FacilityAnalytics
{
    public Guid FacilityId { get; set; }
    public string Name { get; set; } = default!;
    public int Reservations { get; set; }
    public int SportX { get; set; }
    public int External { get; set; }
    public double BookedHours { get; set; }
    public double OpenHours { get; set; }
    public double? UtilizationPercent { get; set; }
}

public class DayAnalytics
{
    public DateOnly Date { get; set; }
    public int SportX { get; set; }
    public int External { get; set; }
    public int Cancelled { get; set; }
}

public class SportAnalytics
{
    public Guid SportId { get; set; }
    public string Name { get; set; } = default!;
    public int Reservations { get; set; }
}

public interface IBusinessDashboardService
{
    Task<DashboardOverviewResponse> GetOverviewAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default);
    Task<AnalyticsResponse> GetAnalyticsAsync(Guid userId, Guid? businessId, AnalyticsQuery query, CancellationToken cancellationToken = default);
}

public class BusinessDashboardService : IBusinessDashboardService
{
    private const string MarketTimeZone = "Europe/Belgrade";

    private readonly ApplicationDbContext _db;
    private readonly IBusinessAccess _access;
    private readonly IBusinessReservationService _reservations;
    private readonly FacilitySettings _settings;

    public BusinessDashboardService(ApplicationDbContext db, IBusinessAccess access, IBusinessReservationService reservations, IOptions<FacilitySettings> settings)
    {
        _db = db;
        _access = access;
        _reservations = reservations;
        _settings = settings.Value;
    }

    public async Task<DashboardOverviewResponse> GetOverviewAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);
        var id = context.BusinessId;

        var limits = await PlanLimits.GetAsync(_db, id, _settings.MaxFacilitiesWithoutSubscription, cancellationToken);
        var subscription = await _db.BusinessSubscriptions.AsNoTracking().Include(s => s.Plan)
            .Where(s => s.BusinessId == id && s.Status != SubscriptionStatus.Incomplete)
            .OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        var statuses = await _db.Facilities.AsNoTracking().Where(f => f.BusinessId == id)
            .GroupBy(f => f.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);

        var today = LocalTime.Today(MarketTimeZone);
        var (todayStart, todayEnd) = LocalTime.DayRangeUtc(today, MarketTimeZone);
        var weekEnd = LocalTime.DayRangeUtc(today.AddDays(6), MarketTimeZone).End;
        var now = DateTime.UtcNow;

        var active = _db.Bookings.AsNoTracking().Where(b => b.BusinessId == id && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending));
        var week = await active.Where(b => b.StartsAt >= todayStart && b.StartsAt < weekEnd)
            .GroupBy(b => b.Source).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);

        var canTakeReservations = context.Business.Status == BusinessStatus.Active && limits.HasActiveSubscription;
        var permissions = context.Has(BusinessPermissions.ManageReservations);

        return new DashboardOverviewResponse
        {
            BusinessId = id,
            BusinessName = context.Business.BusinessName,
            BusinessStatus = context.Business.Status,
            Subscription = new SubscriptionSummary
            {
                HasActiveSubscription = limits.HasActiveSubscription,
                PlanName = subscription?.Plan.Name,
                Status = subscription?.Status,
                CurrentPeriodEnd = subscription?.CurrentPeriodEnd,
                GraceEndsAt = subscription?.GraceEndsAt,
                MaxFacilities = limits.MaxFacilities
            },
            TotalFacilities = statuses.Sum(s => s.Count),
            PublishedFacilities = statuses.Where(s => s.Key == FacilityStatus.Published).Sum(s => s.Count),
            DraftFacilities = statuses.Where(s => s.Key == FacilityStatus.Draft).Sum(s => s.Count),
            HiddenOrSuspendedFacilities = statuses.Where(s => s.Key is FacilityStatus.Hidden or FacilityStatus.Suspended).Sum(s => s.Count),
            ReservationsToday = await active.CountAsync(b => b.StartsAt >= todayStart && b.StartsAt < todayEnd, cancellationToken),
            ReservationsNext7Days = week.Sum(w => w.Count),
            SportXNext7Days = week.Where(w => w.Key == BookingSource.SportX).Sum(w => w.Count),
            ExternalNext7Days = week.Where(w => w.Key == BookingSource.External).Sum(w => w.Count),
            UpcomingReservations = permissions
                ? (await _reservations.ListAsync(userId, id, new BusinessReservationQuery { Scope = "upcoming", PageSize = 10 }, cancellationToken)).Items
                    .Where(r => r.Status is BookingStatus.Confirmed or BookingStatus.Pending).ToList()
                : new List<BusinessReservationResponse>(),
            CanAddFacility = statuses.Sum(s => s.Count) < limits.MaxFacilities && context.Business.Status != BusinessStatus.Suspended,
            CanTakeReservations = canTakeReservations
        };
    }

    public async Task<AnalyticsResponse> GetAnalyticsAsync(Guid userId, Guid? businessId, AnalyticsQuery query, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ViewAnalytics, cancellationToken);
        var to = query.To ?? LocalTime.Today(MarketTimeZone);
        var from = query.From ?? to.AddDays(-29);
        if (from > to) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) from = to.AddDays(-366);

        var rangeStart = LocalTime.DayRangeUtc(from, MarketTimeZone).Start;
        var rangeEnd = LocalTime.DayRangeUtc(to, MarketTimeZone).End;

        var facilitiesQuery = _db.Facilities.AsNoTracking().Where(f => f.BusinessId == context.BusinessId);
        if (query.FacilityId.HasValue) facilitiesQuery = facilitiesQuery.Where(f => f.Id == query.FacilityId);
        var facilities = await facilitiesQuery.Include(f => f.OperatingHours).Include(f => f.ScheduleExceptions).AsSplitQuery().ToListAsync(cancellationToken);
        var facilityIds = facilities.Select(f => f.Id).ToList();

        var bookings = await _db.Bookings.AsNoTracking()
            .Where(b => facilityIds.Contains(b.FacilityId) && b.StartsAt >= rangeStart && b.StartsAt < rangeEnd)
            .Select(b => new { b.FacilityId, b.SportId, b.Source, b.Status, b.StartsAt, b.EndsAt })
            .ToListAsync(cancellationToken);

        var sportNames = await _db.Sports.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        static bool Counts(BookingStatus s) => s is BookingStatus.Confirmed or BookingStatus.Completed or BookingStatus.NoShow or BookingStatus.Pending;
        static bool Cancelled(BookingStatus s) => s is BookingStatus.CancelledByBusiness or BookingStatus.CancelledByCustomer;

        var counted = bookings.Where(b => Counts(b.Status)).ToList();
        var response = new AnalyticsResponse
        {
            From = from,
            To = to,
            TotalReservations = counted.Count,
            SportXReservations = counted.Count(b => b.Source == BookingSource.SportX),
            ExternalReservations = counted.Count(b => b.Source == BookingSource.External),
            CancelledReservations = bookings.Count(b => Cancelled(b.Status)),
            NoShows = bookings.Count(b => b.Status == BookingStatus.NoShow),
            BookedHours = Math.Round(counted.Where(b => b.Status != BookingStatus.NoShow).Sum(b => (b.EndsAt - b.StartsAt).TotalHours), 1),
            ByStatus = bookings.GroupBy(b => b.Status).ToDictionary(g => g.Key, g => g.Count()),
            BySport = counted.GroupBy(b => b.SportId)
                .Select(g => new SportAnalytics { SportId = g.Key, Name = sportNames.GetValueOrDefault(g.Key, "?"), Reservations = g.Count() })
                .OrderByDescending(s => s.Reservations).ToList(),
            ByHourOfDay = counted.GroupBy(b => LocalTime.ToLocal(b.StartsAt, MarketTimeZone).Hour).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count())
        };

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var (start, end) = LocalTime.DayRangeUtc(date, MarketTimeZone);
            var day = bookings.Where(b => b.StartsAt >= start && b.StartsAt < end).ToList();
            response.ByDay.Add(new DayAnalytics
            {
                Date = date,
                SportX = day.Count(b => Counts(b.Status) && b.Source == BookingSource.SportX),
                External = day.Count(b => Counts(b.Status) && b.Source == BookingSource.External),
                Cancelled = day.Count(b => Cancelled(b.Status))
            });
        }

        double totalOpen = 0, totalBookedInOpen = 0;
        foreach (var facility in facilities)
        {
            double openHours = 0;
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var windows = ScheduleEngine.ToUtc(date,
                    ScheduleEngine.WindowsFor(date, null, facility.OperatingHours.ToList(), facility.ScheduleExceptions.ToList()), facility.TimeZone);
                openHours += windows.Sum(w => (w.End - w.Start).TotalHours);
            }

            var own = counted.Where(b => b.FacilityId == facility.Id).ToList();
            var booked = own.Where(b => b.Status != BookingStatus.NoShow).Sum(b => (b.EndsAt - b.StartsAt).TotalHours);
            totalOpen += openHours;
            totalBookedInOpen += openHours > 0 ? booked : 0;

            response.ByFacility.Add(new FacilityAnalytics
            {
                FacilityId = facility.Id,
                Name = facility.Name,
                Reservations = own.Count,
                SportX = own.Count(b => b.Source == BookingSource.SportX),
                External = own.Count(b => b.Source == BookingSource.External),
                BookedHours = Math.Round(booked, 1),
                OpenHours = Math.Round(openHours, 1),
                UtilizationPercent = openHours > 0 ? Math.Round(Math.Min(100, booked / openHours * 100), 1) : null
            });
        }

        response.UtilizationPercent = totalOpen > 0 ? Math.Round(Math.Min(100, totalBookedInOpen / totalOpen * 100), 1) : null;
        response.ByFacility = response.ByFacility.OrderByDescending(f => f.Reservations).ToList();
        return response;
    }
}
