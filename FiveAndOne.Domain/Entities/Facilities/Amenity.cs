namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>Lookup list managed by admins: Parking, Showers, Changing rooms, Lighting, Café, Equipment rental, ...</summary>
public class Amenity : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Icon { get; set; }

    public ICollection<FacilityAmenity> Facilities { get; set; } = new List<FacilityAmenity>();
}

/// <summary>Join table Facility *-* Amenity.</summary>
public class FacilityAmenity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public Guid AmenityId { get; set; }
    public Amenity Amenity { get; set; } = default!;
}
