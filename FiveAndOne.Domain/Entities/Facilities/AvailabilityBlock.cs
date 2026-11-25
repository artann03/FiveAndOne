using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>
/// An exception to the normal schedule: closure, maintenance, holiday, manual block,
/// or a special opening outside normal hours.
/// </summary>
public class AvailabilityBlock : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public AvailabilityBlockType Type { get; set; }
    public string? Reason { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
