namespace FiveAndOne.Domain.Enums;

public enum ActivityVisibility
{
    Public = 1,
    Private = 2
}

public enum ActivityJoinPolicy
{
    Open = 1,           // anyone can join while there is space
    RequestToJoin = 2,  // organizer approves requests
    InviteOnly = 3      // only invited users
}

public enum ActivityStatus
{
    Open = 1,
    Full = 2,
    Cancelled = 3,
    Completed = 4
}

public enum ActivityParticipantStatus
{
    Invited = 1,
    Requested = 2,
    Joined = 3,
    Declined = 4,
    Left = 5,
    Removed = 6
}

public enum AttendanceStatus
{
    Unknown = 1,
    Attended = 2,
    NoShow = 3
}
