using FiveAndOne.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Bookings;

public class GuestDetailsDto
{
    [Required(ErrorMessage = "Your name is required.")]
    [StringLength(150, ErrorMessage = "Name can be at most 150 characters.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^\+?[0-9][0-9 ()\-]{5,28}$", ErrorMessage = "Phone must be a valid phone number, e.g. +383 44 123 456.")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256)]
    public string? Email { get; set; }
}

/// <summary>
/// Create a reservation (story 30). Signed-in customers send only the slot; guests (no token) must add "guest".
/// Send an "Idempotency-Key" header (any unique string per attempt) so a retried submit can't book twice.
/// </summary>
public class CreateReservationRequest
{
    [Required]
    public Guid? FacilityId { get; set; }

    [Required]
    public Guid? SportId { get; set; }

    /// <summary>The slot's UTC start, exactly as returned by the availability endpoint.</summary>
    [Required]
    public DateTime? StartsAt { get; set; }

    /// <summary>One of the facility's duration options for this sport.</summary>
    [Required, Range(5, 1440)]
    public int? DurationMinutes { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>Required when booking without an account.</summary>
    public GuestDetailsDto? Guest { get; set; }

    /// <summary>Only when the API answers "phone_verification_required" (post-MVP SMS verification).</summary>
    public string? PhoneVerificationToken { get; set; }
}

/// <summary>Change date/time (story 34): same facility, sport and duration.</summary>
public class RescheduleReservationRequest
{
    [Required]
    public DateTime? StartsAt { get; set; }

    /// <summary>Business-side changes may also change the length. Customers keep the original duration.</summary>
    [Range(5, 1440)]
    public int? DurationMinutes { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

public class CancellationPolicyResponse
{
    public int CutoffHours { get; set; }
    public string? Note { get; set; }

    /// <summary>Until when the customer can cancel or change (UTC).</summary>
    public DateTime CancellableUntil { get; set; }
    public bool CanCancel { get; set; }
    public bool CanModify { get; set; }
}

public class ReservationFacilityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? CoverImageUrl { get; set; }
}

/// <summary>A reservation as the customer (or guest) sees it.</summary>
public class ReservationResponse
{
    public Guid Id { get; set; }

    /// <summary>Human-friendly reference, e.g. "SX-7K3M9Q".</summary>
    public string BookingNumber { get; set; } = default!;
    public BookingStatus Status { get; set; }
    public BookingSource Source { get; set; }
    public PaymentStatus PaymentStatus { get; set; }

    public ReservationFacilityDto Facility { get; set; } = default!;
    public string BusinessName { get; set; } = default!;
    public Guid SportId { get; set; }
    public string SportName { get; set; } = default!;

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime LocalStartsAt { get; set; }
    public DateTime LocalEndsAt { get; set; }
    public string TimeZone { get; set; } = default!;
    public int DurationMinutes { get; set; }

    public decimal Price { get; set; }
    public string Currency { get; set; } = default!;

    public bool IsGuest { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public CancellationPolicyResponse CancellationPolicy { get; set; } = new();

    /// <summary>Only in the create response for guests: the secret to retrieve the reservation later. Save it.</summary>
    public string? GuestAccessToken { get; set; }
    public string? GuestAccessUrl { get; set; }

    /// <summary>Only when online payment is required (post-MVP): where to pay before ExpiresAt.</summary>
    public string? CheckoutUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public bool CanReview { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BookingHistoryResponse
{
    public BookingEventType EventType { get; set; }
    public BookingStatus? FromStatus { get; set; }
    public BookingStatus? ToStatus { get; set; }
    public DateTime? OldStartsAt { get; set; }
    public DateTime? NewStartsAt { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
}

/// <summary>A reservation as the business sees it (stories 40, 46).</summary>
public class BusinessReservationResponse : ReservationResponse
{
    public ExternalChannel? ExternalChannel { get; set; }
    public string? InternalNotes { get; set; }
    public Guid? CustomerUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public List<BookingHistoryResponse> History { get; set; } = new();
}

/// <summary>"Add external reservation" (story 20).</summary>
public class ExternalReservationRequest
{
    [Required]
    public Guid? FacilityId { get; set; }

    [Required]
    public Guid? SportId { get; set; }

    [Required]
    public DateOnly? Date { get; set; }

    [Required]
    public TimeOnly? StartTime { get; set; }

    /// <summary>Earlier than StartTime = ends the next day.</summary>
    [Required]
    public TimeOnly? EndTime { get; set; }

    public ExternalChannel Channel { get; set; } = ExternalChannel.Phone;

    [StringLength(150)]
    public string? CustomerName { get; set; }

    [StringLength(30)]
    public string? CustomerPhone { get; set; }

    [EmailAddress, StringLength(256)]
    public string? CustomerEmail { get; set; }

    /// <summary>Optional price agreed with the customer; default = the facility's price for that time.</summary>
    [Range(0, 100000)]
    public decimal? Price { get; set; }

    [StringLength(1000)]
    public string? InternalNotes { get; set; }
}

public class ReservationNotesRequest
{
    [StringLength(1000)]
    public string? InternalNotes { get; set; }
}

public class BusinessReservationQuery
{
    public Guid? FacilityId { get; set; }
    public Guid? SportId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public BookingStatus? Status { get; set; }
    public BookingSource? Source { get; set; }

    /// <summary>Reference, customer name, phone or email.</summary>
    public string? Search { get; set; }

    /// <summary>upcoming (default), past or all.</summary>
    public string? Scope { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
