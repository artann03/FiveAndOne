using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.API.Seeding;

public static class DevelopmentSeeder
{
    /// <summary>
    /// Development only: one subscription plan so businesses can subscribe (mock payment) and publish while testing.
    /// Real plans and prices are created by a SuperAdmin (POST /api/admin/plans); none are hard-coded for production.
    /// </summary>
    public static async Task SeedDevelopmentDataAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await db.SubscriptionPlans.AnyAsync())
        {
            return;
        }

        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Name = "Dev Starter",
            Description = "Development plan (created automatically in Development only).",
            Price = 0,
            Currency = "EUR",
            BillingInterval = BillingInterval.Monthly,
            MaxFacilities = 5,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }
}
