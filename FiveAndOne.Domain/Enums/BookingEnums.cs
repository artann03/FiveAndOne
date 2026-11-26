namespace FiveAndOne.Domain.Enums;

// NOTE: the EX_Bookings_NoOverlap constraint hard-codes Pending (1) and Confirmed (2)
// as the statuses that hold a slot. Don't renumber these.
public enum BookingStatus
{
    /// <summary>Waiting for an online payment (only when customer payments are enabled).</summary>
    Pending = 1,
    Confirmed = 2,
    CancelledByCustomer = 3,
    CancelledByBusiness = 4,
    Completed = 5,
    NoShow = 6,
    /// <summary>A pending reservation whose payment window ran out.</summary>
    Expired = 7
}

public enum BookingSource
{
    SportX = 1,
    External = 2
}

/// <summary>Where an external reservation came from (story 20).</summary>
public enum ExternalChannel
{
    Phone = 1,
    WhatsApp = 2,
    SocialMedia = 3,
    WalkIn = 4,
    Other = 5
}

public enum PaymentStatus
{
    /// <summary>MVP: reservations are paid at the venue, nothing is charged online.</summary>
    NotRequired = 1,
    Pending = 2,
    Paid = 3,
    Refunded = 4,
    Failed = 5
}

public enum BookingEventType
{
    Created = 1,
    StatusChanged = 2,
    Rescheduled = 3,
    Updated = 4
}
