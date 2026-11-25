using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Bookings;

public class Booking : BaseEntity
{
    // Short human-friendly reference shown to customers and staff, e.g. "FO-7K3M9Q".
    public string BookingNumber { get; set; } = default!;

    public Guid CustomerUserId { get; set; }
    public User Customer { get; set; } = default!;

    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public Guid SportId { get; set; }
    public Sport Sport { get; set; } = default!;

    // Set only for Session-mode facilities.
    public Guid? SessionId { get; set; }
    public FacilitySession? Session { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    // EndsAt + the facility's buffer. The no-overlap constraint uses [StartsAt, BlockedUntil).
    public DateTime BlockedUntil { get; set; }

    public int Spots { get; set; } = 1;

    // Price snapshot taken at booking time; never recalculated.
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public ConfirmationMode ConfirmationMode { get; set; }

    // When a Pending booking stops holding the slot if nobody confirms it.
    public DateTime? ExpiresAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }

    public string? CustomerNotes { get; set; }

    // Client-supplied key so a retried request doesn't create a second booking.
    public string? IdempotencyKey { get; set; }

    // Postgres xmin optimistic concurrency.
    public uint Version { get; set; }

    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
}
