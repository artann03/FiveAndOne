using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace FiveAndOne.Infrastructure.Persistence.Configurations;

public class SportConfiguration : IEntityTypeConfiguration<Sport>
{
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Sport> entity)
    {
        entity.ToTable("Sports", t =>
            t.HasCheckConstraint("CK_Sports_Participants", "\"DefaultMinParticipants\" >= 1 AND \"DefaultMaxParticipants\" >= \"DefaultMinParticipants\""));
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Slug).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Description).HasMaxLength(1000);
        entity.Property(x => x.Category).HasConversion<int>();
        entity.Property(x => x.IconUrl).HasMaxLength(500);
        entity.Property(x => x.MetadataJson).HasColumnType("jsonb");

        entity.HasIndex(x => x.Name).IsUnique();
        entity.HasIndex(x => x.Slug).IsUnique();

        // Base catalog. Fixed ids so the seed is stable across migrations; admins can add more later.
        entity.HasData(
            Seed(1, "Football", "football", SportCategory.TeamSport, 10, 22),
            Seed(2, "Futsal", "futsal", SportCategory.TeamSport, 6, 12),
            Seed(3, "Basketball", "basketball", SportCategory.TeamSport, 2, 10),
            Seed(4, "Tennis", "tennis", SportCategory.Racket, 2, 4),
            Seed(5, "Padel", "padel", SportCategory.Racket, 2, 4),
            Seed(6, "Fitness", "fitness", SportCategory.Individual, 1, 20),
            Seed(7, "Volleyball", "volleyball", SportCategory.TeamSport, 4, 12),
            Seed(8, "Badminton", "badminton", SportCategory.Racket, 2, 4),
            Seed(9, "Swimming", "swimming", SportCategory.Water, 1, 10),
            Seed(10, "Table Tennis", "table-tennis", SportCategory.Racket, 2, 4),
            Seed(11, "Martial Arts", "martial-arts", SportCategory.Individual, 1, 20));
    }

    private static Sport Seed(int n, string name, string slug, SportCategory category, int min, int max) => new()
    {
        Id = Guid.Parse($"10000000-0000-0000-0000-{n:D12}"),
        Name = name,
        Slug = slug,
        Category = category,
        IsActive = true,
        SortOrder = n,
        DefaultMinParticipants = min,
        DefaultMaxParticipants = max,
        CreatedAt = SeededAt
    };
}
