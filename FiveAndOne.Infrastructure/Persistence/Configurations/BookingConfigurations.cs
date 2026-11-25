using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FiveAndOne.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    // The no-overlap EXCLUDE constraint can't be expressed in EF's model API;
    // it is added with raw SQL in the MultiSportSchema migration (EX_Bookings_NoOverlap).
    public void Configure(EntityTypeBuilder<Booking> entity)
    {
        entity.ToTable("Bookings", t =>
        {
            t.HasCheckConstraint("CK_Bookings_Time", "\"EndsAt\" > \"StartsAt\" AND \"BlockedUntil\" >= \"EndsAt\"");
            t.HasCheckConstraint("CK_Bookings_Spots", "\"Spots\" > 0");
            t.HasCheckConstraint("CK_Bookings_Price", "\"Price\" >= 0");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.BookingNumber).IsRequired().HasMaxLength(20);
        entity.Property(x => x.Price).HasPrecision(18, 2);
        entity.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.PaymentStatus).HasConversion<int>();
        entity.Property(x => x.ConfirmationMode).HasConversion<int>();
        entity.Property(x => x.CancellationReason).HasMaxLength(500);
        entity.Property(x => x.CustomerNotes).HasMaxLength(500);
        entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
        entity.Property(x => x.Version).IsRowVersion();   // maps to Postgres xmin

        entity.HasIndex(x => x.BookingNumber).IsUnique();
        entity.HasIndex(x => new { x.CustomerUserId, x.IdempotencyKey }).IsUnique();
        entity.HasIndex(x => new { x.CustomerUserId, x.StartsAt });
        entity.HasIndex(x => new { x.FacilityId, x.Status, x.StartsAt });
        entity.HasIndex(x => x.SessionId);
        entity.HasIndex(x => new { x.Status, x.ExpiresAt });   // pending-expiry job

        entity.HasOne(x => x.Customer)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Facility)
            .WithMany()
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Session)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
{
    public void Configure(EntityTypeBuilder<BookingStatusHistory> entity)
    {
        entity.ToTable("BookingStatusHistory");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.FromStatus).HasConversion<int>();
        entity.Property(x => x.ToStatus).HasConversion<int>();
        entity.Property(x => x.Reason).HasMaxLength(500);

        entity.HasIndex(x => new { x.BookingId, x.ChangedAt });

        entity.HasOne(x => x.Booking)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
