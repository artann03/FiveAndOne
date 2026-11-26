using FiveAndOne.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Facilities;

// ---------------- Sports (story 09) ----------------

public class FacilitySportSettings
{
    [Required]
    public Guid SportId { get; set; }

    /// <summary>Own bookable durations for this sport; null = use the facility's.</summary>
    public int[]? DurationOptions { get; set; }

    /// <summary>Own buffer for this sport; null = use the facility's.</summary>
    [Range(0, 240, ErrorMessage = "Buffer must be between 0 and 240 minutes.")]
    public int? BufferMinutes { get; set; }
}

public class SaveFacilitySportsRequest
{
    /// <summary>"Save sports": the full list of sports offered, with optional per-sport rules.</summary>
    [Required]
    public List<FacilitySportSettings> Sports { get; set; } = new();
}

// ---------------- Reservation rules (story 16) + cancellation (story 33) ----------------

public class FacilityRulesRequest
{
    /// <summary>Bookable lengths in minutes, e.g. [30, 60, 90, 120].</summary>
    [Required]
    public int[] DurationOptions { get; set; } = { 60 };

    /// <summary>Minutes kept free after every reservation. Default 0.</summary>
    [Range(0, 240, ErrorMessage = "Buffer must be between 0 and 240 minutes.")]
    public int BufferMinutes { get; set; }

    /// <summary>Slots start every N minutes from opening time (5, 10, 15, 20, 30 or 60).</summary>
    public int SlotStepMinutes { get; set; } = 30;

    /// <summary>How long before the start a reservation must be made (minutes).</summary>
    [Range(0, 10080, ErrorMessage = "Minimum notice must be between 0 and 10080 minutes (7 days).")]
    public int MinAdvanceMinutes { get; set; }

    /// <summary>How many days ahead customers can book.</summary>
    [Range(1, 365, ErrorMessage = "Booking window must be between 1 and 365 days.")]
    public int MaxAdvanceDays { get; set; } = 30;

    /// <summary>Hours before the start until which customers can cancel or change. Null = business default.</summary>
    [Range(0, 720, ErrorMessage = "Cancellation deadline must be between 0 and 720 hours.")]
    public int? CancellationCutoffHours { get; set; }

    [StringLength(1000)]
    public string? CancellationPolicyNote { get; set; }
}

// ---------------- Weekly hours (story 14) ----------------

public class TimeWindowDto
{
    /// <summary>Local time, "HH:mm".</summary>
    [Required]
    public TimeOnly? OpensAt { get; set; }

    /// <summary>Local time, "HH:mm". Earlier than (or equal to) OpensAt = closes after midnight.</summary>
    [Required]
    public TimeOnly? ClosesAt { get; set; }
}

public class DayHoursDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsClosed { get; set; }
    public List<TimeWindowDto> Windows { get; set; } = new();
}

public class WeeklyHoursRequest
{
    /// <summary>Null = the facility's hours. Set = hours that apply only to this sport.</summary>
    public Guid? SportId { get; set; }

    /// <summary>One entry per day; days that are missing are treated as closed.</summary>
    [Required]
    public List<DayHoursDto> Days { get; set; } = new();
}

public class WeeklyHoursResponse
{
    public Guid? SportId { get; set; }
    public List<DayHoursDto> Days { get; set; } = new();

    /// <summary>Readable summary, e.g. "Mon–Fri 08:00–23:00, Sat 09:00–22:00, Sun closed".</summary>
    public string Summary { get; set; } = default!;
}

// ---------------- Exceptions / closures (story 15) ----------------

public class ScheduleExceptionRequest
{
    [Required]
    public DateOnly? StartDate { get; set; }

    /// <summary>Inclusive. Defaults to StartDate.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>"Close facility": no reservations on these dates.</summary>
    public bool IsClosed { get; set; }

    /// <summary>Special hours for each date (when not closed).</summary>
    public TimeOnly? OpensAt { get; set; }
    public TimeOnly? ClosesAt { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    /// <summary>Required when existing reservations conflict: CancelReservations or KeepReservations.</summary>
    public ConflictResolution? ConflictResolution { get; set; }
}

public class ScheduleExceptionResponse
{
    public Guid Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsClosed { get; set; }
    public TimeOnly? OpensAt { get; set; }
    public TimeOnly? ClosesAt { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ---------------- Pricing (story 13) ----------------

public class PriceRuleDto
{
    public Guid? Id { get; set; }

    /// <summary>Null = all sports on this facility.</summary>
    public Guid? SportId { get; set; }

    /// <summary>Null = any duration.</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>Null = every day.</summary>
    public DayOfWeek? DayOfWeek { get; set; }

    /// <summary>Reservation start window (local). Both null = all day.</summary>
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }

    /// <summary>PerReservation (fixed price for the booking) or PerHour.</summary>
    public PriceType PriceType { get; set; } = PriceType.PerReservation;

    [Range(0, 100000, ErrorMessage = "Price must be between 0 and 100000.")]
    public decimal Amount { get; set; }

    /// <summary>EUR (only currency supported for now).</summary>
    public string? Currency { get; set; }

    public int Priority { get; set; }

    [StringLength(100)]
    public string? Label { get; set; }
}

public class SavePricingRequest
{
    /// <summary>"Save pricing": replaces all price rules of the facility.</summary>
    [Required]
    public List<PriceRuleDto> Rules { get; set; } = new();
}

public class PricePreviewResponse
{
    public Guid SportId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public Guid? AppliedRuleId { get; set; }
    public string? AppliedRuleLabel { get; set; }
}

// ---------------- Manual blocks (story 19) ----------------

public class CreateBlockRequest
{
    /// <summary>Null = block the whole facility; set = only this sport.</summary>
    public Guid? SportId { get; set; }

    [Required]
    public DateOnly? Date { get; set; }

    [Required]
    public TimeOnly? StartTime { get; set; }

    /// <summary>End time (local). Earlier than StartTime = ends the next day.</summary>
    [Required]
    public TimeOnly? EndTime { get; set; }

    public BlockReason Reason { get; set; } = BlockReason.Other;

    [StringLength(500)]
    public string? InternalNote { get; set; }

    /// <summary>Required when existing reservations conflict: CancelReservations or KeepReservations.</summary>
    public ConflictResolution? ConflictResolution { get; set; }
}

public class BlockResponse
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public Guid? SportId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime LocalStartsAt { get; set; }
    public DateTime LocalEndsAt { get; set; }
    public BlockReason Reason { get; set; }
    public string? InternalNote { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A reservation that collides with a closure/block/external reservation being saved.</summary>
public class ReservationConflictResponse
{
    public Guid BookingId { get; set; }
    public string BookingNumber { get; set; } = default!;
    public BookingSource Source { get; set; }
    public string? ContactName { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime LocalStartsAt { get; set; }
    public BookingStatus Status { get; set; }
}

/// <summary>Everything on the facility's configuration screens in one call.</summary>
public class FacilityConfigurationResponse
{
    public Guid FacilityId { get; set; }
    public string TimeZone { get; set; } = default!;
    public FacilityRulesRequest Rules { get; set; } = new();
    public int EffectiveCancellationCutoffHours { get; set; }
    public List<FacilitySportResponse> Sports { get; set; } = new();
    public WeeklyHoursResponse FacilityHours { get; set; } = new();
    public List<WeeklyHoursResponse> SportHours { get; set; } = new();
    public List<PriceRuleDto> PriceRules { get; set; } = new();
    public List<ScheduleExceptionResponse> UpcomingExceptions { get; set; } = new();
}
