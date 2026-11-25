using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Bookings;

/// <summary>Audit trail: one row per booking status change.</summary>
public class BookingStatusHistory : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = default!;

    public BookingStatus? FromStatus { get; set; }   // null for the initial status
    public BookingStatus ToStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }       // null = system (e.g. expiry job)
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
