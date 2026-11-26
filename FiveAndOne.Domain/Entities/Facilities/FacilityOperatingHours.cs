namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>
/// Weekly opening window in the facility's local time. Several rows per day are allowed (e.g. a lunch break).
/// ClosesAt at or before OpensAt means the window runs past midnight (e.g. 18:00-01:00).
/// A day without rows is closed.
/// SportId null = facility hours. If a sport has its own rows, those replace the facility hours for that sport.
/// </summary>
public class FacilityOperatingHours : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public Guid? SportId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
}

/// <summary>
/// Date-specific override of the weekly hours (story 15): a closure (holiday, renovation) or special hours.
/// Takes precedence over FacilityOperatingHours for every date between StartDate and EndDate (inclusive).
/// </summary>
public class FacilityScheduleException : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public bool IsClosed { get; set; }

    // Special hours for each date in the range (required when IsClosed is false).
    public TimeOnly? OpensAt { get; set; }
    public TimeOnly? ClosesAt { get; set; }

    public string? Reason { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
