using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>Price for a time window (local time). The matching rule with the highest Priority wins.</summary>
public class FacilityPriceRule : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public DayOfWeek? DayOfWeek { get; set; }   // null = every day
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public PriceType PriceType { get; set; } = PriceType.PerHour;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public int Priority { get; set; }

    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
}
