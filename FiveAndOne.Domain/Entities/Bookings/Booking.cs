using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Bookings;

/// <summary>
/// A reservation of one facility for one sport and time range. Covers SportX bookings made by a
/// customer or a guest, and external reservations a business records (phone, walk-in, ...).
/// </summary>
public class Booking : BaseEntity
{
    // Short human-friendly reference shown to customers and staff, e.g. "SX-7K3M9Q".
    public string BookingNumber { get; set; } = default!;

    public BookingSource Source { get; set; } = BookingSource.SportX;
    public ExternalChannel? ExternalChannel { get; set; }

    // Denormalised from the facility so business queries and data isolation stay simple.
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public Guid SportId { get; set; }
    public Sport Sport { get; set; } = default!;

    // Registered customer. Null for guests and external reservations.
    public Guid? CustomerUserId { get; set; }
    public User? Customer { get; set; }

    public bool IsGuest { get; set; }

    // Contact snapshot (guest details, external customer, or the customer's details at booking time).
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    // Guests access their reservation with a secret link; only its SHA-256 is stored.
    public string? GuestAccessTokenHash { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    // EndsAt + buffer. The no-overlap constraint uses [StartsAt, BlockedUntil).
    public DateTime BlockedUntil { get; set; }

    // Price snapshot taken at booking time; never recalculated.
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.NotRequired;
    // Only for Pending (online payment): when the slot is released if nobody pays.
    public DateTime? ExpiresAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }

    public string? CustomerNotes { get; set; }
    // Visible to the business only.
    public string? InternalNotes { get; set; }

    // Client-supplied key so a retried request doesn't create a second booking.
    public string? IdempotencyKey { get; set; }

    // Who created the record (customer, guest = null, or business staff for external ones).
    public Guid? CreatedByUserId { get; set; }

    // Privacy-conscious abuse controls for guest bookings: hashes only, never raw values.
    public string? DeviceHash { get; set; }
    public string? IpHash { get; set; }

    // Postgres xmin optimistic concurrency.
    public uint Version { get; set; }

    public ICollection<BookingHistory> History { get; set; } = new List<BookingHistory>();

    public bool HoldsSlot => Status is BookingStatus.Pending or BookingStatus.Confirmed;
    public int DurationMinutes => (int)Math.Round((EndsAt - StartsAt).TotalMinutes);
}

/// <summary>Audit trail of a reservation: creation, status changes and reschedules.</summary>
public class BookingHistory : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = default!;

    public BookingEventType EventType { get; set; }

    public BookingStatus? FromStatus { get; set; }
    public BookingStatus? ToStatus { get; set; }

    // For reschedules.
    public DateTime? OldStartsAt { get; set; }
    public DateTime? OldEndsAt { get; set; }
    public DateTime? NewStartsAt { get; set; }
    public DateTime? NewEndsAt { get; set; }

    public Guid? ChangedByUserId { get; set; }       // null = system or guest
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
