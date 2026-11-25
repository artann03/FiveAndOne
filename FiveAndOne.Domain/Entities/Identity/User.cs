using FiveAndOne.Domain.Entities.Activities;
using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Identity;

public class User : BaseEntity
{
    public string FirstName { get; set; } = default!;
    public string? LastName { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.User;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime? EmailConfirmedAt { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? City { get; set; }
    public string? Bio { get; set; }

    // Set when the user "deletes" their account (Status = Deactivated). Nothing is actually deleted.
    public DateTime? DeactivatedAt { get; set; }

    // A deactivated account can be brought back for this many days; after that it stays closed for good.
    public const int ReactivationWindowDays = 60;

    // Not mapped (no setter): convenience check used by auth.
    public bool IsActive => Status == UserStatus.Active;

    public bool CanBeReactivated(DateTime utcNow) =>
        Status == UserStatus.Deactivated
        && DeactivatedAt.HasValue
        && utcNow < DeactivatedAt.Value.AddDays(ReactivationWindowDays);

    // Only for Business accounts; null until the business saves its profile.
    public BusinessProfile? BusinessProfile { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserSportProfile> SportProfiles { get; set; } = new List<UserSportProfile>();
    public ICollection<Facility> OwnedFacilities { get; set; } = new List<Facility>();
    public ICollection<FacilityMember> FacilityMemberships { get; set; } = new List<FacilityMember>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Activity> OrganizedActivities { get; set; } = new List<Activity>();
    public ICollection<ActivityParticipant> ActivityParticipations { get; set; } = new List<ActivityParticipant>();
}
