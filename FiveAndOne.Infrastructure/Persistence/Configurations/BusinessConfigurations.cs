using FiveAndOne.Domain.Entities.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FiveAndOne.Infrastructure.Persistence.Configurations;

public class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> entity)
    {
        entity.ToTable("BusinessProfiles");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.BusinessName).IsRequired().HasMaxLength(150);
        entity.Property(x => x.AddressLine).IsRequired().HasMaxLength(250);
        entity.Property(x => x.City).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Country).IsRequired().HasMaxLength(2);
        entity.Property(x => x.Phone).IsRequired().HasMaxLength(30);
        entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
        entity.Property(x => x.PreferredLanguage).IsRequired().HasMaxLength(10);

        // One profile per business account.
        entity.HasIndex(x => x.OwnerUserId).IsUnique();

        entity.HasOne(x => x.Owner)
            .WithOne(x => x.BusinessProfile)
            .HasForeignKey<BusinessProfile>(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
