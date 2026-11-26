using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Businesses;

/// <summary>
/// A sports business on SportX. Facilities, reservations and subscriptions belong to the business,
/// people get access through <see cref="BusinessMember"/> (owner or staff).
/// </summary>
public class Business : BaseEntity
{
    // The account that registered the business (also has an Owner membership row).
    public Guid OwnerUserId { get; set; }
    public User Owner { get; set; } = default!;

    public string BusinessName { get; set; } = default!;

    // Official registration identifier (Kosovo ARBK number). Stored for verification only,
    // never returned by public or profile endpoints.
    public string? RegistrationNumber { get; set; }

    public string AddressLine { get; set; } = default!;
    public string City { get; set; } = default!;
    // ISO 3166-1 alpha-2, upper case (Kosovo = "XK").
    public string Country { get; set; } = "XK";
    public string Phone { get; set; } = default!;
    public string Email { get; set; } = default!;
    // Language code used for communication with the business: sq, en, tr or sr.
    public string PreferredLanguage { get; set; } = "sq";

    public BusinessStatus Status { get; set; } = BusinessStatus.Active;
    public DateTime? SuspendedAt { get; set; }
    public string? SuspensionReason { get; set; }

    // Data retention (story 46): set when the business becomes Inactive after the grace period.
    public DateTime? InactiveSince { get; set; }
    public DateTime? RetentionUntil { get; set; }
    public DateTime? AnonymizedAt { get; set; }

    // Default cancellation policy for its facilities (a facility can override it).
    public int CancellationCutoffHours { get; set; } = 24;
    public string? CancellationPolicyNote { get; set; }

    public ICollection<BusinessMember> Members { get; set; } = new List<BusinessMember>();
    public ICollection<Facility> Facilities { get; set; } = new List<Facility>();
    public ICollection<Venue> Venues { get; set; } = new List<Venue>();
    public ICollection<BusinessSubscription> Subscriptions { get; set; } = new List<BusinessSubscription>();
}

/// <summary>A person's access to a business: the owner, or a staff member with chosen permissions.</summary>
public class BusinessMember : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public BusinessMemberRole Role { get; set; } = BusinessMemberRole.Staff;

    // Owners implicitly have every permission; this matters for staff.
    public BusinessPermissions Permissions { get; set; } = BusinessPermissions.None;

    public bool IsActive { get; set; } = true;

    public BusinessPermissions EffectivePermissions =>
        Role == BusinessMemberRole.Owner ? BusinessPermissions.All : Permissions;
}

/// <summary>Email invitation for a staff member (story 57).</summary>
public class BusinessInvitation : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public string Email { get; set; } = default!;
    public BusinessPermissions Permissions { get; set; }

    // SHA-256 of the token in the invitation link; the raw token is only emailed.
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public Guid InvitedByUserId { get; set; }
    public DateTime LastSentAt { get; set; } = DateTime.UtcNow;

    public Guid? AcceptedByUserId { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

/// <summary>Optional parent location grouping several courts of one sports centre (story 59).</summary>
public class Venue : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = "XK";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? CoverImageUrl { get; set; }

    public ICollection<Facility> Facilities { get; set; } = new List<Facility>();
}

/// <summary>A plan businesses subscribe to (story 42). Prices and limits are set by admins.</summary>
public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;

    // Limits
    public int MaxFacilities { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class BusinessSubscription : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = default!;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Incomplete;

    // Billing stays anchored to the original start date, also after late payments.
    public DateTime? AnchorDate { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }

    // Set when a renewal fails: CurrentPeriodEnd + 14 days.
    public DateTime? GraceEndsAt { get; set; }
    public bool GraceNoticeSent { get; set; }

    public bool CancelAtPeriodEnd { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? ExpiredAt { get; set; }

    // Postgres xmin: the renewal job and a webhook must not overwrite each other.
    public uint Version { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

/// <summary>
/// A payment attempt through the payment provider, for a subscription or (post-MVP) a reservation.
/// Status only changes from the backend (webhook / renewal job), never from the frontend.
/// </summary>
public class Payment : BaseEntity
{
    public PaymentPurpose Purpose { get; set; }

    public Guid? BusinessId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public BusinessSubscription? Subscription { get; set; }
    public Guid? BookingId { get; set; }
    public Guid? PayerUserId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public PaymentRecordStatus Status { get; set; } = PaymentRecordStatus.Pending;

    public string Provider { get; set; } = default!;
    public string? ProviderReference { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? FailureReason { get; set; }

    // Subscription period this payment covers.
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }

    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }
}
