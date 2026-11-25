namespace FiveAndOne.Domain.Enums;

// NOTE: the EX_Bookings_NoOverlap constraint in the MultiSportSchema migration hard-codes
// Pending (1) and Confirmed (2). Don't renumber these.
public enum BookingStatus
{
    Pending = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5,
    NoShow = 6,
    Expired = 7
}

public enum PaymentStatus
{
    Unpaid = 1,
    PayAtVenue = 2,
    Paid = 3,
    Refunded = 4
}
