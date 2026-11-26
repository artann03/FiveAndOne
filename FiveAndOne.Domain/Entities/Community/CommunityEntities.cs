using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Community;

/// <summary>A rating and review of a facility, allowed once per completed reservation (story 63).</summary>
public class Review : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public int Rating { get; set; }               // 1-5
    public string? Comment { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Published;
    public string? ModerationNote { get; set; }

    // Optional public reply from the business.
    public string? BusinessReply { get; set; }
    public DateTime? BusinessRepliedAt { get; set; }
}

/// <summary>A user report about a review, user or team, for moderation.</summary>
public class ContentReport : BaseEntity
{
    public ReportTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public Guid ReporterUserId { get; set; }
    public string Reason { get; set; } = default!;
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}

/// <summary>A team of players for one sport (story 65).</summary>
public class Team : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }

    public Guid SportId { get; set; }
    public Sport Sport { get; set; } = default!;

    public string? City { get; set; }
    public TeamVisibility Visibility { get; set; } = TeamVisibility.Public;
    public int MaxMembers { get; set; } = 20;
    public bool IsOpenToRequests { get; set; } = true;

    public Guid CaptainUserId { get; set; }
    public User Captain { get; set; } = default!;

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}

public class TeamMember : BaseEntity
{
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;
    public TeamMemberStatus Status { get; set; }
    public Guid? InvitedByUserId { get; set; }
    public DateTime? RespondedAt { get; set; }
}

/// <summary>A paid/featured placement of a facility in discovery, always labelled "Sponsored" (story 66).</summary>
public class FacilityPromotion : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Label { get; set; } = "Sponsored";
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
}
