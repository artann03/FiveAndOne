using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Businesses;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Admin;

public class AdminBusinessResponse
{
    public Guid Id { get; set; }
    public string BusinessName { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public BusinessStatus Status { get; set; }
    public string? SuspensionReason { get; set; }
    public Guid OwnerUserId { get; set; }
    public string OwnerName { get; set; } = default!;
    public string OwnerEmail { get; set; } = default!;
    public int Facilities { get; set; }
    public int PublishedFacilities { get; set; }
    public string? PlanName { get; set; }
    public SubscriptionStatus? SubscriptionStatus { get; set; }
    public DateTime? InactiveSince { get; set; }
    public DateTime? RetentionUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminBusinessDetailsResponse : AdminBusinessResponse
{
    /// <summary>Visible to admins only, for verification.</summary>
    public string? RegistrationNumber { get; set; }
    public string AddressLine { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string PreferredLanguage { get; set; } = default!;
    public int UpcomingReservations { get; set; }
    public int TotalReservations { get; set; }
    public List<BusinessMemberAdminDto> Members { get; set; } = new();
}

public class BusinessMemberAdminDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public BusinessMemberRole Role { get; set; }
    public BusinessPermissions Permissions { get; set; }
    public bool IsActive { get; set; }
}

public class AdminDashboardResponse
{
    public int Customers { get; set; }
    public int BusinessAccounts { get; set; }
    public int Businesses { get; set; }
    public int ActiveBusinesses { get; set; }
    public int PublishedFacilities { get; set; }
    public int ReservationsToday { get; set; }
    public int ReservationsNext7Days { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int PastDueSubscriptions { get; set; }
    public int OpenReports { get; set; }
    public int FailedNotifications { get; set; }
    public List<AuditLogResponse> RecentActivity { get; set; } = new();
}

public class PlatformAnalyticsResponse
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public Dictionary<string, int> UsersByRole { get; set; } = new();
    public Dictionary<string, int> UsersByStatus { get; set; } = new();
    public Dictionary<string, int> BusinessesByStatus { get; set; } = new();
    public Dictionary<string, int> FacilitiesByStatus { get; set; } = new();
    public Dictionary<string, int> SubscriptionsByStatus { get; set; } = new();
    public Dictionary<string, int> ReservationsByStatus { get; set; } = new();
    public int SportXReservations { get; set; }
    public int ExternalReservations { get; set; }
    public int GuestReservations { get; set; }
    public decimal SubscriptionRevenue { get; set; }
    public List<PlatformDayStat> ByDay { get; set; } = new();
}

public class PlatformDayStat
{
    public DateOnly Date { get; set; }
    public int NewUsers { get; set; }
    public int NewBusinesses { get; set; }
    public int Reservations { get; set; }
}

public interface IAdminPlatformService
{
    Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<PlatformAnalyticsResponse> GetAnalyticsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    Task<PagedResponse<AdminBusinessResponse>> GetBusinessesAsync(BusinessStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminBusinessDetailsResponse> GetBusinessAsync(Guid businessId, CancellationToken cancellationToken = default);
    Task<AdminBusinessDetailsResponse> SuspendBusinessAsync(Guid actorId, Guid businessId, string reason, CancellationToken cancellationToken = default);
    Task<AdminBusinessDetailsResponse> UnsuspendBusinessAsync(Guid actorId, Guid businessId, CancellationToken cancellationToken = default);
}

public class AdminPlatformService : IAdminPlatformService
{
    private const string MarketTimeZone = "Europe/Belgrade";

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public AdminPlatformService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var today = LocalTime.Today(MarketTimeZone);
        var (start, end) = LocalTime.DayRangeUtc(today, MarketTimeZone);
        var weekEnd = LocalTime.DayRangeUtc(today.AddDays(6), MarketTimeZone).End;
        var active = _db.Bookings.Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending);

        return new AdminDashboardResponse
        {
            Customers = await _db.Users.CountAsync(u => u.Role == UserRole.User, cancellationToken),
            BusinessAccounts = await _db.Users.CountAsync(u => u.Role == UserRole.Business, cancellationToken),
            Businesses = await _db.Businesses.CountAsync(b => b.AnonymizedAt == null, cancellationToken),
            ActiveBusinesses = await _db.Businesses.CountAsync(b => b.Status == BusinessStatus.Active, cancellationToken),
            PublishedFacilities = await _db.Facilities.CountAsync(f => f.Status == FacilityStatus.Published, cancellationToken),
            ReservationsToday = await active.CountAsync(b => b.StartsAt >= start && b.StartsAt < end, cancellationToken),
            ReservationsNext7Days = await active.CountAsync(b => b.StartsAt >= start && b.StartsAt < weekEnd, cancellationToken),
            ActiveSubscriptions = await _db.BusinessSubscriptions.CountAsync(s => s.Status == SubscriptionStatus.Active, cancellationToken),
            PastDueSubscriptions = await _db.BusinessSubscriptions.CountAsync(s => s.Status == SubscriptionStatus.PastDue, cancellationToken),
            OpenReports = await _db.ContentReports.CountAsync(r => r.Status == ReportStatus.Open, cancellationToken),
            FailedNotifications = await _db.NotificationMessages.CountAsync(n => n.Status == NotificationStatus.Failed, cancellationToken),
            RecentActivity = (await _audit.SearchAsync(new AuditLogQuery { PageSize = 15 }, cancellationToken)).Items
        };
    }

    public async Task<PlatformAnalyticsResponse> GetAnalyticsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var end = to ?? LocalTime.Today(MarketTimeZone);
        var start = from ?? end.AddDays(-29);
        if (start > end) (start, end) = (end, start);
        if (end.DayNumber - start.DayNumber > 366) start = end.AddDays(-366);

        var startUtc = LocalTime.DayRangeUtc(start, MarketTimeZone).Start;
        var endUtc = LocalTime.DayRangeUtc(end, MarketTimeZone).End;

        var bookings = await _db.Bookings.AsNoTracking().Where(b => b.StartsAt >= startUtc && b.StartsAt < endUtc)
            .Select(b => new { b.StartsAt, b.Status, b.Source, b.IsGuest }).ToListAsync(cancellationToken);
        var users = await _db.Users.AsNoTracking().Where(u => u.CreatedAt >= startUtc && u.CreatedAt < endUtc).Select(u => u.CreatedAt).ToListAsync(cancellationToken);
        var businesses = await _db.Businesses.AsNoTracking().Where(b => b.CreatedAt >= startUtc && b.CreatedAt < endUtc).Select(b => b.CreatedAt).ToListAsync(cancellationToken);

        var response = new PlatformAnalyticsResponse
        {
            From = start,
            To = end,
            UsersByRole = await _db.Users.GroupBy(u => u.Role).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key.ToString(), x => x.C, cancellationToken),
            UsersByStatus = await _db.Users.GroupBy(u => u.Status).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key.ToString(), x => x.C, cancellationToken),
            BusinessesByStatus = await _db.Businesses.GroupBy(b => b.Status).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key.ToString(), x => x.C, cancellationToken),
            FacilitiesByStatus = await _db.Facilities.GroupBy(f => f.Status).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key.ToString(), x => x.C, cancellationToken),
            SubscriptionsByStatus = await _db.BusinessSubscriptions.GroupBy(s => s.Status).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key.ToString(), x => x.C, cancellationToken),
            ReservationsByStatus = bookings.GroupBy(b => b.Status).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            SportXReservations = bookings.Count(b => b.Source == BookingSource.SportX),
            ExternalReservations = bookings.Count(b => b.Source == BookingSource.External),
            GuestReservations = bookings.Count(b => b.IsGuest),
            SubscriptionRevenue = await _db.Payments.Where(p => p.Purpose == PaymentPurpose.Subscription && p.Status == PaymentRecordStatus.Succeeded
                                                               && p.PaidAt >= startUtc && p.PaidAt < endUtc).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0
        };

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var (s, e) = LocalTime.DayRangeUtc(date, MarketTimeZone);
            response.ByDay.Add(new PlatformDayStat
            {
                Date = date,
                NewUsers = users.Count(c => c >= s && c < e),
                NewBusinesses = businesses.Count(c => c >= s && c < e),
                Reservations = bookings.Count(b => b.StartsAt >= s && b.StartsAt < e)
            });
        }

        return response;
    }

    public async Task<PagedResponse<AdminBusinessResponse>> GetBusinessesAsync(BusinessStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = _db.Businesses.AsNoTracking();
        if (status.HasValue) q = q.Where(b => b.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{Text.EscapeLike(search.Trim())}%";
            q = q.Where(b => EF.Functions.ILike(b.BusinessName, pattern, "\\") || EF.Functions.ILike(b.Email, pattern, "\\") || EF.Functions.ILike(b.City, pattern, "\\")
                             || (b.RegistrationNumber != null && EF.Functions.ILike(b.RegistrationNumber, pattern, "\\")));
        }

        var total = await q.CountAsync(cancellationToken);
        var ids = await q.OrderByDescending(b => b.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(b => b.Id).ToListAsync(cancellationToken);

        var items = new List<AdminBusinessResponse>();
        foreach (var id in ids)
        {
            items.Add(await LoadAsync(id, details: false, cancellationToken));
        }
        return PagedResponse<AdminBusinessResponse>.Create(items, page, pageSize, total);
    }

    public async Task<AdminBusinessDetailsResponse> GetBusinessAsync(Guid businessId, CancellationToken cancellationToken = default) =>
        (AdminBusinessDetailsResponse)await LoadAsync(businessId, details: true, cancellationToken);

    /// <summary>Hides the business from discovery and stops new reservations; existing ones are kept (owner informs customers).</summary>
    public async Task<AdminBusinessDetailsResponse> SuspendBusinessAsync(Guid actorId, Guid businessId, string reason, CancellationToken cancellationToken = default)
    {
        var business = await _db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, cancellationToken) ?? throw new NotFoundException("Business not found.");
        if (business.AnonymizedAt is not null) throw new ConflictException("This business was anonymized.", "business_anonymized");

        business.Status = BusinessStatus.Suspended;
        business.SuspendedAt = DateTime.UtcNow;
        business.SuspensionReason = reason.Trim();
        business.UpdatedAt = DateTime.UtcNow;
        _audit.Log(actorId, "business.suspended", "Business", businessId.ToString(), reason.Trim(), businessId: businessId);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetBusinessAsync(businessId, cancellationToken);
    }

    public async Task<AdminBusinessDetailsResponse> UnsuspendBusinessAsync(Guid actorId, Guid businessId, CancellationToken cancellationToken = default)
    {
        var business = await _db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, cancellationToken) ?? throw new NotFoundException("Business not found.");
        if (business.Status != BusinessStatus.Suspended) throw new ConflictException("This business is not suspended.", "invalid_status");

        // Back to Inactive if its subscription lapsed in the meantime, otherwise Active.
        var lapsed = await _db.BusinessSubscriptions.AnyAsync(s => s.BusinessId == businessId && s.Status == SubscriptionStatus.Expired, cancellationToken)
                     && !await _db.BusinessSubscriptions.AnyAsync(s => s.BusinessId == businessId && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue), cancellationToken);
        business.Status = lapsed ? BusinessStatus.Inactive : BusinessStatus.Active;
        business.SuspendedAt = null;
        business.SuspensionReason = null;
        business.UpdatedAt = DateTime.UtcNow;
        _audit.Log(actorId, "business.unsuspended", "Business", businessId.ToString(), $"Now {business.Status}", businessId: businessId);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetBusinessAsync(businessId, cancellationToken);
    }

    private async Task<AdminBusinessResponse> LoadAsync(Guid businessId, bool details, CancellationToken cancellationToken)
    {
        var b = await _db.Businesses.AsNoTracking().Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == businessId, cancellationToken)
            ?? throw new NotFoundException("Business not found.");
        var subscription = await _db.BusinessSubscriptions.AsNoTracking().Include(s => s.Plan)
            .Where(s => s.BusinessId == businessId && s.Status != SubscriptionStatus.Incomplete)
            .OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        AdminBusinessResponse response = details ? new AdminBusinessDetailsResponse() : new AdminBusinessResponse();
        response.Id = b.Id;
        response.BusinessName = b.BusinessName;
        response.City = b.City;
        response.Email = b.Email;
        response.Phone = b.Phone;
        response.Status = b.Status;
        response.SuspensionReason = b.SuspensionReason;
        response.OwnerUserId = b.OwnerUserId;
        response.OwnerName = $"{b.Owner.FirstName} {b.Owner.LastName}".Trim();
        response.OwnerEmail = b.Owner.Email;
        response.Facilities = await _db.Facilities.CountAsync(f => f.BusinessId == b.Id, cancellationToken);
        response.PublishedFacilities = await _db.Facilities.CountAsync(f => f.BusinessId == b.Id && f.Status == FacilityStatus.Published, cancellationToken);
        response.PlanName = subscription?.Plan.Name;
        response.SubscriptionStatus = subscription?.Status;
        response.InactiveSince = b.InactiveSince;
        response.RetentionUntil = b.RetentionUntil;
        response.CreatedAt = b.CreatedAt;

        if (response is AdminBusinessDetailsResponse d)
        {
            var now = DateTime.UtcNow;
            d.RegistrationNumber = b.RegistrationNumber;
            d.AddressLine = b.AddressLine;
            d.Country = b.Country;
            d.PreferredLanguage = b.PreferredLanguage;
            d.TotalReservations = await _db.Bookings.CountAsync(x => x.BusinessId == b.Id, cancellationToken);
            d.UpcomingReservations = await _db.Bookings.CountAsync(x => x.BusinessId == b.Id && x.Status == BookingStatus.Confirmed && x.StartsAt > now, cancellationToken);
            d.Members = await _db.BusinessMembers.AsNoTracking().Where(m => m.BusinessId == b.Id)
                .Select(m => new BusinessMemberAdminDto
                {
                    UserId = m.UserId, Name = m.User.FirstName + " " + m.User.LastName, Email = m.User.Email, Role = m.Role,
                    Permissions = m.Role == BusinessMemberRole.Owner ? BusinessPermissions.All : m.Permissions, IsActive = m.IsActive
                }).ToListAsync(cancellationToken);
        }

        return response;
    }
}
