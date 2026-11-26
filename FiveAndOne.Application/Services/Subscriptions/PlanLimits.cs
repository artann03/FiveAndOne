using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Subscriptions;

public sealed record BusinessPlanLimits(bool HasActiveSubscription, string? PlanName, int MaxFacilities, SubscriptionStatus? Status);

/// <summary>What the business's current subscription allows (story 08: facility limit comes from the active plan).</summary>
public static class PlanLimits
{
    public static async Task<BusinessPlanLimits> GetAsync(ApplicationDbContext db, Guid businessId, int maxWithoutSubscription, CancellationToken cancellationToken)
    {
        // PastDue = inside the grace period: still full access.
        var subscription = await db.BusinessSubscriptions.AsNoTracking()
            .Include(x => x.Plan)
            .Where(x => x.BusinessId == businessId && (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.PastDue))
            .OrderByDescending(x => x.CurrentPeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);

        return subscription is null
            ? new BusinessPlanLimits(false, null, maxWithoutSubscription, null)
            : new BusinessPlanLimits(true, subscription.Plan.Name, subscription.Plan.MaxFacilities, subscription.Status);
    }
}
