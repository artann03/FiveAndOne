namespace FiveAndOne.Application.Dtos.Availability;

/// <summary>One bookable time button (story 18).</summary>
public class SlotResponse
{
    /// <summary>UTC instant (send this back when booking).</summary>
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    /// <summary>Facility local wall-clock time, for display ("2026-11-26T18:00:00", no offset).</summary>
    public DateTime LocalStartsAt { get; set; }
    public DateTime LocalEndsAt { get; set; }

    public bool Available { get; set; }

    /// <summary>Why it's unavailable: taken, too_soon, too_far_ahead. Null when available.</summary>
    public string? UnavailableReason { get; set; }

    public decimal? Price { get; set; }
    public string? Currency { get; set; }
}

public class DaySlotsResponse
{
    public Guid FacilityId { get; set; }
    public Guid SportId { get; set; }
    public DateOnly Date { get; set; }
    public string TimeZone { get; set; } = default!;
    public int DurationMinutes { get; set; }
    public int[] DurationOptions { get; set; } = Array.Empty<int>();
    public int BufferMinutes { get; set; }

    /// <summary>True when the facility doesn't open on this date (closed day or closure).</summary>
    public bool IsClosed { get; set; }

    /// <summary>True when no price rule matches; slots exist but can't be booked yet.</summary>
    public bool PricingMissing { get; set; }

    public List<SlotResponse> Slots { get; set; } = new();
}

/// <summary>One day in the availability calendar strip.</summary>
public class CalendarDayResponse
{
    public DateOnly Date { get; set; }
    public bool IsOpen { get; set; }
    public int AvailableSlots { get; set; }
}

public class CalendarResponse
{
    public Guid FacilityId { get; set; }
    public Guid SportId { get; set; }
    public int DurationMinutes { get; set; }
    public string TimeZone { get; set; } = default!;
    public List<CalendarDayResponse> Days { get; set; } = new();
}
