using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>
/// A price for reservations on a facility (story 13). Every filter is optional:
/// a rule with no sport, duration, day or time is the facility's default price.
/// When several rules match, the most specific one wins (sport, then duration, then day, then time),
/// and Priority breaks ties.
/// </summary>
public class FacilityPriceRule : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public Guid? SportId { get; set; }
    public int? DurationMinutes { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }

    // Local time window the reservation must start in. Both null = all day.
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }

    public PriceType PriceType { get; set; } = PriceType.PerReservation;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public int Priority { get; set; }
    public string? Label { get; set; }
}
