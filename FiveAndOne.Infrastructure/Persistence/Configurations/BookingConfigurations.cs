using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FiveAndOne.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    // The no-overlap EXCLUDE constraint (EX_Bookings_NoOverlap) can't be expressed in EF's model API;
    // it is created with raw SQL in the migrations.
    public void Configure(EntityTypeBuilder<Booking> entity)
    {
        entity.ToTable("Bookings", t =>
        {
            t.HasCheckConstraint("CK_Bookings_Time", "\"EndsAt\" > \"StartsAt\" AND \"BlockedUntil\" >= \"EndsAt\"");
            t.HasCheckConstraint("CK_Bookings_Price", "\"Price\" >= 0");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.BookingNumber).IsRequired().HasMaxLength(20);
        entity.Property(x => x.Source).HasConversion<int>();
        entity.Property(x => x.ExternalChannel).HasConversion<int>();
        entity.Property(x => x.ContactName).HasMaxLength(150);
        entity.Property(x => x.ContactPhone).HasMaxLength(30);
        entity.Property(x => x.ContactEmail).HasMaxLength(256);
        entity.Property(x => x.GuestAccessTokenHash).HasMaxLength(128);
        entity.Property(x => x.Price).HasPrecision(18, 2);
        entity.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.PaymentStatus).HasConversion<int>();
        entity.Property(x => x.CancellationReason).HasMaxLength(500);
        entity.Property(x => x.CustomerNotes).HasMaxLength(500);
        entity.Property(x => x.InternalNotes).HasMaxLength(1000);
        entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
        entity.Property(x => x.DeviceHash).HasMaxLength(128);
        entity.Property(x => x.IpHash).HasMaxLength(128);
        entity.Property(x => x.Version).IsRowVersion();   // maps to Postgres xmin

        entity.Ignore(x => x.HoldsSlot);
        entity.Ignore(x => x.DurationMinutes);

        entity.HasIndex(x => x.BookingNumber).IsUnique();
        entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        entity.HasIndex(x => x.GuestAccessTokenHash).IsUnique().HasFilter("\"GuestAccessTokenHash\" IS NOT NULL");
        entity.HasIndex(x => new { x.CustomerUserId, x.StartsAt });
        entity.HasIndex(x => new { x.FacilityId, x.StartsAt });
        entity.HasIndex(x => new { x.BusinessId, x.StartsAt });
        entity.HasIndex(x => new { x.ContactPhone, x.CreatedAt });
        entity.HasIndex(x => new { x.DeviceHash, x.CreatedAt });
        entity.HasIndex(x => new { x.Status, x.ExpiresAt });

        entity.HasOne(x => x.Customer)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Facility)
            .WithMany()
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class BookingHistoryConfiguration : IEntityTypeConfiguration<BookingHistory>
{
    public void Configure(EntityTypeBuilder<BookingHistory> entity)
    {
        entity.ToTable("BookingHistory");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.EventType).HasConversion<int>();
        entity.Property(x => x.FromStatus).HasConversion<int>();
        entity.Property(x => x.ToStatus).HasConversion<int>();
        entity.Property(x => x.Reason).HasMaxLength(500);

        entity.HasIndex(x => new { x.BookingId, x.ChangedAt });

        entity.HasOne(x => x.Booking)
            .WithMany(x => x.History)
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
