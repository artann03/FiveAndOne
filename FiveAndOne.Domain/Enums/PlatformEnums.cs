namespace FiveAndOne.Domain.Enums;

public enum NotificationChannel
{
    Email = 1,
    Sms = 2
}

public enum NotificationStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}

public enum ReviewStatus
{
    Published = 1,
    Hidden = 2,
    Removed = 3
}

public enum ReportTargetType
{
    Review = 1,
    User = 2,
    Team = 3
}

public enum ReportStatus
{
    Open = 1,
    Resolved = 2,
    Dismissed = 3
}

public enum ProfileVisibility
{
    Private = 1,
    /// <summary>Visible to signed-in users only.</summary>
    Members = 2,
    Public = 3
}

public enum TeamVisibility
{
    Public = 1,
    Private = 2
}

public enum TeamMemberRole
{
    Captain = 1,
    Member = 2
}

public enum TeamMemberStatus
{
    Invited = 1,
    Requested = 2,
    Active = 3,
    Declined = 4,
    Left = 5,
    Removed = 6
}
