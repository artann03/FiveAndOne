using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace FiveAndOne.Infrastructure.Persistence.Configurations;

public class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> entity)
    {
        entity.ToTable("Facilities", t =>
        {
            t.HasCheckConstraint("CK_Facilities_Capacity", "\"Capacity\" IS NULL OR \"Capacity\" > 0");
            t.HasCheckConstraint("CK_Facilities_Buffer", "\"BufferMinutes\" >= 0");
            t.HasCheckConstraint("CK_Facilities_Durations",
                "\"MinDurationMinutes\" IS NULL OR \"MaxDurationMinutes\" IS NULL OR \"MaxDurationMinutes\" >= \"MinDurationMinutes\"");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Name).IsRequired().HasMaxLength(150);
        entity.Property(x => x.Slug).IsRequired().HasMaxLength(180);
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.Type).HasConversion<int>();
        entity.Property(x => x.Surface).HasMaxLength(50);
        entity.Property(x => x.AddressLine).HasMaxLength(250);
        entity.Property(x => x.City).HasMaxLength(100);
        entity.Property(x => x.Country).IsRequired().HasMaxLength(2);
        entity.Property(x => x.Latitude).HasPrecision(9, 6);
        entity.Property(x => x.Longitude).HasPrecision(9, 6);
        entity.Property(x => x.TimeZone).IsRequired().HasMaxLength(64);
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.SuspensionReason).HasMaxLength(500);
        entity.Property(x => x.BookingMode).HasConversion<int>();
        entity.Property(x => x.ConfirmationMode).HasConversion<int>();
        entity.Property(x => x.Version).IsRowVersion();   // maps to Postgres xmin

        entity.HasIndex(x => x.Slug).IsUnique();
        entity.HasIndex(x => new { x.OwnerUserId, x.Status });
        entity.HasIndex(x => new { x.Status, x.City });
        entity.HasIndex(x => new { x.Latitude, x.Longitude });

        entity.HasOne(x => x.Owner)
            .WithMany(x => x.OwnedFacilities)
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FacilityImageConfiguration : IEntityTypeConfiguration<FacilityImage>
{
    public void Configure(EntityTypeBuilder<FacilityImage> entity)
    {
        entity.ToTable("FacilityImages");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Url).IsRequired().HasMaxLength(500);

        entity.HasIndex(x => new { x.FacilityId, x.SortOrder });

        entity.HasOne(x => x.Facility)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Amenity> entity)
    {
        entity.ToTable("Amenities");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Icon).HasMaxLength(100);

        entity.HasIndex(x => x.Name).IsUnique();

        // Base list owners pick from. Fixed ids so the seed is stable across migrations.
        entity.HasData(
            Seed(1, "Parking", "parking"),
            Seed(2, "Changing rooms", "changing-room"),
            Seed(3, "Showers", "shower"),
            Seed(4, "Lockers", "locker"),
            Seed(5, "Floodlights", "floodlight"),
            Seed(6, "Café / Bar", "cafe"),
            Seed(7, "Equipment rental", "equipment"),
            Seed(8, "Spectator seating", "seating"),
            Seed(9, "Wi-Fi", "wifi"),
            Seed(10, "Air conditioning", "air-conditioning"),
            Seed(11, "Heating", "heating"),
            Seed(12, "First aid", "first-aid"));
    }

    private static Amenity Seed(int n, string name, string icon) => new()
    {
        Id = Guid.Parse($"20000000-0000-0000-0000-{n:D12}"),
        Name = name,
        Icon = icon,
        CreatedAt = SeededAt
    };
}

public class FacilityAmenityConfiguration : IEntityTypeConfiguration<FacilityAmenity>
{
    public void Configure(EntityTypeBuilder<FacilityAmenity> entity)
    {
        entity.ToTable("FacilityAmenities");
        entity.HasKey(x => new { x.FacilityId, x.AmenityId });

        entity.HasIndex(x => x.AmenityId);

        entity.HasOne(x => x.Facility)
            .WithMany(x => x.Amenities)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Amenity)
            .WithMany(x => x.Facilities)
            .HasForeignKey(x => x.AmenityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FacilitySportConfiguration : IEntityTypeConfiguration<FacilitySport>
{
    public void Configure(EntityTypeBuilder<FacilitySport> entity)
    {
        entity.ToTable("FacilitySports");
        entity.HasKey(x => new { x.FacilityId, x.SportId });

        entity.HasIndex(x => x.SportId);

        entity.HasOne(x => x.Facility)
            .WithMany(x => x.Sports)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FacilityMemberConfiguration : IEntityTypeConfiguration<FacilityMember>
{
    public void Configure(EntityTypeBuilder<FacilityMember> entity)
    {
        entity.ToTable("FacilityMembers");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Permissions).HasConversion<int>();

        entity.HasIndex(x => new { x.FacilityId, x.UserId }).IsUnique();
        entity.HasIndex(x => x.UserId);

        entity.HasOne(x => x.Facility)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.User)
            .WithMany(x => x.FacilityMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FacilityOperatingHoursConfiguration : IEntityTypeConfiguration<FacilityOperatingHours>
{
    public void Configure(EntityTypeBuilder<FacilityOperatingHours> entity)
    {
        entity.ToTable("FacilityOperatingHours");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.DayOfWeek).HasConversion<int>();

        entity.HasIndex(x => new { x.FacilityId, x.DayOfWeek });

        entity.HasOne(x => x.Facility)
            .WithMany(x => x.OperatingHours)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FacilityPriceRuleConfiguration : IEntityTypeConfiguration<FacilityPriceRule>
{
    public void Configure(EntityTypeBuilder<FacilityPriceRule> entity)
    {
        entity.ToTable("FacilityPriceRules", t =>
            t.HasCheckConstraint("CK_FacilityPriceRules_Amount", "\"Amount\" >= 0"));
        entity.HasKey(x => x.Id);

        entity.Property(x => x.DayOfWeek).HasConversion<int>();
        entity.Property(x => x.PriceType).HasConversion<int>();
        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        entity.HasIndex(x => new { x.FacilityId, x.DayOfWeek });

        entity.HasOne(x => x.Facility)
            .WithMany(x => x.PriceRules)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FacilitySessionConfiguration : IEntityTypeConfiguration<FacilitySession>
{
    public void Configure(EntityTypeBuilder<FacilitySession> entity)
    {
        entity.ToTable("FacilitySessions", t =>
        {
            t.HasCheckConstraint("CK_FacilitySessions_Time", "\"EndsAt\" > \"StartsAt\"");
            t.HasCheckConstraint("CK_FacilitySessions_Capacity", "\"Capacity\" > 0 AND \"BookedCount\" >= 0 AND \"BookedCount\" <= \"Capacity\"");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.PricePerSpot).HasPrecision(18, 2);
        entity.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.Version).IsRowVersion();   // maps to Postgres xmin

        entity.HasIndex(x => new { x.FacilityId, x.StartsAt });

        entity.HasOne(x => x.Facility)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AvailabilityBlockConfiguration : IEntityTypeConfiguration<AvailabilityBlock>
{
    public void Configure(EntityTypeBuilder<AvailabilityBlock> entity)
    {
        entity.ToTable("AvailabilityBlocks", t =>
            t.HasCheckConstraint("CK_AvailabilityBlocks_Time", "\"EndsAt\" > \"StartsAt\""));
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Type).HasConversion<int>();
        entity.Property(x => x.Reason).HasMaxLength(500);

        entity.HasIndex(x => new { x.FacilityId, x.StartsAt, x.EndsAt });

        entity.HasOne(x => x.Facility)
            .WithMany(x => x.AvailabilityBlocks)
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
