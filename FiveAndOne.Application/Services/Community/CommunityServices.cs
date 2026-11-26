using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Services.Availability;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Community;
using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Entities.Platform;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Services.Community;

// =====================================================================
// Reviews (story 63)
// =====================================================================

public class CreateReviewRequest
{
    [Required]
    public Guid? BookingId { get; set; }

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }
}

public class ReviewResponse
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string AuthorName { get; set; } = default!;
    public ReviewStatus Status { get; set; }
    public string? BusinessReply { get; set; }
    public DateTime? BusinessRepliedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReportRequest
{
    [Required, StringLength(1000, MinimumLength = 3)]
    public string? Reason { get; set; }
}

public class ReportResponse
{
    public Guid Id { get; set; }
    public ReportTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public Guid ReporterUserId { get; set; }
    public string Reason { get; set; } = default!;
    public ReportStatus Status { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ModerateReviewRequest
{
    /// <summary>Published, Hidden or Removed.</summary>
    public ReviewStatus Status { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class ResolveReportRequest
{
    /// <summary>Resolved (action taken) or Dismissed.</summary>
    public ReportStatus Status { get; set; } = ReportStatus.Resolved;

    [StringLength(500)]
    public string? Note { get; set; }
}

public interface IReviewService
{
    Task<PagedResponse<ReviewResponse>> GetForFacilityAsync(string facilityIdOrSlug, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ReviewResponse> CreateAsync(Guid userId, CreateReviewRequest request, CancellationToken cancellationToken = default);
    Task DeleteOwnAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken = default);
    Task<ReviewResponse> ReplyAsync(Guid userId, Guid? businessId, Guid reviewId, string reply, CancellationToken cancellationToken = default);
    Task ReportAsync(Guid userId, ReportTargetType type, Guid targetId, string reason, CancellationToken cancellationToken = default);

    // Moderation (admin)
    Task<PagedResponse<ReportResponse>> GetReportsAsync(ReportStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ReportResponse> ResolveReportAsync(Guid actorId, Guid reportId, ResolveReportRequest request, CancellationToken cancellationToken = default);
    Task<ReviewResponse> ModerateAsync(Guid actorId, Guid reviewId, ModerateReviewRequest request, CancellationToken cancellationToken = default);
}

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _db;
    private readonly IFeatureFlags _flags;
    private readonly IPlatformSettings _settings;
    private readonly IBusinessAccess _access;
    private readonly IAuditService _audit;

    public ReviewService(ApplicationDbContext db, IFeatureFlags flags, IPlatformSettings settings, IBusinessAccess access, IAuditService audit)
    {
        _db = db;
        _flags = flags;
        _settings = settings;
        _access = access;
        _audit = audit;
    }

    public async Task<PagedResponse<ReviewResponse>> GetForFacilityAsync(string facilityIdOrSlug, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.Reviews, cancellationToken);
        (page, pageSize) = Paging.Normalize(page, pageSize, 50);

        var facilityId = await _db.Facilities.AsNoTracking().Public().ByIdOrSlug(facilityIdOrSlug).Select(f => (Guid?)f.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        var q = _db.Reviews.AsNoTracking().Where(r => r.FacilityId == facilityId && r.Status == ReviewStatus.Published);
        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ReviewResponse
            {
                Id = r.Id, FacilityId = r.FacilityId, Rating = r.Rating, Comment = r.Comment,
                // Public display shows first name + initial only.
                AuthorName = r.User.FirstName + (r.User.LastName != null ? " " + r.User.LastName.Substring(0, 1) + "." : ""),
                Status = r.Status, BusinessReply = r.BusinessReply, BusinessRepliedAt = r.BusinessRepliedAt, CreatedAt = r.CreatedAt
            }).ToListAsync(cancellationToken);

        return PagedResponse<ReviewResponse>.Create(items, page, pageSize, total);
    }

    public async Task<ReviewResponse> CreateAsync(Guid userId, CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.Reviews, cancellationToken);

        // Verified-reservation eligibility: the reviewer's own completed reservation, within the review window.
        var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BookingId && b.CustomerUserId == userId, cancellationToken)
            ?? throw new NotFoundException("Reservation not found.");
        if (booking.Status != BookingStatus.Completed)
        {
            throw new ConflictException("You can review a facility after the reservation is completed.", "review_not_allowed");
        }

        var windowDays = await _settings.GetIntAsync(PlatformSettingKeys.ReviewWindowDays, 30, cancellationToken);
        if (booking.EndsAt.AddDays(windowDays) < DateTime.UtcNow)
        {
            throw new ConflictException($"Reviews can be written up to {windowDays} days after the visit.", "review_window_closed");
        }
        if (await _db.Reviews.AnyAsync(r => r.BookingId == booking.Id, cancellationToken))
        {
            throw new ConflictException("You already reviewed this reservation.", "already_reviewed");
        }

        var review = new Review
        {
            FacilityId = booking.FacilityId,
            BookingId = booking.Id,
            UserId = userId,
            Rating = request.Rating,
            Comment = Text.NullIfEmpty(request.Comment)
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, cancellationToken);
        return ToResponse(review, user);
    }

    public async Task DeleteOwnAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken = default)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Review not found.");
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReviewResponse> ReplyAsync(Guid userId, Guid? businessId, Guid reviewId, string reply, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.Reviews, cancellationToken);
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageProfile, cancellationToken);
        var review = await _db.Reviews.Include(r => r.User).Include(r => r.Facility)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.Facility.BusinessId == context.BusinessId, cancellationToken)
            ?? throw new NotFoundException("Review not found.");

        review.BusinessReply = Text.NullIfEmpty(reply);
        review.BusinessRepliedAt = review.BusinessReply is null ? null : DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(review, review.User);
    }

    public async Task ReportAsync(Guid userId, ReportTargetType type, Guid targetId, string reason, CancellationToken cancellationToken = default)
    {
        var exists = type switch
        {
            ReportTargetType.Review => await _db.Reviews.AnyAsync(r => r.Id == targetId, cancellationToken),
            ReportTargetType.User => await _db.Users.AnyAsync(u => u.Id == targetId, cancellationToken),
            ReportTargetType.Team => await _db.Teams.AnyAsync(t => t.Id == targetId, cancellationToken),
            _ => false
        };
        if (!exists)
        {
            throw new NotFoundException("Not found.");
        }

        if (await _db.ContentReports.AnyAsync(r => r.ReporterUserId == userId && r.TargetType == type && r.TargetId == targetId, cancellationToken))
        {
            return;   // already reported: idempotent
        }

        _db.ContentReports.Add(new ContentReport { TargetType = type, TargetId = targetId, ReporterUserId = userId, Reason = reason.Trim() });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResponse<ReportResponse>> GetReportsAsync(ReportStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = _db.ContentReports.AsNoTracking();
        if (status.HasValue) q = q.Where(r => r.Status == status);

        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderBy(r => r.Status).ThenByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ReportResponse
            {
                Id = r.Id, TargetType = r.TargetType, TargetId = r.TargetId, ReporterUserId = r.ReporterUserId, Reason = r.Reason,
                Status = r.Status, ResolutionNote = r.ResolutionNote, CreatedAt = r.CreatedAt
            }).ToListAsync(cancellationToken);
        return PagedResponse<ReportResponse>.Create(items, page, pageSize, total);
    }

    public async Task<ReportResponse> ResolveReportAsync(Guid actorId, Guid reportId, ResolveReportRequest request, CancellationToken cancellationToken = default)
    {
        var report = await _db.ContentReports.FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken) ?? throw new NotFoundException("Report not found.");
        if (request.Status is not (ReportStatus.Resolved or ReportStatus.Dismissed))
        {
            throw new BadRequestException("Status must be Resolved or Dismissed.");
        }

        report.Status = request.Status;
        report.ResolutionNote = Text.NullIfEmpty(request.Note);
        report.ResolvedAt = DateTime.UtcNow;
        report.ResolvedByUserId = actorId;
        _audit.Log(actorId, "report.resolved", "ContentReport", report.Id.ToString(), $"{report.TargetType} {report.TargetId}: {request.Status}");
        await _db.SaveChangesAsync(cancellationToken);

        return new ReportResponse
        {
            Id = report.Id, TargetType = report.TargetType, TargetId = report.TargetId, ReporterUserId = report.ReporterUserId,
            Reason = report.Reason, Status = report.Status, ResolutionNote = report.ResolutionNote, CreatedAt = report.CreatedAt
        };
    }

    public async Task<ReviewResponse> ModerateAsync(Guid actorId, Guid reviewId, ModerateReviewRequest request, CancellationToken cancellationToken = default)
    {
        var review = await _db.Reviews.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken) ?? throw new NotFoundException("Review not found.");
        review.Status = request.Status;
        review.ModerationNote = Text.NullIfEmpty(request.Note);
        review.UpdatedAt = DateTime.UtcNow;
        _audit.Log(actorId, "review.moderated", "Review", review.Id.ToString(), $"{request.Status}. {request.Note}".Trim());
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(review, review.User);
    }

    private static ReviewResponse ToResponse(Review r, User author) => new()
    {
        Id = r.Id, FacilityId = r.FacilityId, Rating = r.Rating, Comment = r.Comment,
        AuthorName = author.FirstName + (author.LastName is { Length: > 0 } last ? $" {last[0]}." : ""),
        Status = r.Status, BusinessReply = r.BusinessReply, BusinessRepliedAt = r.BusinessRepliedAt, CreatedAt = r.CreatedAt
    };
}

// =====================================================================
// Player profiles (story 64)
// =====================================================================

public class PlayerSportDto
{
    [Required]
    public Guid? SportId { get; set; }

    public SkillLevel SkillLevel { get; set; } = SkillLevel.Beginner;

    [StringLength(50)]
    public string? PreferredPosition { get; set; }
}

public class SavePlayerProfileRequest
{
    public ProfileVisibility Visibility { get; set; } = ProfileVisibility.Private;

    [StringLength(500)]
    public string? Bio { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    public List<PlayerSportDto> Sports { get; set; } = new();
}

public class PlayerProfileResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? ProfileImageUrl { get; set; }
    public string? City { get; set; }
    public string? Bio { get; set; }
    public ProfileVisibility Visibility { get; set; }
    public List<PlayerSportResponse> Sports { get; set; } = new();
}

public class PlayerSportResponse
{
    public Guid SportId { get; set; }
    public string SportName { get; set; } = default!;
    public SkillLevel SkillLevel { get; set; }
    public string? PreferredPosition { get; set; }
}

public interface IPlayerService
{
    Task<PlayerProfileResponse> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PlayerProfileResponse> SaveMineAsync(Guid userId, SavePlayerProfileRequest request, CancellationToken cancellationToken = default);
    Task<PlayerProfileResponse> GetAsync(Guid? viewerId, string username, CancellationToken cancellationToken = default);

    /// <summary>Teammate discovery (story 65): visible profiles by sport, city and level.</summary>
    Task<PagedResponse<PlayerProfileResponse>> SearchAsync(Guid? viewerId, Guid? sportId, string? city, SkillLevel? level, int page, int pageSize, CancellationToken cancellationToken = default);
}

public class PlayerService : IPlayerService
{
    private readonly ApplicationDbContext _db;
    private readonly IFeatureFlags _flags;

    public PlayerService(ApplicationDbContext db, IFeatureFlags flags)
    {
        _db = db;
        _flags = flags;
    }

    public async Task<PlayerProfileResponse> GetMineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.PlayerProfiles, cancellationToken);
        return ToResponse(await LoadAsync(u => u.Id == userId, cancellationToken) ?? throw new UnauthorizedException("User no longer exists."));
    }

    public async Task<PlayerProfileResponse> SaveMineAsync(Guid userId, SavePlayerProfileRequest request, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.PlayerProfiles, cancellationToken);
        var user = await _db.Users.Include(u => u.SportProfiles).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("User no longer exists.");

        var sportIds = request.Sports.Select(s => s.SportId!.Value).ToList();
        if (sportIds.Distinct().Count() != sportIds.Count)
        {
            throw RequestValidationException.For("sports", "Each sport can only be listed once.");
        }
        if (await _db.Sports.CountAsync(s => sportIds.Contains(s.Id), cancellationToken) != sportIds.Count)
        {
            throw RequestValidationException.For("sports", "One or more sports do not exist.");
        }

        user.ProfileVisibility = request.Visibility;
        user.Bio = Text.NullIfEmpty(request.Bio);
        user.City = Text.NullIfEmpty(request.City) ?? user.City;

        foreach (var old in user.SportProfiles.Where(p => !sportIds.Contains(p.SportId)).ToList())
        {
            user.SportProfiles.Remove(old);
        }
        foreach (var sport in request.Sports)
        {
            var row = user.SportProfiles.FirstOrDefault(p => p.SportId == sport.SportId);
            if (row is null)
            {
                row = new UserSportProfile { UserId = userId, SportId = sport.SportId!.Value };
                user.SportProfiles.Add(row);
            }
            row.SkillLevel = sport.SkillLevel;
            row.PreferredPosition = Text.NullIfEmpty(sport.PreferredPosition);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await GetMineAsync(userId, cancellationToken);
    }

    public async Task<PlayerProfileResponse> GetAsync(Guid? viewerId, string username, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.PlayerProfiles, cancellationToken);
        var user = await LoadAsync(u => u.Username == username, cancellationToken);
        if (user is null || !CanSee(user, viewerId))
        {
            throw new NotFoundException("Player not found.");
        }
        return ToResponse(user);
    }

    public async Task<PagedResponse<PlayerProfileResponse>> SearchAsync(Guid? viewerId, Guid? sportId, string? city, SkillLevel? level, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.PlayerProfiles, cancellationToken);
        (page, pageSize) = Paging.Normalize(page, pageSize, 50);

        var q = _db.Users.AsNoTracking().Where(u => u.Status == UserStatus.Active
            && (u.ProfileVisibility == ProfileVisibility.Public || (viewerId != null && u.ProfileVisibility == ProfileVisibility.Members))
            && u.Id != viewerId);
        if (sportId.HasValue) q = q.Where(u => u.SportProfiles.Any(p => p.SportId == sportId && (level == null || p.SkillLevel == level)));
        else if (level.HasValue) q = q.Where(u => u.SportProfiles.Any(p => p.SkillLevel == level));
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(u => u.City != null && EF.Functions.ILike(u.City, Text.EscapeLike(city.Trim()), "\\"));

        var total = await q.CountAsync(cancellationToken);
        var users = await q.Include(u => u.SportProfiles).ThenInclude(p => p.Sport)
            .OrderBy(u => u.FirstName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return PagedResponse<PlayerProfileResponse>.Create(users.Select(ToResponse).ToList(), page, pageSize, total);
    }

    private Task<User?> LoadAsync(System.Linq.Expressions.Expression<Func<User, bool>> predicate, CancellationToken cancellationToken) =>
        _db.Users.AsNoTracking().Include(u => u.SportProfiles).ThenInclude(p => p.Sport)
            .Where(u => u.Status == UserStatus.Active).FirstOrDefaultAsync(predicate, cancellationToken);

    private static bool CanSee(User user, Guid? viewerId) =>
        user.Id == viewerId || user.ProfileVisibility == ProfileVisibility.Public || (user.ProfileVisibility == ProfileVisibility.Members && viewerId.HasValue);

    private static PlayerProfileResponse ToResponse(User u) => new()
    {
        UserId = u.Id,
        Username = u.Username,
        // Never exposes email or phone.
        DisplayName = u.FirstName + (u.LastName is { Length: > 0 } last ? $" {last[0]}." : ""),
        ProfileImageUrl = u.ProfileImageUrl,
        City = u.City,
        Bio = u.Bio,
        Visibility = u.ProfileVisibility,
        Sports = u.SportProfiles.Select(p => new PlayerSportResponse
        {
            SportId = p.SportId, SportName = p.Sport.Name, SkillLevel = p.SkillLevel, PreferredPosition = p.PreferredPosition
        }).ToList()
    };
}

// =====================================================================
// Teams (story 65)
// =====================================================================

public class SaveTeamRequest
{
    [Required, StringLength(100)]
    public string? Name { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid? SportId { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    public TeamVisibility Visibility { get; set; } = TeamVisibility.Public;

    [Range(2, 100)]
    public int MaxMembers { get; set; } = 20;

    public bool IsOpenToRequests { get; set; } = true;
}

public class TeamMemberResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public TeamMemberRole Role { get; set; }
    public TeamMemberStatus Status { get; set; }
}

public class TeamResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public Guid SportId { get; set; }
    public string SportName { get; set; } = default!;
    public string? City { get; set; }
    public TeamVisibility Visibility { get; set; }
    public int MaxMembers { get; set; }
    public bool IsOpenToRequests { get; set; }
    public Guid CaptainUserId { get; set; }
    public int MemberCount { get; set; }

    /// <summary>The caller's own membership status, if any.</summary>
    public TeamMemberStatus? MyStatus { get; set; }

    /// <summary>Only for members (active members see each other; the captain also sees requests/invites).</summary>
    public List<TeamMemberResponse> Members { get; set; } = new();
}

public class InviteToTeamRequest
{
    [Required, StringLength(50)]
    public string? Username { get; set; }
}

public interface ITeamService
{
    Task<PagedResponse<TeamResponse>> SearchAsync(Guid? viewerId, Guid? sportId, string? city, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<List<TeamResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TeamResponse> GetAsync(Guid? viewerId, Guid teamId, CancellationToken cancellationToken = default);
    Task<TeamResponse> CreateAsync(Guid userId, SaveTeamRequest request, CancellationToken cancellationToken = default);
    Task<TeamResponse> UpdateAsync(Guid userId, Guid teamId, SaveTeamRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default);
    Task<TeamResponse> InviteAsync(Guid userId, Guid teamId, string username, CancellationToken cancellationToken = default);
    Task<TeamResponse> RequestToJoinAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default);
    Task<TeamResponse> RespondAsync(Guid userId, Guid teamId, bool accept, CancellationToken cancellationToken = default);
    Task<TeamResponse> DecideRequestAsync(Guid userId, Guid teamId, Guid memberUserId, bool approve, CancellationToken cancellationToken = default);
    Task<TeamResponse> RemoveMemberAsync(Guid userId, Guid teamId, Guid memberUserId, CancellationToken cancellationToken = default);
    Task LeaveAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default);
}

public class TeamService : ITeamService
{
    private readonly ApplicationDbContext _db;
    private readonly IFeatureFlags _flags;

    public TeamService(ApplicationDbContext db, IFeatureFlags flags)
    {
        _db = db;
        _flags = flags;
    }

    public async Task<PagedResponse<TeamResponse>> SearchAsync(Guid? viewerId, Guid? sportId, string? city, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        (page, pageSize) = Paging.Normalize(page, pageSize, 50);

        var q = _db.Teams.AsNoTracking().Where(t => t.Visibility == TeamVisibility.Public);
        if (sportId.HasValue) q = q.Where(t => t.SportId == sportId);
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(t => t.City != null && EF.Functions.ILike(t.City, Text.EscapeLike(city.Trim()), "\\"));
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(t => EF.Functions.ILike(t.Name, $"%{Text.EscapeLike(search.Trim())}%", "\\"));

        var total = await q.CountAsync(cancellationToken);
        var teams = await q.Include(t => t.Sport).Include(t => t.Members).ThenInclude(m => m.User)
            .OrderBy(t => t.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return PagedResponse<TeamResponse>.Create(teams.Select(t => ToResponse(t, viewerId)).ToList(), page, pageSize, total);
    }

    public async Task<List<TeamResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        var teams = await _db.Teams.AsNoTracking().Include(t => t.Sport).Include(t => t.Members).ThenInclude(m => m.User)
            .Where(t => t.Members.Any(m => m.UserId == userId && (m.Status == TeamMemberStatus.Active || m.Status == TeamMemberStatus.Invited || m.Status == TeamMemberStatus.Requested)))
            .OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return teams.Select(t => ToResponse(t, userId)).ToList();
    }

    public async Task<TeamResponse> GetAsync(Guid? viewerId, Guid teamId, CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        var team = await LoadAsync(teamId, tracked: false, cancellationToken);
        var isMember = viewerId.HasValue && team.Members.Any(m => m.UserId == viewerId && m.Status is TeamMemberStatus.Active or TeamMemberStatus.Invited);
        if (team.Visibility == TeamVisibility.Private && !isMember)
        {
            throw new NotFoundException("Team not found.");
        }
        return ToResponse(team, viewerId);
    }

    public async Task<TeamResponse> CreateAsync(Guid userId, SaveTeamRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        if (!await _db.Sports.AnyAsync(s => s.Id == request.SportId && s.IsActive, cancellationToken))
        {
            throw RequestValidationException.For("sportId", "Sport not found.");
        }
        if (await _db.Teams.CountAsync(t => t.CaptainUserId == userId, cancellationToken) >= 10)
        {
            throw new ConflictException("You can captain at most 10 teams.", "team_limit_reached");
        }

        var team = new Team { CaptainUserId = userId };
        team.Slug = Slugs.Create(request.Name!, team.Id, "team");
        Apply(team, request);
        team.Members.Add(new TeamMember { TeamId = team.Id, UserId = userId, Role = TeamMemberRole.Captain, Status = TeamMemberStatus.Active, RespondedAt = DateTime.UtcNow });

        _db.Teams.Add(team);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, team.Id, cancellationToken);
    }

    public async Task<TeamResponse> UpdateAsync(Guid userId, Guid teamId, SaveTeamRequest request, CancellationToken cancellationToken = default)
    {
        var team = await LoadCaptainTeamAsync(userId, teamId, cancellationToken);
        if (request.MaxMembers < team.Members.Count(m => m.Status == TeamMemberStatus.Active))
        {
            throw RequestValidationException.For("maxMembers", "Max members can't be lower than the current number of members.");
        }
        Apply(team, request);
        team.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, teamId, cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
    {
        var team = await LoadCaptainTeamAsync(userId, teamId, cancellationToken);
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TeamResponse> InviteAsync(Guid userId, Guid teamId, string username, CancellationToken cancellationToken = default)
    {
        var team = await LoadCaptainTeamAsync(userId, teamId, cancellationToken);
        var invitee = await _db.Users.FirstOrDefaultAsync(u => u.Username == username.Trim() && u.Status == UserStatus.Active, cancellationToken)
            ?? throw new NotFoundException("Player not found.");
        EnsureRoom(team);

        var member = team.Members.FirstOrDefault(m => m.UserId == invitee.Id);
        if (member is { Status: TeamMemberStatus.Active or TeamMemberStatus.Invited })
        {
            throw new ConflictException("This player is already in the team or invited.", "already_member");
        }

        if (member is { Status: TeamMemberStatus.Requested })
        {
            member.Status = TeamMemberStatus.Active;   // they asked already: inviting = approving
            member.RespondedAt = DateTime.UtcNow;
        }
        else if (member is null)
        {
            team.Members.Add(new TeamMember { TeamId = team.Id, UserId = invitee.Id, Status = TeamMemberStatus.Invited, InvitedByUserId = userId });
        }
        else
        {
            member.Status = TeamMemberStatus.Invited;
            member.InvitedByUserId = userId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, teamId, cancellationToken);
    }

    public async Task<TeamResponse> RequestToJoinAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        var team = await LoadAsync(teamId, tracked: true, cancellationToken);
        if (team.Visibility == TeamVisibility.Private || !team.IsOpenToRequests)
        {
            throw new ConflictException("This team doesn't accept join requests.", "requests_closed");
        }
        EnsureRoom(team);

        var member = team.Members.FirstOrDefault(m => m.UserId == userId);
        if (member is { Status: TeamMemberStatus.Active or TeamMemberStatus.Requested })
        {
            throw new ConflictException("You already asked to join or are a member.", "already_member");
        }
        if (member is { Status: TeamMemberStatus.Removed })
        {
            throw new ForbiddenException("You were removed from this team.", "removed_from_team");
        }

        if (member is { Status: TeamMemberStatus.Invited })
        {
            member.Status = TeamMemberStatus.Active;
            member.RespondedAt = DateTime.UtcNow;
        }
        else if (member is null)
        {
            team.Members.Add(new TeamMember { TeamId = team.Id, UserId = userId, Status = TeamMemberStatus.Requested });
        }
        else
        {
            member.Status = TeamMemberStatus.Requested;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, teamId, cancellationToken);
    }

    public async Task<TeamResponse> RespondAsync(Guid userId, Guid teamId, bool accept, CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        var team = await LoadAsync(teamId, tracked: true, cancellationToken);
        var member = team.Members.FirstOrDefault(m => m.UserId == userId && m.Status == TeamMemberStatus.Invited)
            ?? throw new NotFoundException("Invitation not found.");
        if (accept) EnsureRoom(team);

        member.Status = accept ? TeamMemberStatus.Active : TeamMemberStatus.Declined;
        member.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, teamId, cancellationToken);
    }

    public async Task<TeamResponse> DecideRequestAsync(Guid userId, Guid teamId, Guid memberUserId, bool approve, CancellationToken cancellationToken = default)
    {
        var team = await LoadCaptainTeamAsync(userId, teamId, cancellationToken);
        var member = team.Members.FirstOrDefault(m => m.UserId == memberUserId && m.Status == TeamMemberStatus.Requested)
            ?? throw new NotFoundException("Join request not found.");
        if (approve) EnsureRoom(team);

        member.Status = approve ? TeamMemberStatus.Active : TeamMemberStatus.Declined;
        member.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, teamId, cancellationToken);
    }

    public async Task<TeamResponse> RemoveMemberAsync(Guid userId, Guid teamId, Guid memberUserId, CancellationToken cancellationToken = default)
    {
        var team = await LoadCaptainTeamAsync(userId, teamId, cancellationToken);
        if (memberUserId == userId)
        {
            throw new BadRequestException("The captain can't remove themselves. Delete the team or hand it over first.");
        }
        var member = team.Members.FirstOrDefault(m => m.UserId == memberUserId) ?? throw new NotFoundException("Member not found.");
        member.Status = TeamMemberStatus.Removed;
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, teamId, cancellationToken);
    }

    public async Task LeaveAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        var team = await LoadAsync(teamId, tracked: true, cancellationToken);
        var member = team.Members.FirstOrDefault(m => m.UserId == userId && m.Status == TeamMemberStatus.Active)
            ?? throw new NotFoundException("You are not a member of this team.");

        if (team.CaptainUserId == userId)
        {
            // Hand the team to the longest-standing member; delete it when nobody is left.
            var next = team.Members.Where(m => m.UserId != userId && m.Status == TeamMemberStatus.Active).OrderBy(m => m.CreatedAt).FirstOrDefault();
            if (next is null)
            {
                _db.Teams.Remove(team);
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }
            next.Role = TeamMemberRole.Captain;
            team.CaptainUserId = next.UserId;
        }

        member.Status = TeamMemberStatus.Left;
        member.Role = TeamMemberRole.Member;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private Task EnsureEnabledAsync(CancellationToken cancellationToken) => _flags.EnsureEnabledAsync(FeatureFlagKeys.Teams, cancellationToken);

    private async Task<Team> LoadAsync(Guid teamId, bool tracked, CancellationToken cancellationToken)
    {
        var q = tracked ? _db.Teams : _db.Teams.AsNoTracking();
        return await q.Include(t => t.Sport).Include(t => t.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken) ?? throw new NotFoundException("Team not found.");
    }

    private async Task<Team> LoadCaptainTeamAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        await EnsureEnabledAsync(cancellationToken);
        var team = await LoadAsync(teamId, tracked: true, cancellationToken);
        if (team.CaptainUserId != userId)
        {
            throw new ForbiddenException("Only the team captain can do this.", "not_captain");
        }
        return team;
    }

    private static void EnsureRoom(Team team)
    {
        if (team.Members.Count(m => m.Status == TeamMemberStatus.Active) >= team.MaxMembers)
        {
            throw new ConflictException("This team is full.", "team_full");
        }
    }

    private static void Apply(Team team, SaveTeamRequest request)
    {
        team.Name = request.Name!.Trim();
        team.Description = Text.NullIfEmpty(request.Description);
        team.SportId = request.SportId!.Value;
        team.City = Text.NullIfEmpty(request.City);
        team.Visibility = request.Visibility;
        team.MaxMembers = request.MaxMembers;
        team.IsOpenToRequests = request.IsOpenToRequests;
    }

    private static TeamResponse ToResponse(Team t, Guid? viewerId)
    {
        var mine = viewerId.HasValue ? t.Members.FirstOrDefault(m => m.UserId == viewerId) : null;
        var isActiveMember = mine?.Status == TeamMemberStatus.Active;
        var isCaptain = t.CaptainUserId == viewerId;

        return new TeamResponse
        {
            Id = t.Id,
            Name = t.Name,
            Slug = t.Slug,
            Description = t.Description,
            SportId = t.SportId,
            SportName = t.Sport.Name,
            City = t.City,
            Visibility = t.Visibility,
            MaxMembers = t.MaxMembers,
            IsOpenToRequests = t.IsOpenToRequests,
            CaptainUserId = t.CaptainUserId,
            MemberCount = t.Members.Count(m => m.Status == TeamMemberStatus.Active),
            MyStatus = mine?.Status,
            Members = isActiveMember
                ? t.Members.Where(m => m.Status == TeamMemberStatus.Active
                                       || (isCaptain && m.Status is TeamMemberStatus.Invited or TeamMemberStatus.Requested))
                    .OrderBy(m => m.Role).ThenBy(m => m.User.FirstName)
                    .Select(m => new TeamMemberResponse
                    {
                        UserId = m.UserId,
                        Username = m.User.Username,
                        DisplayName = m.User.FirstName + (m.User.LastName is { Length: > 0 } last ? $" {last[0]}." : ""),
                        Role = m.Role,
                        Status = m.Status
                    }).ToList()
                : new List<TeamMemberResponse>()
        };
    }
}

// =====================================================================
// Phone verification (story 61)
// =====================================================================

public class SendCodeRequest
{
    [Required]
    [RegularExpression(@"^\+?[0-9][0-9 ()\-]{5,28}$", ErrorMessage = "Phone must be a valid phone number.")]
    public string? Phone { get; set; }
}

public class VerifyCodeRequest
{
    [Required]
    [RegularExpression(@"^\+?[0-9][0-9 ()\-]{5,28}$", ErrorMessage = "Phone must be a valid phone number.")]
    public string? Phone { get; set; }

    [Required, StringLength(6, MinimumLength = 6)]
    public string? Code { get; set; }
}

public class VerificationTokenResponse
{
    /// <summary>Send as "phoneVerificationToken" when creating the reservation (valid 30 minutes, once).</summary>
    public string VerificationToken { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
}

public interface IPhoneVerificationService
{
    Task SendCodeAsync(string phone, CancellationToken cancellationToken = default);
    Task<VerificationTokenResponse> VerifyAsync(string phone, string code, CancellationToken cancellationToken = default);
}

public class PhoneVerificationService : IPhoneVerificationService
{
    private const int CodeMinutes = 10;
    private const int MaxAttempts = 5;
    private const int MaxCodesPerHour = 3;

    private readonly ApplicationDbContext _db;
    private readonly IFeatureFlags _flags;
    private readonly INotificationService _notifications;

    public PhoneVerificationService(ApplicationDbContext db, IFeatureFlags flags, INotificationService notifications)
    {
        _db = db;
        _flags = flags;
        _notifications = notifications;
    }

    public async Task SendCodeAsync(string phone, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.SmsVerification, cancellationToken);
        var normalized = Text.NormalizePhone(phone)!;

        var hourAgo = DateTime.UtcNow.AddHours(-1);
        if (await _db.PhoneVerifications.CountAsync(v => v.Phone == normalized && v.CreatedAt >= hourAgo, cancellationToken) >= MaxCodesPerHour)
        {
            throw new TooManyRequestsException("Too many codes requested. Try again later.", "verification_rate_limited");
        }

        var code = SecureTokens.NumericCode();
        _db.PhoneVerifications.Add(new PhoneVerification
        {
            Phone = normalized,
            CodeHash = SecureTokens.Hash($"{normalized}:{code}"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(CodeMinutes)
        });
        _notifications.QueueSms(normalized, $"SportX code: {code} (valid {CodeMinutes} min)", "sms.verification_code");
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<VerificationTokenResponse> VerifyAsync(string phone, string code, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.SmsVerification, cancellationToken);
        var normalized = Text.NormalizePhone(phone)!;
        var now = DateTime.UtcNow;

        var verification = await _db.PhoneVerifications
            .Where(v => v.Phone == normalized && v.VerifiedAt == null && v.ExpiresAt > now)
            .OrderByDescending(v => v.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException("The code has expired. Request a new one.", "code_expired");

        verification.Attempts++;
        if (verification.Attempts > MaxAttempts || verification.CodeHash != SecureTokens.Hash($"{normalized}:{code.Trim()}"))
        {
            await _db.SaveChangesAsync(cancellationToken);
            throw RequestValidationException.For("code", "The code is incorrect.", "invalid_code");
        }

        var token = SecureTokens.Create();
        verification.VerifiedAt = now;
        verification.VerificationTokenHash = SecureTokens.Hash(token);
        await _db.SaveChangesAsync(cancellationToken);

        return new VerificationTokenResponse { VerificationToken = token, ExpiresAt = now.AddMinutes(30) };
    }
}

// =====================================================================
// Promotions (story 66) — admin-managed, always labelled
// =====================================================================

public class SavePromotionRequest
{
    [Required]
    public Guid? FacilityId { get; set; }

    [Required]
    public DateTime? StartsAt { get; set; }

    [Required]
    public DateTime? EndsAt { get; set; }

    [StringLength(50)]
    public string? Label { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PromotionResponse
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = default!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Label { get; set; } = default!;
    public bool IsActive { get; set; }
}

public interface IPromotionService
{
    Task<List<PromotionResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PromotionResponse> SaveAsync(Guid actorId, Guid? promotionId, SavePromotionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid actorId, Guid promotionId, CancellationToken cancellationToken = default);
}

public class PromotionService : IPromotionService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public PromotionService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public Task<List<PromotionResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _db.FacilityPromotions.AsNoTracking().OrderByDescending(p => p.StartsAt)
            .Select(p => new PromotionResponse
            {
                Id = p.Id, FacilityId = p.FacilityId, FacilityName = p.Facility.Name, StartsAt = p.StartsAt, EndsAt = p.EndsAt,
                Label = p.Label, IsActive = p.IsActive
            }).ToListAsync(cancellationToken);

    public async Task<PromotionResponse> SaveAsync(Guid actorId, Guid? promotionId, SavePromotionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.EndsAt <= request.StartsAt)
        {
            throw RequestValidationException.For("endsAt", "End must be after start.");
        }

        var facility = await _db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.FacilityId, cancellationToken)
            ?? throw RequestValidationException.For("facilityId", "Facility not found.");

        var promotion = promotionId.HasValue
            ? await _db.FacilityPromotions.FirstOrDefaultAsync(p => p.Id == promotionId, cancellationToken) ?? throw new NotFoundException("Promotion not found.")
            : new FacilityPromotion { CreatedByUserId = actorId };
        if (!promotionId.HasValue) _db.FacilityPromotions.Add(promotion);

        promotion.FacilityId = facility.Id;
        promotion.StartsAt = request.StartsAt!.Value.ToUniversalTime();
        promotion.EndsAt = request.EndsAt!.Value.ToUniversalTime();
        // Commercial labelling is mandatory; only the wording can change.
        promotion.Label = Text.NullIfEmpty(request.Label) ?? "Sponsored";
        promotion.IsActive = request.IsActive;

        _audit.Log(actorId, promotionId.HasValue ? "promotion.updated" : "promotion.created", "FacilityPromotion", promotion.Id.ToString(),
            $"{facility.Name}: {promotion.StartsAt:u}..{promotion.EndsAt:u}", businessId: facility.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return new PromotionResponse
        {
            Id = promotion.Id, FacilityId = facility.Id, FacilityName = facility.Name, StartsAt = promotion.StartsAt, EndsAt = promotion.EndsAt,
            Label = promotion.Label, IsActive = promotion.IsActive
        };
    }

    public async Task DeleteAsync(Guid actorId, Guid promotionId, CancellationToken cancellationToken = default)
    {
        var promotion = await _db.FacilityPromotions.FirstOrDefaultAsync(p => p.Id == promotionId, cancellationToken) ?? throw new NotFoundException("Promotion not found.");
        _db.FacilityPromotions.Remove(promotion);
        _audit.Log(actorId, "promotion.deleted", "FacilityPromotion", promotionId.ToString(), null);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
