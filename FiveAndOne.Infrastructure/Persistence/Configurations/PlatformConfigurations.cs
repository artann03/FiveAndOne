using FiveAndOne.Domain.Entities.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;

namespace FiveAndOne.Infrastructure.Persistence.Configurations;

/// <summary>Well-known feature flag keys (seeded). Post-MVP features start disabled.</summary>
public static class FeatureFlagKeys
{
    public const string GuestBooking = "guest_booking";
    public const string CustomerEmailNotifications = "customer_email_notifications";
    public const string SmsNotifications = "sms_notifications";
    public const string SmsVerification = "sms_verification";
    public const string CustomerPayments = "customer_payments";
    public const string Reviews = "reviews";
    public const string PlayerProfiles = "player_profiles";
    public const string Teams = "teams";
    public const string MultipleBusinesses = "multiple_businesses";
    public const string StaffInvitations = "staff_invitations";
    public const string Venues = "venues";
    public const string Recommendations = "recommendations";
    public const string PromotedListings = "promoted_listings";
}

/// <summary>Well-known platform setting keys (seeded with the PRD defaults).</summary>
public static class PlatformSettingKeys
{
    public const string MaxReservationsPerDay = "booking.max_reservations_per_day";
    public const string MaxActiveReservations = "booking.max_active_reservations";
    public const string GuestMaxReservationsPerDay = "guest.max_reservations_per_day";
    public const string GuestMaxAttemptsPerHour = "guest.max_attempts_per_hour";
    public const string GraceDays = "subscription.grace_days";
    public const string RetentionYears = "subscription.retention_years";
    public const string ReviewWindowDays = "reviews.window_days";
}

public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<FeatureFlag> entity)
    {
        entity.ToTable("FeatureFlags");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Key).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Description).HasMaxLength(500);
        entity.Property(x => x.ConfigJson).HasColumnType("jsonb");

        entity.HasIndex(x => x.Key).IsUnique();

        entity.HasData(
            Seed(1, FeatureFlagKeys.GuestBooking, "Guests can reserve without an account.", true),
            Seed(2, FeatureFlagKeys.CustomerEmailNotifications, "Email confirmations/updates to customers (post-MVP).", false),
            Seed(3, FeatureFlagKeys.SmsNotifications, "SMS notifications to customers (post-MVP).", false),
            Seed(4, FeatureFlagKeys.SmsVerification, "Risk-triggered SMS phone verification for guest bookings (post-MVP).", false),
            Seed(5, FeatureFlagKeys.CustomerPayments, "Online payment for reservations (post-MVP).", false),
            Seed(6, FeatureFlagKeys.Reviews, "Ratings and reviews (post-MVP).", false),
            Seed(7, FeatureFlagKeys.PlayerProfiles, "Customer player profiles (post-MVP).", false),
            Seed(8, FeatureFlagKeys.Teams, "Teammate discovery and teams (post-MVP).", false),
            Seed(9, FeatureFlagKeys.MultipleBusinesses, "One user managing several businesses (needs product approval).", false),
            Seed(10, FeatureFlagKeys.StaffInvitations, "Invite staff with permissions (post-MVP).", false),
            Seed(11, FeatureFlagKeys.Venues, "Parent venues grouping several courts (post-MVP).", false),
            Seed(12, FeatureFlagKeys.Recommendations, "Personal facility recommendations (post-MVP).", false),
            Seed(13, FeatureFlagKeys.PromotedListings, "Sponsored facilities in discovery (post-MVP).", false));
    }

    private static FeatureFlag Seed(int n, string key, string description, bool enabled) => new()
    {
        Id = Guid.Parse($"40000000-0000-0000-0000-{n:D12}"),
        Key = key,
        Description = description,
        IsEnabled = enabled,
        CreatedAt = SeededAt
    };
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.ToTable("AuditLogs");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.ActorRole).HasMaxLength(30);
        entity.Property(x => x.Action).IsRequired().HasMaxLength(100);
        entity.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
        entity.Property(x => x.EntityId).HasMaxLength(100);
        entity.Property(x => x.Summary).HasMaxLength(500);
        entity.Property(x => x.DataJson).HasColumnType("jsonb");
        entity.Property(x => x.CorrelationId).HasMaxLength(100);

        entity.HasIndex(x => x.CreatedAt);
        entity.HasIndex(x => new { x.EntityType, x.EntityId });
        entity.HasIndex(x => x.ActorUserId);
        entity.HasIndex(x => x.Action);
        entity.HasIndex(x => x.BusinessId);
    }
}

public class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<PlatformSetting> entity)
    {
        entity.ToTable("PlatformSettings");
        entity.HasKey(x => x.Key);

        entity.Property(x => x.Key).HasMaxLength(100);
        entity.Property(x => x.Value).IsRequired().HasMaxLength(1000);
        entity.Property(x => x.Description).HasMaxLength(500);

        entity.HasData(
            Seed(PlatformSettingKeys.MaxReservationsPerDay, "2", "Max reservations a customer can hold on the same day (cancelled ones don't count)."),
            Seed(PlatformSettingKeys.MaxActiveReservations, "2", "Max upcoming reservations a customer can hold at a time."),
            Seed(PlatformSettingKeys.GuestMaxReservationsPerDay, "2", "Max guest reservations per phone number / device per day."),
            Seed(PlatformSettingKeys.GuestMaxAttemptsPerHour, "10", "Guest booking attempts per IP/device per hour before requests are refused."),
            Seed(PlatformSettingKeys.GraceDays, "14", "Days a business keeps full access after a failed subscription renewal."),
            Seed(PlatformSettingKeys.RetentionYears, "2", "Years an unpaid (inactive) business's data is kept before anonymization."),
            Seed(PlatformSettingKeys.ReviewWindowDays, "30", "Days after a completed reservation during which a review can be written."));
    }

    private static PlatformSetting Seed(string key, string value, string description) => new()
    {
        Key = key,
        Value = value,
        Description = description,
        UpdatedAt = SeededAt
    };
}

public class TranslationConfiguration : IEntityTypeConfiguration<Translation>
{
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Translation> entity)
    {
        entity.ToTable("Translations");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.EntityType).IsRequired().HasMaxLength(50);
        entity.Property(x => x.Field).IsRequired().HasMaxLength(50);
        entity.Property(x => x.Language).IsRequired().HasMaxLength(10);
        entity.Property(x => x.Value).IsRequired().HasMaxLength(1000);

        entity.HasIndex(x => new { x.EntityType, x.EntityId, x.Field, x.Language }).IsUnique();

        // English is the base text on the record itself; these are sq / tr / sr names for the seeded catalogue.
        var rows = new List<Translation>();
        var n = 0;

        void Add(string type, string idPrefix, int item, string sq, string tr, string sr)
        {
            var entityId = Guid.Parse($"{idPrefix}-0000-0000-0000-{item:D12}");
            foreach (var (lang, value) in new[] { ("sq", sq), ("tr", tr), ("sr", sr) })
            {
                n++;
                rows.Add(new Translation
                {
                    Id = Guid.Parse($"30000000-0000-0000-0000-{n:D12}"),
                    EntityType = type,
                    EntityId = entityId,
                    Field = "Name",
                    Language = lang,
                    Value = value,
                    CreatedAt = SeededAt
                });
            }
        }

        const string sport = "10000000";
        Add("Sport", sport, 1, "Futboll", "Futbol", "Fudbal");
        Add("Sport", sport, 2, "Futsall", "Futsal", "Futsal");
        Add("Sport", sport, 3, "Basketboll", "Basketbol", "Košarka");
        Add("Sport", sport, 4, "Tenis", "Tenis", "Tenis");
        Add("Sport", sport, 5, "Padel", "Padel", "Padel");
        Add("Sport", sport, 6, "Fitnes", "Fitness", "Fitnes");
        Add("Sport", sport, 7, "Volejboll", "Voleybol", "Odbojka");
        Add("Sport", sport, 8, "Badminton", "Badminton", "Badminton");
        Add("Sport", sport, 9, "Not", "Yüzme", "Plivanje");
        Add("Sport", sport, 10, "Pingpong", "Masa tenisi", "Stoni tenis");
        Add("Sport", sport, 11, "Arte marciale", "Dövüş sanatları", "Borilačke veštine");

        const string amenity = "20000000";
        Add("Amenity", amenity, 1, "Parking", "Otopark", "Parking");
        Add("Amenity", amenity, 2, "Dhoma zhveshjeje", "Soyunma odaları", "Svlačionice");
        Add("Amenity", amenity, 3, "Dushe", "Duşlar", "Tuševi");
        Add("Amenity", amenity, 4, "Dollapë", "Dolaplar", "Ormarići");
        Add("Amenity", amenity, 5, "Ndriçim", "Projektör aydınlatma", "Reflektori");
        Add("Amenity", amenity, 6, "Kafene / Bar", "Kafe / Bar", "Kafić / Bar");
        Add("Amenity", amenity, 7, "Qiramarrje pajisjesh", "Ekipman kiralama", "Iznajmljivanje opreme");
        Add("Amenity", amenity, 8, "Ulëse për spektatorë", "Seyirci oturma alanı", "Mesta za gledaoce");
        Add("Amenity", amenity, 9, "Wi-Fi", "Wi-Fi", "Wi-Fi");
        Add("Amenity", amenity, 10, "Kondicioner", "Klima", "Klima uređaj");
        Add("Amenity", amenity, 11, "Ngrohje", "Isıtma", "Grejanje");
        Add("Amenity", amenity, 12, "Ndihma e parë", "İlk yardım", "Prva pomoć");

        entity.HasData(rows);
    }
}

public class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> entity)
    {
        entity.ToTable("NotificationMessages");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Channel).HasConversion<int>();
        entity.Property(x => x.Status).HasConversion<int>();
        entity.Property(x => x.Recipient).IsRequired().HasMaxLength(256);
        entity.Property(x => x.Subject).HasMaxLength(300);
        entity.Property(x => x.Body).IsRequired();
        entity.Property(x => x.Template).IsRequired().HasMaxLength(100);
        entity.Property(x => x.LastError).HasMaxLength(1000);

        entity.HasIndex(x => new { x.Status, x.CreatedAt });
        entity.HasIndex(x => x.BusinessId);
        entity.HasIndex(x => x.BookingId);
    }
}

public class PhoneVerificationConfiguration : IEntityTypeConfiguration<PhoneVerification>
{
    public void Configure(EntityTypeBuilder<PhoneVerification> entity)
    {
        entity.ToTable("PhoneVerifications");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Phone).IsRequired().HasMaxLength(30);
        entity.Property(x => x.CodeHash).IsRequired().HasMaxLength(128);
        entity.Property(x => x.VerificationTokenHash).HasMaxLength(128);

        entity.HasIndex(x => new { x.Phone, x.CreatedAt });
        entity.HasIndex(x => x.VerificationTokenHash).IsUnique().HasFilter("\"VerificationTokenHash\" IS NOT NULL");
    }
}
