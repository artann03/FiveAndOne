namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>
/// Weekly opening window in the facility's local time. Several rows per day are allowed (e.g. a lunch break).
/// ClosesAt earlier than OpensAt means the window runs past midnight.
/// </summary>
public class FacilityOperatingHours : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
}
