namespace FiveAndOne.Domain.Enums;

// Draft -> Published <-> Hidden. Only Published is visible to the public.
// Suspended is set by an admin; the owner can't publish until an admin lifts it.
public enum FacilityStatus
{
    Draft = 1,
    Published = 2,
    Hidden = 3,
    Suspended = 4
}

public enum FacilityType
{
    Court = 1,
    Pitch = 2,
    Field = 3,
    Lane = 4,
    Course = 5,
    Range = 6,
    Pool = 7,
    Room = 8,
    Other = 9
}

public enum PriceType
{
    PerHour = 1,          // Amount x (duration / 60)
    PerReservation = 2    // Amount for the whole reservation, whatever its length
}

/// <summary>Reason picked when a business manually blocks time (story 19).</summary>
public enum BlockReason
{
    Maintenance = 1,
    PrivateUse = 2,
    Event = 3,
    Other = 4
}

/// <summary>What to do with reservations that collide with a new closure or block.</summary>
public enum ConflictResolution
{
    /// <summary>Cancel the colliding reservations (as "cancelled by business").</summary>
    CancelReservations = 1,

    /// <summary>Save anyway and keep the reservations; the business handles them itself.</summary>
    KeepReservations = 2
}
