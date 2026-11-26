using FiveAndOne.Domain.Entities.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FiveAndOne.Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> entity)
    {
        entity.ToTable("Businesses", t =>
            t.HasCheckConstraint("CK_Businesses_CancellationCutoff", "\"CancellationCutoffHours\" >= 0"));
        entity.HasKey(x => x.Id);

        entity.Property(x => x.BusinessName).IsRequired().HasMaxLength(150);
        entity.Property(x => x.RegistrationNumber).HasMaxLength(30);
        entity.Property(x => x.AddressLine).IsRequired().HasMaxLength(250);
        entity.Property(x => x.City).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Country).IsRequired().HasMaxLength(2);
        entity.Property(x => x.Phone).IsRequired().HasMaxLength(30);
        entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
        entity.Property(x => x.PreferredLanguage).IsRequired().HasMaxLength(10);
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.SuspensionReason).HasMaxLength(500);
        entity.Property(x => x.CancellationPolicyNote).HasMaxLength(1000);

        entity.HasIndex(x => x.OwnerUserId);
        entity.HasIndex(x => x.Status);
        entity.HasIndex(x => x.RegistrationNumber).IsUnique().HasFilter("\"RegistrationNumber\" IS NOT NULL");

        entity.HasOne(x => x.Owner)
            .WithMany(x => x.OwnedBusinesses)
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BusinessMemberConfiguration : IEntityTypeConfiguration<BusinessMember>
{
    public void Configure(EntityTypeBuilder<BusinessMember> entity)
    {
        entity.ToTable("BusinessMembers");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Role).HasConversion<int>();
        entity.Property(x => x.Permissions).HasConversion<int>();

        entity.HasIndex(x => new { x.BusinessId, x.UserId }).IsUnique();
        entity.HasIndex(x => x.UserId);

        entity.HasOne(x => x.Business)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.User)
            .WithMany(x => x.BusinessMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BusinessInvitationConfiguration : IEntityTypeConfiguration<BusinessInvitation>
{
    public void Configure(EntityTypeBuilder<BusinessInvitation> entity)
    {
        entity.ToTable("BusinessInvitations");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
        entity.Property(x => x.Permissions).HasConversion<int>();
        entity.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        entity.Property(x => x.Status).HasConversion<int>();

        entity.HasIndex(x => x.TokenHash).IsUnique();
        entity.HasIndex(x => new { x.BusinessId, x.Status });
        entity.HasIndex(x => x.Email);

        entity.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> entity)
    {
        entity.ToTable("Venues");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Name).IsRequired().HasMaxLength(150);
        entity.Property(x => x.Slug).IsRequired().HasMaxLength(180);
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.AddressLine).HasMaxLength(250);
        entity.Property(x => x.City).HasMaxLength(100);
        entity.Property(x => x.Country).IsRequired().HasMaxLength(2);
        entity.Property(x => x.Latitude).HasPrecision(9, 6);
        entity.Property(x => x.Longitude).HasPrecision(9, 6);
        entity.Property(x => x.CoverImageUrl).HasMaxLength(500);

        entity.HasIndex(x => x.Slug).IsUnique();
        entity.HasIndex(x => x.BusinessId);

        entity.HasOne(x => x.Business)
            .WithMany(x => x.Venues)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> entity)
    {
        entity.ToTable("SubscriptionPlans", t =>
        {
            t.HasCheckConstraint("CK_SubscriptionPlans_Price", "\"Price\" >= 0");
            t.HasCheckConstraint("CK_SubscriptionPlans_MaxFacilities", "\"MaxFacilities\" >= 0");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Description).HasMaxLength(1000);
        entity.Property(x => x.Price).HasPrecision(18, 2);
        entity.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        entity.Property(x => x.BillingInterval).HasConversion<int>();

        entity.HasIndex(x => x.Name).IsUnique();
    }
}

public class BusinessSubscriptionConfiguration : IEntityTypeConfiguration<BusinessSubscription>
{
    public void Configure(EntityTypeBuilder<BusinessSubscription> entity)
    {
        entity.ToTable("BusinessSubscriptions");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.Version).IsRowVersion();

        entity.HasIndex(x => new { x.BusinessId, x.Status });
        entity.HasIndex(x => new { x.Status, x.CurrentPeriodEnd });

        entity.HasOne(x => x.Business)
            .WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> entity)
    {
        entity.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" >= 0"));
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Purpose).HasConversion<int>();
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        entity.Property(x => x.Provider).IsRequired().HasMaxLength(50);
        entity.Property(x => x.ProviderReference).HasMaxLength(200);
        entity.Property(x => x.CheckoutUrl).HasMaxLength(1000);
        entity.Property(x => x.FailureReason).HasMaxLength(500);

        entity.HasIndex(x => new { x.Provider, x.ProviderReference }).IsUnique()
            .HasFilter("\"ProviderReference\" IS NOT NULL");
        entity.HasIndex(x => new { x.BusinessId, x.CreatedAt });
        entity.HasIndex(x => x.BookingId);

        entity.HasOne(x => x.Subscription)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
