using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>Facility staff: gives a user permissions on one specific facility.</summary>
public class FacilityMember : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public FacilityPermissions Permissions { get; set; } = FacilityPermissions.ManageBookings;
    public bool IsActive { get; set; } = true;
}
