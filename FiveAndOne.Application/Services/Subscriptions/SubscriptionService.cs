using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Abstractions;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Subscriptions;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FiveAndOne.Application.Services.Subscriptions;

public interface ISubscriptionService
{
    // Plans (story 42)
    Task<List<PlanResponse>> GetActivePlansAsync(CancellationToken cancellationToken = default);
    Task<List<PlanResponse>> GetAllPlansAsync(CancellationToken cancellationToken = default);
    Task<PlanResponse> SavePlanAsync(Guid actorId, Guid? planId, SavePlanRequest request, CancellationToken cancellationToken = default);

    // Business (stories 43-45)
    Task<SubscriptionResponse> GetMineAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default);
    Task<CheckoutResponse> SubscribeAsync(Guid userId, Guid? businessId, Guid planId, CancellationToken cancellationToken = default);
    Task<CheckoutResponse> PayOutstandingAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default);
    Task<SubscriptionResponse> SetCancelAtPeriodEndAsync(Guid userId, Guid? businessId, bool cancel, CancellationToken cancellationToken = default);

    /// <summary>Applies a verified payment result (webhook). Never called from the frontend.</summary>
    Task ApplyPaymentResultAsync(Payment payment, bool succeeded, string? failureReason, CancellationToken cancellationToken = default);

    // Background jobs (stories 44-46)
    Task<int> ProcessRenewalsAsync(CancellationToken cancellationToken = default);
    Task<int> ProcessRetentionAsync(CancellationToken cancellationToken = default);

    // Admin
    Task<PagedResponse<AdminSubscriptionResponse>> AdminListAsync(SubscriptionStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<List<RetentionStatusResponse>> AdminRetentionAsync(CancellationToken cancellationToken = default);
}

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessAccess _access;
    private readonly IPaymentGateway _gateway;
    private readonly INotificationService _notifications;
    private readonly IPlatformSettings _settings;
    private readonly IAuditService _audit;
    private readonly AppSettings _app;

    public SubscriptionService(ApplicationDbContext db, IBusinessAccess access, IPaymentGateway gateway, INotificationService notifications,
        IPlatformSettings settings, IAuditService audit, IOptions<AppSettings> app)
    {
        _db = db;
        _access = access;
        _gateway = gateway;
        _notifications = notifications;
        _settings = settings;
        _audit = audit;
        _app = app.Value;
    }

    // =====================================================================
    // Plans
    // =====================================================================

    public async Task<List<PlanResponse>> GetActivePlansAsync(CancellationToken cancellationToken = default) =>
        (await _db.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ThenBy(p => p.Price).ToListAsync(cancellationToken))
        .Select(PlanResponse.FromEntity).ToList();

    public async Task<List<PlanResponse>> GetAllPlansAsync(CancellationToken cancellationToken = default) =>
        (await _db.SubscriptionPlans.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Price).ToListAsync(cancellationToken))
        .Select(PlanResponse.FromEntity).ToList();

    public async Task<PlanResponse> SavePlanAsync(Guid actorId, Guid? planId, SavePlanRequest request, CancellationToken cancellationToken = default)
    {
        SubscriptionPlan plan;
        if (planId.HasValue)
        {
            plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken) ?? throw new NotFoundException("Plan not found.");
            plan.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            plan = new SubscriptionPlan();
            _db.SubscriptionPlans.Add(plan);
        }

        var name = request.Name!.Trim();
        if (await _db.SubscriptionPlans.AnyAsync(p => p.Id != plan.Id && p.Name == name, cancellationToken))
        {
            throw new ConflictException("A plan with this name already exists.", "duplicate");
        }

        // Existing subscribers keep their price until they change plan; new checkouts use the new values.
        plan.Name = name;
        plan.Description = Text.NullIfEmpty(request.Description);
        plan.Price = Math.Round(request.Price, 2);
        plan.Currency = "EUR";
        plan.BillingInterval = request.BillingInterval;
        plan.MaxFacilities = request.MaxFacilities;
        plan.IsActive = request.IsActive;
        plan.SortOrder = request.SortOrder;

        _audit.Log(actorId, planId.HasValue ? "plan.updated" : "plan.created", "SubscriptionPlan", plan.Id.ToString(),
            $"{plan.Name}: {plan.Price} {plan.Currency}/{plan.BillingInterval}, {plan.MaxFacilities} facilities, active {plan.IsActive}");
        await _db.SaveChangesAsync(cancellationToken);
        return PlanResponse.FromEntity(plan);
    }

    // =====================================================================
    // Business
    // =====================================================================

    public async Task<SubscriptionResponse> GetMineAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);
        var business = context.Business;

        var subscription = await _db.BusinessSubscriptions.AsNoTracking().Include(s => s.Plan)
            .Where(s => s.BusinessId == business.Id && s.Status != SubscriptionStatus.Incomplete)
            .OrderByDescending(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.BusinessId == business.Id && p.Purpose == PaymentPurpose.Subscription)
            .OrderByDescending(p => p.CreatedAt).Take(50).ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var upcoming = await _db.Bookings.CountAsync(b => b.BusinessId == business.Id && b.Status == BookingStatus.Confirmed && b.StartsAt > now, cancellationToken);

        return new SubscriptionResponse
        {
            Id = subscription?.Id,
            BusinessStatus = business.Status,
            Status = subscription?.Status,
            Plan = subscription is null ? null : PlanResponse.FromEntity(subscription.Plan),
            AnchorDate = subscription?.AnchorDate,
            CurrentPeriodStart = subscription?.CurrentPeriodStart,
            CurrentPeriodEnd = subscription?.CurrentPeriodEnd,
            NextBillingDate = subscription?.Status == SubscriptionStatus.Active && !subscription.CancelAtPeriodEnd ? subscription.CurrentPeriodEnd : null,
            GraceEndsAt = subscription?.GraceEndsAt,
            CancelAtPeriodEnd = subscription?.CancelAtPeriodEnd ?? false,
            LastPaymentStatus = payments.FirstOrDefault()?.Status,
            Notice = Notice(business, subscription, upcoming),
            UpcomingReservations = upcoming,
            InactiveSince = business.InactiveSince,
            DataRetentionUntil = business.RetentionUntil,
            Payments = payments.Select(PaymentResponse.FromEntity).ToList()
        };
    }

    public async Task<CheckoutResponse> SubscribeAsync(Guid userId, Guid? businessId, Guid planId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageSubscription, cancellationToken);
        if (context.Business.Status == BusinessStatus.Suspended)
        {
            throw new ForbiddenException("Your business is suspended by an administrator.", "business_suspended");
        }

        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        var current = await _db.BusinessSubscriptions
            .Where(s => s.BusinessId == context.BusinessId && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue))
            .FirstOrDefaultAsync(cancellationToken);

        if (current is not null && current.PlanId == plan.Id)
        {
            throw new ConflictException(current.Status == SubscriptionStatus.PastDue
                ? "Your renewal is unpaid. Use \"Pay now\" to settle it."
                : "You are already subscribed to this plan.", "already_subscribed");
        }

        // A plan change keeps working until the new plan's first payment succeeds.
        if (current is not null)
        {
            var facilities = await _db.Facilities.CountAsync(f => f.BusinessId == context.BusinessId, cancellationToken);
            if (facilities > plan.MaxFacilities)
            {
                throw new PlanLimitExceededException(
                    $"You have {facilities} facilities but {plan.Name} allows {plan.MaxFacilities}. Delete some facilities before switching.",
                    "plan_too_small");
            }
        }

        // Abandoned checkouts are replaced (kept as Cancelled because payments reference them).
        var stale = await _db.BusinessSubscriptions.Where(s => s.BusinessId == context.BusinessId && s.Status == SubscriptionStatus.Incomplete).ToListAsync(cancellationToken);
        foreach (var abandoned in stale)
        {
            abandoned.Status = SubscriptionStatus.Cancelled;
            abandoned.CancelledAt = DateTime.UtcNow;
        }
        var stalePayments = await _db.Payments.Where(p => p.BusinessId == context.BusinessId && p.Purpose == PaymentPurpose.Subscription
                                                          && p.Status == PaymentRecordStatus.Pending).ToListAsync(cancellationToken);
        foreach (var abandonedPayment in stalePayments)
        {
            abandonedPayment.Status = PaymentRecordStatus.Cancelled;
        }

        var subscription = new BusinessSubscription { BusinessId = context.BusinessId, PlanId = plan.Id, Plan = plan, Status = SubscriptionStatus.Incomplete };
        _db.BusinessSubscriptions.Add(subscription);

        var payment = await CreateCheckoutPaymentAsync(context, subscription, plan.Price, $"SportX {plan.Name} ({plan.BillingInterval})", null, null, cancellationToken);
        _audit.Log(userId, "subscription.checkout_started", "BusinessSubscription", subscription.Id.ToString(), plan.Name, businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return new CheckoutResponse { PaymentId = payment.Id, SubscriptionId = subscription.Id, CheckoutUrl = payment.CheckoutUrl! };
    }

    public async Task<CheckoutResponse> PayOutstandingAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageSubscription, cancellationToken);

        // The unpaid renewal (grace period) or, after expiry, the latest subscription to reactivate.
        var subscription = await _db.BusinessSubscriptions.Include(s => s.Plan)
            .Where(s => s.BusinessId == context.BusinessId && (s.Status == SubscriptionStatus.PastDue || s.Status == SubscriptionStatus.Expired))
            .OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException("There is nothing to pay right now.", "nothing_to_pay");

        if (!subscription.Plan.IsActive)
        {
            throw new ConflictException("Your plan is no longer offered. Choose a new plan.", "plan_unavailable");
        }

        // Billing stays anchored: a late payment covers the period that was due, not a new one from today.
        var (start, end) = subscription.Status == SubscriptionStatus.PastDue
            ? (subscription.CurrentPeriodEnd!.Value, NextPeriodEnd(subscription.AnchorDate!.Value, subscription.CurrentPeriodEnd!.Value, subscription.Plan.BillingInterval))
            : CurrentAnchoredPeriod(subscription.AnchorDate ?? DateTime.UtcNow, subscription.Plan.BillingInterval);

        var payment = await CreateCheckoutPaymentAsync(context, subscription, subscription.Plan.Price, $"SportX {subscription.Plan.Name} renewal", start, end, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new CheckoutResponse { PaymentId = payment.Id, SubscriptionId = subscription.Id, CheckoutUrl = payment.CheckoutUrl! };
    }

    public async Task<SubscriptionResponse> SetCancelAtPeriodEndAsync(Guid userId, Guid? businessId, bool cancel, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageSubscription, cancellationToken);
        var subscription = await _db.BusinessSubscriptions
            .FirstOrDefaultAsync(s => s.BusinessId == context.BusinessId && s.Status == SubscriptionStatus.Active, cancellationToken)
            ?? throw new ConflictException("There is no active subscription.", "no_active_subscription");

        subscription.CancelAtPeriodEnd = cancel;
        subscription.UpdatedAt = DateTime.UtcNow;
        _audit.Log(userId, cancel ? "subscription.cancel_scheduled" : "subscription.cancel_revoked", "BusinessSubscription", subscription.Id.ToString(), null, businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetMineAsync(userId, context.BusinessId, cancellationToken);
    }

    public async Task ApplyPaymentResultAsync(Payment payment, bool succeeded, string? failureReason, CancellationToken cancellationToken = default)
    {
        if (payment.Status != PaymentRecordStatus.Pending)
        {
            return;   // webhooks can be delivered more than once
        }

        var subscription = await _db.BusinessSubscriptions.Include(s => s.Plan).Include(s => s.Business)
            .FirstOrDefaultAsync(s => s.Id == payment.SubscriptionId, cancellationToken);
        if (subscription is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        payment.UpdatedAt = now;

        if (!succeeded)
        {
            payment.Status = PaymentRecordStatus.Failed;
            payment.FailureReason = failureReason ?? "Payment failed";
            _audit.Log(null, "subscription.payment_failed", "Payment", payment.Id.ToString(), payment.FailureReason, businessId: subscription.BusinessId);
            return;
        }

        payment.Status = PaymentRecordStatus.Succeeded;
        payment.PaidAt = now;

        if (subscription.Status == SubscriptionStatus.Incomplete)
        {
            // First payment: the subscription starts now and its billing day is anchored to today.
            subscription.Status = SubscriptionStatus.Active;
            subscription.AnchorDate = now;
            subscription.CurrentPeriodStart = now;
            subscription.CurrentPeriodEnd = NextPeriodEnd(now, now, subscription.Plan.BillingInterval);
            payment.PeriodStart = subscription.CurrentPeriodStart;
            payment.PeriodEnd = subscription.CurrentPeriodEnd;

            // A plan change replaces the previous subscription.
            var previous = await _db.BusinessSubscriptions
                .Where(s => s.BusinessId == subscription.BusinessId && s.Id != subscription.Id
                            && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue))
                .ToListAsync(cancellationToken);
            foreach (var old in previous)
            {
                old.Status = SubscriptionStatus.Cancelled;
                old.CancelledAt = now;
            }
        }
        else
        {
            // Renewal / late payment for the period recorded on the payment.
            subscription.Status = SubscriptionStatus.Active;
            subscription.CurrentPeriodStart = payment.PeriodStart ?? subscription.CurrentPeriodEnd;
            subscription.CurrentPeriodEnd = payment.PeriodEnd ?? NextPeriodEnd(subscription.AnchorDate!.Value, subscription.CurrentPeriodEnd!.Value, subscription.Plan.BillingInterval);
            subscription.GraceEndsAt = null;
            subscription.GraceNoticeSent = false;
            subscription.ExpiredAt = null;
        }

        subscription.UpdatedAt = now;
        Reactivate(subscription.Business);
        _audit.Log(null, "subscription.payment_succeeded", "Payment", payment.Id.ToString(), $"{payment.Amount} {payment.Currency}", businessId: subscription.BusinessId);
    }

    // =====================================================================
    // Background jobs
    // =====================================================================

    /// <summary>Renewals, failed renewals (grace period), grace reminders and expiry (stories 44, 45).</summary>
    public async Task<int> ProcessRenewalsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var graceDays = await _settings.GetIntAsync(PlatformSettingKeys.GraceDays, 14, cancellationToken);
        var retentionYears = await _settings.GetIntAsync(PlatformSettingKeys.RetentionYears, 2, cancellationToken);
        var processed = 0;

        // 1) Periods that ended.
        var due = await _db.BusinessSubscriptions.Include(s => s.Plan).Include(s => s.Business)
            .Where(s => s.Status == SubscriptionStatus.Active && s.CurrentPeriodEnd <= now)
            .Take(100).ToListAsync(cancellationToken);

        foreach (var subscription in due)
        {
            processed++;
            if (subscription.CancelAtPeriodEnd)
            {
                subscription.Status = SubscriptionStatus.Cancelled;
                subscription.CancelledAt = now;
                Deactivate(subscription.Business, now, retentionYears, "Subscription cancelled");
                continue;
            }

            var start = subscription.CurrentPeriodEnd!.Value;
            var end = NextPeriodEnd(subscription.AnchorDate!.Value, start, subscription.Plan.BillingInterval);
            var payment = new Payment
            {
                Purpose = PaymentPurpose.Subscription,
                BusinessId = subscription.BusinessId,
                SubscriptionId = subscription.Id,
                Amount = subscription.Plan.Price,
                Currency = subscription.Plan.Currency,
                Provider = _gateway.Name,
                PeriodStart = start,
                PeriodEnd = end
            };
            _db.Payments.Add(payment);

            var result = subscription.Plan.Price == 0
                ? new ChargeResult(true, $"free-{payment.Id:N}", null)
                : await _gateway.ChargeRenewalAsync(payment.Id, subscription.BusinessId, payment.Amount, payment.Currency, cancellationToken);
            payment.ProviderReference = result.ProviderReference;

            if (result.Succeeded)
            {
                payment.Status = PaymentRecordStatus.Succeeded;
                payment.PaidAt = now;
                subscription.CurrentPeriodStart = start;
                subscription.CurrentPeriodEnd = end;
            }
            else
            {
                payment.Status = PaymentRecordStatus.Failed;
                payment.FailureReason = result.FailureReason;
                subscription.Status = SubscriptionStatus.PastDue;
                subscription.GraceEndsAt = start.AddDays(graceDays);
                await NotifyAsync(subscription.Business, "subscription.renewal_failed",
                    "Payment failed – SportX subscription",
                    $"We couldn't renew your {subscription.Plan.Name} subscription. Pay before {subscription.GraceEndsAt:dd.MM.yyyy} to keep your facilities visible and keep taking reservations. " +
                    "Existing reservations are not cancelled.", cancellationToken);
            }
            subscription.UpdatedAt = now;
        }

        // 2) Grace period about to end: remind once, with the consequences for existing reservations (story 45).
        var reminderFrom = now.AddDays(3);
        var reminders = await _db.BusinessSubscriptions.Include(s => s.Plan).Include(s => s.Business)
            .Where(s => s.Status == SubscriptionStatus.PastDue && !s.GraceNoticeSent && s.GraceEndsAt <= reminderFrom && s.GraceEndsAt > now)
            .Take(100).ToListAsync(cancellationToken);
        foreach (var subscription in reminders)
        {
            processed++;
            var upcoming = await _db.Bookings.CountAsync(b => b.BusinessId == subscription.BusinessId && b.Status == BookingStatus.Confirmed && b.StartsAt > now, cancellationToken);
            await NotifyAsync(subscription.Business, "subscription.grace_ending",
                "Your SportX access ends soon",
                $"Your subscription is unpaid. On {subscription.GraceEndsAt:dd.MM.yyyy} your facilities will be hidden and new reservations will stop. " +
                $"Your {upcoming} confirmed upcoming reservation(s) will NOT be cancelled; you remain responsible for informing those customers.", cancellationToken);
            subscription.GraceNoticeSent = true;
        }

        // 3) Grace period over: restrict (stories 44, 45). Confirmed reservations stay as they are.
        var expired = await _db.BusinessSubscriptions.Include(s => s.Plan).Include(s => s.Business)
            .Where(s => s.Status == SubscriptionStatus.PastDue && s.GraceEndsAt <= now)
            .Take(100).ToListAsync(cancellationToken);
        foreach (var subscription in expired)
        {
            processed++;
            subscription.Status = SubscriptionStatus.Expired;
            subscription.ExpiredAt = now;
            subscription.UpdatedAt = now;
            Deactivate(subscription.Business, now, retentionYears, "Subscription unpaid after the grace period");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return processed;
    }

    /// <summary>Anonymizes inactive businesses whose retention period ended (story 46).</summary>
    public async Task<int> ProcessRetentionAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var businesses = await _db.Businesses.Include(b => b.Members)
            .Where(b => b.Status == BusinessStatus.Inactive && b.AnonymizedAt == null && b.RetentionUntil <= now)
            .Take(20).ToListAsync(cancellationToken);

        foreach (var business in businesses)
        {
            business.BusinessName = $"Deleted business {business.Id.ToString("N")[..6]}";
            business.RegistrationNumber = null;
            business.AddressLine = "-";
            business.Phone = "-";
            business.Email = $"deleted-{business.Id:N}@invalid.sportx";
            business.CancellationPolicyNote = null;
            business.AnonymizedAt = now;
            foreach (var member in business.Members) member.IsActive = false;

            // External customers' contact details are personal data of non-users: remove them.
            await _db.Bookings.Where(b => b.BusinessId == business.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.ContactName, (string?)null)
                    .SetProperty(b => b.ContactPhone, (string?)null)
                    .SetProperty(b => b.ContactEmail, (string?)null)
                    .SetProperty(b => b.InternalNotes, (string?)null)
                    .SetProperty(b => b.CustomerNotes, (string?)null), cancellationToken);

            await _db.Facilities.Where(f => f.BusinessId == business.Id && f.Status == FacilityStatus.Published)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, FacilityStatus.Hidden), cancellationToken);

            _audit.Log(null, "business.anonymized", "Business", business.Id.ToString(), "Retention period ended", businessId: business.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return businesses.Count;
    }

    // =====================================================================
    // Admin
    // =====================================================================

    public async Task<PagedResponse<AdminSubscriptionResponse>> AdminListAsync(SubscriptionStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = _db.BusinessSubscriptions.AsNoTracking();
        if (status.HasValue) q = q.Where(s => s.Status == status);

        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderByDescending(s => s.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new AdminSubscriptionResponse
            {
                Id = s.Id, BusinessId = s.BusinessId, BusinessName = s.Business.BusinessName, PlanName = s.Plan.Name,
                Status = s.Status, CurrentPeriodEnd = s.CurrentPeriodEnd, GraceEndsAt = s.GraceEndsAt, CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResponse<AdminSubscriptionResponse>.Create(items, page, pageSize, total);
    }

    public async Task<List<RetentionStatusResponse>> AdminRetentionAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Businesses.AsNoTracking()
            .Where(b => b.Status == BusinessStatus.Inactive || b.AnonymizedAt != null)
            .OrderBy(b => b.RetentionUntil)
            .Select(b => new { b.Id, b.BusinessName, b.Status, b.InactiveSince, b.RetentionUntil, b.AnonymizedAt })
            .ToListAsync(cancellationToken);

        return rows.Select(b => new RetentionStatusResponse
        {
            BusinessId = b.Id,
            BusinessName = b.BusinessName,
            Status = b.Status,
            InactiveSince = b.InactiveSince,
            RetentionUntil = b.RetentionUntil,
            AnonymizedAt = b.AnonymizedAt,
            DaysUntilDisposition = b.RetentionUntil.HasValue ? Math.Max(0, (int)(b.RetentionUntil.Value - now).TotalDays) : 0
        }).ToList();
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private async Task<Payment> CreateCheckoutPaymentAsync(BusinessContext context, BusinessSubscription subscription, decimal amount, string description,
        DateTime? periodStart, DateTime? periodEnd, CancellationToken cancellationToken)
    {
        var payment = new Payment
        {
            Purpose = PaymentPurpose.Subscription,
            BusinessId = context.BusinessId,
            SubscriptionId = subscription.Id,
            PayerUserId = context.UserId,
            Amount = amount,
            Currency = "EUR",
            Provider = _gateway.Name,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd
        };

        var session = await _gateway.CreateCheckoutAsync(new CheckoutRequest(payment.Id, amount, "EUR", description, context.Business.Email), cancellationToken);
        payment.ProviderReference = session.ProviderReference;
        payment.CheckoutUrl = session.CheckoutUrl;
        _db.Payments.Add(payment);
        return payment;
    }

    private void Deactivate(Business business, DateTime now, int retentionYears, string reason)
    {
        if (business.Status == BusinessStatus.Inactive)
        {
            return;
        }

        // Facilities keep their status; FacilityQueries.Public() hides inactive businesses, so paying restores everything.
        business.Status = BusinessStatus.Inactive;
        business.InactiveSince = now;
        business.RetentionUntil = now.AddYears(retentionYears);
        business.UpdatedAt = now;
        _audit.Log(null, "business.inactive", "Business", business.Id.ToString(), reason, businessId: business.Id);
        _notifications.QueueEmail(business.Email, "Your SportX listing is paused",
            $"{reason}. Your facilities are hidden and new reservations are stopped. Existing confirmed reservations were NOT cancelled. " +
            $"Your data is kept until {business.RetentionUntil:dd.MM.yyyy}. Renew any time: {_app.FrontendBaseUrl}/business/subscription",
            "business.inactive", business.Id);
    }

    private void Reactivate(Business business)
    {
        if (business.Status != BusinessStatus.Inactive)
        {
            return;
        }

        business.Status = BusinessStatus.Active;
        business.InactiveSince = null;
        business.RetentionUntil = null;
        business.UpdatedAt = DateTime.UtcNow;
        _audit.Log(null, "business.reactivated", "Business", business.Id.ToString(), "Subscription paid", businessId: business.Id);
    }

    private Task NotifyAsync(Business business, string template, string subject, string body, CancellationToken cancellationToken)
    {
        _notifications.QueueEmail(business.Email, subject, $"{body}\n\n{_app.FrontendBaseUrl}/business/subscription", template, business.Id);
        return Task.CompletedTask;
    }

    private static string? Notice(Business business, BusinessSubscription? subscription, int upcoming)
    {
        if (business.Status == BusinessStatus.Inactive)
        {
            return $"Your subscription has expired. Your facilities are hidden and new reservations are stopped. " +
                   $"{upcoming} confirmed reservation(s) remain valid; please contact those customers. Renew to restore everything.";
        }

        return subscription?.Status switch
        {
            null => "You don't have a subscription yet. Choose a plan to publish facilities and take reservations.",
            SubscriptionStatus.PastDue => $"Your renewal payment failed. Pay before {subscription.GraceEndsAt:dd.MM.yyyy} to avoid hiding your facilities. " +
                                          "Existing reservations are never cancelled automatically.",
            SubscriptionStatus.Active when subscription.CancelAtPeriodEnd =>
                $"Your subscription ends on {subscription.CurrentPeriodEnd:dd.MM.yyyy}. After that your facilities are hidden; existing reservations stay valid.",
            _ => null
        };
    }

    /// <summary>The first period end after <paramref name="after"/>, counted from the anchor (keeps the billing day stable).</summary>
    internal static DateTime NextPeriodEnd(DateTime anchor, DateTime after, BillingInterval interval)
    {
        var months = interval == BillingInterval.Yearly ? 12 : 1;
        var k = 1;
        while (anchor.AddMonths(k * months) <= after)
        {
            k++;
        }
        return anchor.AddMonths(k * months);
    }

    private static (DateTime Start, DateTime End) CurrentAnchoredPeriod(DateTime anchor, BillingInterval interval)
    {
        var now = DateTime.UtcNow;
        var end = NextPeriodEnd(anchor, now, interval);
        var months = interval == BillingInterval.Yearly ? 12 : 1;
        return (end.AddMonths(-months), end);
    }
}
