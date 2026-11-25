namespace FiveAndOne.Domain.Entities.Facilities;

public class FacilityImage : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public string Url { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsCover { get; set; }
}
