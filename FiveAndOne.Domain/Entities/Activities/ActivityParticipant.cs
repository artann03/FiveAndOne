using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Activities;

/// <summary>
/// One row per user per activity. Covers invitations (Invited), join requests (Requested),
/// and actual participation (Joined), plus attendance after the activity.
/// </summary>
public class ActivityParticipant : BaseEntity
{
    public Guid ActivityId { get; set; }
    public Activity Activity { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public ActivityParticipantStatus Status { get; set; }
    public bool IsOrganizer { get; set; }

    public Guid? InvitedByUserId { get; set; }

    // Optional team split (1, 2, ...) for team sports.
    public short? TeamNumber { get; set; }

    public AttendanceStatus Attendance { get; set; } = AttendanceStatus.Unknown;
    public DateTime? RespondedAt { get; set; }
}
