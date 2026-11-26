using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Subscriptions;

public class PlanResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = default!;
    public BillingInterval BillingInterval { get; set; }
    public int MaxFacilities { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }

    public static PlanResponse FromEntity(SubscriptionPlan p) => new()
    {
        Id = p.Id, Name = p.Name, Description = p.Description, Price = p.Price, Currency = p.Currency,
        BillingInterval = p.BillingInterval, MaxFacilities = p.MaxFacilities, IsActive = p.IsActive, SortOrder = p.SortOrder
    };
}

/// <summary>Create/edit a plan (story 42). Prices and limits are decided by the business, not hard-coded.</summary>
public class SavePlanRequest
{
    [Required, StringLength(100)]
    public string? Name { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 100000)]
    public decimal Price { get; set; }

    public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;

    [Range(0, 1000)]
    public int MaxFacilities { get; set; } = 1;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class SubscribeRequest
{
    [Required]
    public Guid? PlanId { get; set; }
}

public class CheckoutResponse
{
    public Guid PaymentId { get; set; }
    public Guid SubscriptionId { get; set; }

    /// <summary>Send the user here to pay. The subscription activates only after the provider confirms (webhook).</summary>
    public string CheckoutUrl { get; set; } = default!;
}

public class PaymentResponse
{
    public Guid Id { get; set; }
    public PaymentPurpose Purpose { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public PaymentRecordStatus Status { get; set; }
    public string Provider { get; set; } = default!;
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? FailureReason { get; set; }

    public static PaymentResponse FromEntity(Payment p) => new()
    {
        Id = p.Id, Purpose = p.Purpose, Amount = p.Amount, Currency = p.Currency, Status = p.Status, Provider = p.Provider,
        PeriodStart = p.PeriodStart, PeriodEnd = p.PeriodEnd, CreatedAt = p.CreatedAt, PaidAt = p.PaidAt, FailureReason = p.FailureReason
    };
}

/// <summary>Subscription screen (story 44): status, plan, billing date, payment status and history.</summary>
public class SubscriptionResponse
{
    public Guid? Id { get; set; }
    public BusinessStatus BusinessStatus { get; set; }
    public SubscriptionStatus? Status { get; set; }
    public PlanResponse? Plan { get; set; }
    public DateTime? AnchorDate { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }

    /// <summary>Next billing date (= current period end, anchored to the original start).</summary>
    public DateTime? NextBillingDate { get; set; }
    public DateTime? GraceEndsAt { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public PaymentRecordStatus? LastPaymentStatus { get; set; }

    /// <summary>Plain-language explanation of what the current status means (story 45).</summary>
    public string? Notice { get; set; }

    /// <summary>Confirmed upcoming reservations the business stays responsible for if access is restricted.</summary>
    public int UpcomingReservations { get; set; }

    public DateTime? InactiveSince { get; set; }
    public DateTime? DataRetentionUntil { get; set; }

    public List<PaymentResponse> Payments { get; set; } = new();
}

/// <summary>Admin list row (story 47: Business Subscriptions).</summary>
public class AdminSubscriptionResponse
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = default!;
    public string PlanName { get; set; } = default!;
    public SubscriptionStatus Status { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? GraceEndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RetentionStatusResponse
{
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = default!;
    public BusinessStatus Status { get; set; }
    public DateTime? InactiveSince { get; set; }
    public DateTime? RetentionUntil { get; set; }
    public DateTime? AnonymizedAt { get; set; }
    public int DaysUntilDisposition { get; set; }
}
