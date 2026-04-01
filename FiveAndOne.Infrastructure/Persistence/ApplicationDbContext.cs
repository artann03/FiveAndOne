using FiveAndOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<GamePost> GamePosts => Set<GamePost>();
    public DbSet<GameParticipant> GameParticipants => Set<GameParticipant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUser(modelBuilder);
        ConfigureGamePost(modelBuilder);
        ConfigureGameParticipant(modelBuilder);
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.LastName)
                .HasMaxLength(100);

            entity.Property(x => x.Username)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.PhoneNumber)
                .HasMaxLength(30);

            entity.Property(x => x.PasswordHash)
                .IsRequired();

            entity.Property(x => x.ProfileImageUrl)
                .HasMaxLength(500);

            entity.Property(x => x.City)
                .HasMaxLength(100);

            entity.Property(x => x.Bio)
                .HasMaxLength(500);

            entity.HasIndex(x => x.Username)
                .IsUnique();

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.HasMany(x => x.CreatedGamePosts)
                .WithOne(x => x.CreatedByUser)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.GameParticipants)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureGamePost(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GamePost>(entity =>
        {
            entity.ToTable("GamePosts");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Description)
                .HasMaxLength(1000);

            entity.Property(x => x.PlayersPerTeam)
                .IsRequired();

            entity.Property(x => x.TotalSlots)
                .IsRequired();

            entity.Property(x => x.PricePerPlayer)
                .HasPrecision(18, 2);

            entity.Property(x => x.Currency)
                .HasMaxLength(10);

            entity.Property(x => x.Latitude)
                .HasPrecision(9, 6);

            entity.Property(x => x.Longitude)
                .HasPrecision(9, 6);

            entity.Property(x => x.LocationName)
                .HasMaxLength(150);

            entity.Property(x => x.Address)
                .HasMaxLength(250);

            entity.Property(x => x.Status)
                .HasConversion<int>();

            entity.Property(x => x.Visibility)
                .HasConversion<int>();

            entity.HasIndex(x => x.CreatedByUserId);

            entity.HasIndex(x => x.GameDate);

            entity.HasIndex(x => x.Status);

            entity.HasIndex(x => new { x.Status, x.GameDate });

            entity.HasMany(x => x.Participants)
                .WithOne(x => x.GamePost)
                .HasForeignKey(x => x.GamePostId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureGameParticipant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameParticipant>(entity =>
        {
            entity.ToTable("GameParticipants");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status)
                .HasConversion<int>();

            entity.Property(x => x.TeamSide)
                .HasConversion<int>();

            entity.HasIndex(x => x.UserId);

            entity.HasIndex(x => new { x.GamePostId, x.UserId })
                .IsUnique();
        });
    }
}