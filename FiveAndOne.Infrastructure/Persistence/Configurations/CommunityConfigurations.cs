using FiveAndOne.Domain.Entities.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FiveAndOne.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> entity)
    {
        entity.ToTable("Reviews", t => t.HasCheckConstraint("CK_Reviews_Rating", "\"Rating\" BETWEEN 1 AND 5"));
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Comment).HasMaxLength(2000);
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.ModerationNote).HasMaxLength(500);
        entity.Property(x => x.BusinessReply).HasMaxLength(2000);

        // One review per reservation.
        entity.HasIndex(x => x.BookingId).IsUnique();
        entity.HasIndex(x => new { x.FacilityId, x.Status, x.CreatedAt });
        entity.HasIndex(x => x.UserId);

        entity.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> entity)
    {
        entity.ToTable("ContentReports");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.TargetType).HasConversion<int>();
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.Reason).IsRequired().HasMaxLength(1000);
        entity.Property(x => x.ResolutionNote).HasMaxLength(500);

        entity.HasIndex(x => new { x.Status, x.CreatedAt });
        entity.HasIndex(x => new { x.TargetType, x.TargetId });
        entity.HasIndex(x => new { x.ReporterUserId, x.TargetType, x.TargetId }).IsUnique();
    }
}

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> entity)
    {
        entity.ToTable("Teams", t => t.HasCheckConstraint("CK_Teams_MaxMembers", "\"MaxMembers\" BETWEEN 2 AND 100"));
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Slug).IsRequired().HasMaxLength(130);
        entity.Property(x => x.Description).HasMaxLength(1000);
        entity.Property(x => x.City).HasMaxLength(100);
        entity.Property(x => x.Visibility).HasConversion<int>();

        entity.HasIndex(x => x.Slug).IsUnique();
        entity.HasIndex(x => new { x.SportId, x.City });

        entity.HasOne(x => x.Sport).WithMany().HasForeignKey(x => x.SportId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Captain).WithMany().HasForeignKey(x => x.CaptainUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> entity)
    {
        entity.ToTable("TeamMembers");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Role).HasConversion<int>();
        entity.Property(x => x.Status).HasConversion<int>();

        entity.HasIndex(x => new { x.TeamId, x.UserId }).IsUnique();
        entity.HasIndex(x => x.UserId);

        entity.HasOne(x => x.Team).WithMany(x => x.Members).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FacilityPromotionConfiguration : IEntityTypeConfiguration<FacilityPromotion>
{
    public void Configure(EntityTypeBuilder<FacilityPromotion> entity)
    {
        entity.ToTable("FacilityPromotions", t =>
            t.HasCheckConstraint("CK_FacilityPromotions_Time", "\"EndsAt\" > \"StartsAt\""));
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Label).IsRequired().HasMaxLength(50);

        entity.HasIndex(x => new { x.IsActive, x.StartsAt, x.EndsAt });

        entity.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Cascade);
    }
}
