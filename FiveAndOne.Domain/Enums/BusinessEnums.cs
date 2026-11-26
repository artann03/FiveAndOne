namespace FiveAndOne.Domain.Enums;

public enum BusinessStatus
{
    Active = 1,
    /// <summary>Suspended by a platform administrator.</summary>
    Suspended = 2,
    /// <summary>Subscription unpaid after the grace period: hidden from discovery, no new reservations.</summary>
    Inactive = 3
}

public enum BusinessMemberRole
{
    Owner = 1,
    Staff = 2
}

[Flags]
public enum BusinessPermissions
{
    None = 0,
    ManageReservations = 1,
    ManageAvailability = 2,
    ManageFacilities = 4,
    ManagePricing = 8,
    ViewAnalytics = 16,
    ManageProfile = 32,
    ManageStaff = 64,
    ManageSubscription = 128,
    All = ManageReservations | ManageAvailability | ManageFacilities | ManagePricing
          | ViewAnalytics | ManageProfile | ManageStaff | ManageSubscription
}

public enum InvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Revoked = 3,
    Expired = 4
}

public enum BillingInterval
{
    Monthly = 1,
    Yearly = 2
}

public enum SubscriptionStatus
{
    /// <summary>Created at checkout, waiting for the first payment.</summary>
    Incomplete = 1,
    Active = 2,
    /// <summary>Renewal unpaid; inside the 14-day grace period.</summary>
    PastDue = 3,
    /// <summary>Grace period over without payment.</summary>
    Expired = 4,
    Cancelled = 5
}

public enum PaymentPurpose
{
    Subscription = 1,
    Reservation = 2
}

public enum PaymentRecordStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Refunded = 4,
    Cancelled = 5
}
