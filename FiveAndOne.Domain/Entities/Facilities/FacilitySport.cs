using FiveAndOne.Domain.Entities.Sports;

namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>Join table Facility *-* Sport (a hall can host basketball and volleyball).</summary>
public class FacilitySport
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public Guid SportId { get; set; }
    public Sport Sport { get; set; } = default!;
}
