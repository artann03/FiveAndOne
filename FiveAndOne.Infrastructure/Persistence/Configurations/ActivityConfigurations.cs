using FiveAndOne.Domain.Entities.Activities;
using FiveAndOne.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FiveAndOne.Infrastructure.Persistence.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> entity)
    {
        entity.ToTable("Activities", t =>
        {
            t.HasCheckConstraint("CK_Activities_Time", "\"EndsAt\" > \"StartsAt\"");
            t.HasCheckConstraint("CK_Activities_Participants",
                "\"MinParticipants\" >= 1 AND \"MaxParticipants\" >= \"MinParticipants\" AND \"ParticipantCount\" >= 0 AND \"ParticipantCount\" <= \"MaxParticipants\"");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Title).IsRequired().HasMaxLength(150);
        entity.Property(x => x.Description).HasMaxLength(2000);
        entity.Property(x => x.LocationName).HasMaxLength(150);
        entity.Property(x => x.Address).HasMaxLength(250);
        entity.Property(x => x.Latitude).HasPrecision(9, 6);
        entity.Property(x => x.Longitude).HasPrecision(9, 6);
        entity.Property(x => x.SkillLevel).HasConversion<int>();
        entity.Property(x => x.Visibility).HasConversion<int>();
        entity.Property(x => x.JoinPolicy).HasConversion<int>();
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.PricePerParticipant).HasPrecision(18, 2);
        entity.Property(x => x.Currency).HasMaxLength(3);
        entity.Property(x => x.CancellationReason).HasMaxLength(500);
        entity.Property(x => x.Version).IsRowVersion();   // maps to Postgres xmin

        entity.HasIndex(x => x.BookingId).IsUnique();
        entity.HasIndex(x => new { x.Status, x.Visibility, x.StartsAt });
        entity.HasIndex(x => new { x.SportId, x.StartsAt });
        entity.HasIndex(x => x.OrganizerUserId);
        entity.HasIndex(x => x.FacilityId);

        entity.HasOne(x => x.Organizer)
            .WithMany(x => x.OrganizedActivities)
            .HasForeignKey(x => x.OrganizerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Booking)
            .WithOne()
            .HasForeignKey<Activity>(x => x.BookingId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(x => x.Facility)
            .WithMany()
            .HasForeignKey(x => x.FacilityId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ActivityParticipantConfiguration : IEntityTypeConfiguration<ActivityParticipant>
{
    public void Configure(EntityTypeBuilder<ActivityParticipant> entity)
    {
        entity.ToTable("ActivityParticipants");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.Attendance).HasConversion<int>();

        entity.HasIndex(x => new { x.ActivityId, x.UserId }).IsUnique();
        entity.HasIndex(x => new { x.UserId, x.Status });

        entity.HasOne(x => x.Activity)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.User)
            .WithMany(x => x.ActivityParticipations)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.InvitedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
