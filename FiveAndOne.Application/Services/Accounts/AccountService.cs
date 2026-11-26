using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Users;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Services.Accounts;

/// <summary>"Save profile" (story 28). Email and username are not editable here.</summary>
public class UpdateProfileRequest
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(100)]
    public string? FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    [RegularExpression(@"^\+?[0-9][0-9 ()\-]{5,28}$", ErrorMessage = "Phone must be a valid phone number, e.g. +383 44 123 456.")]
    public string? PhoneNumber { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(500)]
    public string? Bio { get; set; }

    [StringLength(500)]
    public string? ProfileImageUrl { get; set; }

    [RegularExpression("^(?i)(sq|en|tr|sr)$", ErrorMessage = "Preferred language must be one of: sq, en, tr, sr.")]
    public string? PreferredLanguage { get; set; }
}

public class AdminUserQuery
{
    public UserRole? Role { get; set; }
    public UserStatus? Status { get; set; }

    /// <summary>Name, username, email or phone.</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AdminUserDetailsResponse
{
    public UserResponse User { get; set; } = default!;
    public int Reservations { get; set; }
    public int UpcomingReservations { get; set; }
    public int NoShows { get; set; }
    public List<Dtos.Businesses.MyBusinessResponse> Businesses { get; set; } = new();
}

public interface IAccountService
{
    Task<UserResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>Everything stored about the user (data-subject access request, story 52).</summary>
    Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken = default);

    // Admin (stories 47, 48)
    Task<PagedResponse<UserResponse>> AdminSearchAsync(AdminUserQuery query, CancellationToken cancellationToken = default);
    Task<AdminUserDetailsResponse> AdminGetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserResponse> AdminSetStatusAsync(Guid actorId, Guid userId, UserStatus status, string? reason, CancellationToken cancellationToken = default);
}

public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public AccountService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<UserResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        UserResponse.FromUser(await GetActiveAsync(userId, tracked: false, cancellationToken));

    public async Task<UserResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveAsync(userId, tracked: true, cancellationToken);

        user.FirstName = request.FirstName!.Trim();
        user.LastName = Text.NullIfEmpty(request.LastName);
        user.PhoneNumber = Text.NullIfEmpty(request.PhoneNumber);
        user.City = Text.NullIfEmpty(request.City);
        user.Bio = Text.NullIfEmpty(request.Bio);
        user.ProfileImageUrl = Text.NullIfEmpty(request.ProfileImageUrl);
        if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
        {
            user.PreferredLanguage = request.PreferredLanguage.Trim().ToLowerInvariant();
        }
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return UserResponse.FromUser(user);
    }

    public async Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("User no longer exists.");

        return new
        {
            exportedAt = DateTime.UtcNow,
            profile = UserResponse.FromUser(user),
            sportProfiles = await _db.UserSportProfiles.AsNoTracking().Where(p => p.UserId == userId)
                .Select(p => new { p.SportId, sport = p.Sport.Name, p.SkillLevel, p.PreferredPosition }).ToListAsync(cancellationToken),
            reservations = await _db.Bookings.AsNoTracking().Where(b => b.CustomerUserId == userId)
                .OrderByDescending(b => b.StartsAt)
                .Select(b => new
                {
                    b.BookingNumber, facility = b.Facility.Name, sport = b.Sport.Name, b.StartsAt, b.EndsAt, b.Price, b.Currency,
                    b.Status, b.ContactName, b.ContactPhone, b.ContactEmail, b.CustomerNotes, b.CreatedAt, b.CancelledAt, b.CancellationReason
                }).ToListAsync(cancellationToken),
            reviews = await _db.Reviews.AsNoTracking().Where(r => r.UserId == userId)
                .Select(r => new { facility = r.Facility.Name, r.Rating, r.Comment, r.Status, r.CreatedAt }).ToListAsync(cancellationToken),
            teams = await _db.TeamMembers.AsNoTracking().Where(m => m.UserId == userId)
                .Select(m => new { team = m.Team.Name, m.Role, m.Status, m.CreatedAt }).ToListAsync(cancellationToken),
            businessMemberships = await _db.BusinessMembers.AsNoTracking().Where(m => m.UserId == userId)
                .Select(m => new { business = m.Business.BusinessName, m.Role, m.IsActive, m.CreatedAt }).ToListAsync(cancellationToken),
            sessions = await _db.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId)
                .Select(t => new { t.CreatedAt, t.ExpiresAt, t.RevokedAt }).ToListAsync(cancellationToken)
        };
    }

    public async Task<PagedResponse<UserResponse>> AdminSearchAsync(AdminUserQuery query, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var q = _db.Users.AsNoTracking();

        if (query.Role.HasValue) q = q.Where(u => u.Role == query.Role);
        if (query.Status.HasValue) q = q.Where(u => u.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{Text.EscapeLike(query.Search.Trim())}%";
            q = q.Where(u => EF.Functions.ILike(u.FirstName, pattern, "\\") || (u.LastName != null && EF.Functions.ILike(u.LastName, pattern, "\\"))
                             || EF.Functions.ILike(u.Username, pattern, "\\") || EF.Functions.ILike(u.Email, pattern, "\\")
                             || (u.PhoneNumber != null && EF.Functions.ILike(u.PhoneNumber, pattern, "\\")));
        }

        var total = await q.CountAsync(cancellationToken);
        var users = await q.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return PagedResponse<UserResponse>.Create(users.Select(UserResponse.FromUser).ToList(), page, pageSize, total);
    }

    public async Task<AdminUserDetailsResponse> AdminGetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User not found.");
        var now = DateTime.UtcNow;

        return new AdminUserDetailsResponse
        {
            User = UserResponse.FromUser(user),
            Reservations = await _db.Bookings.CountAsync(b => b.CustomerUserId == userId, cancellationToken),
            UpcomingReservations = await _db.Bookings.CountAsync(b => b.CustomerUserId == userId && b.Status == BookingStatus.Confirmed && b.StartsAt > now, cancellationToken),
            NoShows = await _db.Bookings.CountAsync(b => b.CustomerUserId == userId && b.Status == BookingStatus.NoShow, cancellationToken),
            Businesses = await _db.BusinessMembers.AsNoTracking().Where(m => m.UserId == userId)
                .Select(m => new Dtos.Businesses.MyBusinessResponse
                {
                    Id = m.BusinessId, BusinessName = m.Business.BusinessName, Status = m.Business.Status, Role = m.Role,
                    Permissions = m.Role == BusinessMemberRole.Owner ? BusinessPermissions.All : m.Permissions, City = m.Business.City
                }).ToListAsync(cancellationToken)
        };
    }

    /// <summary>Suspend / reactivate an account (story 48). Suspending signs the user out everywhere.</summary>
    public async Task<UserResponse> AdminSetStatusAsync(Guid actorId, Guid userId, UserStatus status, string? reason, CancellationToken cancellationToken = default)
    {
        if (actorId == userId)
        {
            throw new BadRequestException("You can't change your own account status.");
        }

        if (status is not (UserStatus.Active or UserStatus.Suspended))
        {
            throw new BadRequestException("Status must be Active or Suspended. Users delete (deactivate) their own accounts.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken) ?? throw new NotFoundException("User not found.");
        if (user.Role == UserRole.SuperAdmin)
        {
            throw new ForbiddenException("A SuperAdmin account can't be suspended.", "superadmin_protected");
        }
        if (user.Status == UserStatus.Deactivated)
        {
            throw new ConflictException("This account was deleted by its owner.", "account_deactivated");
        }

        var now = DateTime.UtcNow;
        var old = user.Status;
        user.Status = status;
        user.UpdatedAt = now;

        if (status == UserStatus.Suspended)
        {
            await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
        }

        _audit.Log(actorId, status == UserStatus.Suspended ? "user.suspended" : "user.reactivated", "User", userId.ToString(),
            $"{old} -> {status}. {reason}".Trim());
        await _db.SaveChangesAsync(cancellationToken);
        return UserResponse.FromUser(user);
    }

    private async Task<Domain.Entities.Identity.User> GetActiveAsync(Guid userId, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? _db.Users : _db.Users.AsNoTracking();
        var user = await query.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is { IsActive: true } ? user : throw new UnauthorizedException("User no longer exists.");
    }
}
