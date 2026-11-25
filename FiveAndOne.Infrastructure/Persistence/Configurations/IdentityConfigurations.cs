using FiveAndOne.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FiveAndOne.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        entity.Property(x => x.LastName).HasMaxLength(100);
        entity.Property(x => x.Username).IsRequired().HasMaxLength(50);
        entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
        entity.Property(x => x.PhoneNumber).HasMaxLength(30);
        entity.Property(x => x.PasswordHash).IsRequired();
        entity.Property(x => x.Role).HasConversion<int>().IsRequired();
        entity.Property(x => x.Status).HasConversion<int>().IsRequired();
        entity.Property(x => x.ProfileImageUrl).HasMaxLength(500);
        entity.Property(x => x.City).HasMaxLength(100);
        entity.Property(x => x.Bio).HasMaxLength(500);

        entity.HasIndex(x => x.Username).IsUnique();
        entity.HasIndex(x => x.Email).IsUnique();
        entity.HasIndex(x => x.Role);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.ToTable("RefreshTokens");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);

        entity.HasIndex(x => x.TokenHash).IsUnique();
        entity.HasIndex(x => x.UserId);

        entity.HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserSportProfileConfiguration : IEntityTypeConfiguration<UserSportProfile>
{
    public void Configure(EntityTypeBuilder<UserSportProfile> entity)
    {
        entity.ToTable("UserSportProfiles");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.SkillLevel).HasConversion<int>();
        entity.Property(x => x.PreferredPosition).HasMaxLength(50);

        entity.HasIndex(x => new { x.UserId, x.SportId }).IsUnique();

        entity.HasOne(x => x.User)
            .WithMany(x => x.SportProfiles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
