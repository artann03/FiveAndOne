using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>Time a business blocked manually (maintenance, private use, event...). Story 19.</summary>
public class AvailabilityBlock : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    // Null = the whole facility. Set = only this sport is blocked.
    public Guid? SportId { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public BlockReason Reason { get; set; }
    public string? InternalNote { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
