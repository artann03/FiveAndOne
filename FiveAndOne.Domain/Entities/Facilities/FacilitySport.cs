using FiveAndOne.Domain.Entities.Sports;

namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>
/// A sport offered on a facility (a hall can host basketball and volleyball), with optional
/// sport-specific reservation rules. Null overrides fall back to the facility's settings.
/// Sport-specific prices and hours live in FacilityPriceRule / FacilityOperatingHours (SportId).
/// </summary>
public class FacilitySport
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public Guid SportId { get; set; }
    public Sport Sport { get; set; } = default!;

    public int[]? DurationOptions { get; set; }
    public int? BufferMinutes { get; set; }
}
