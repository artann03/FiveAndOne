using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Platform;

/// <summary>Backend-managed on/off switch (story 49). Never a replacement for authorization.</summary>
public class FeatureFlag : BaseEntity
{
    public string Key { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    // Optional JSON configuration for the feature (jsonb).
    public string? ConfigJson { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>Who did what to which record (story 50). No secrets or unnecessary personal data.</summary>
public class AuditLog : BaseEntity
{
    public Guid? ActorUserId { get; set; }
    public string? ActorRole { get; set; }
    public string Action { get; set; } = default!;        // e.g. "facility.suspended"
    public string EntityType { get; set; } = default!;    // e.g. "Facility"
    public string? EntityId { get; set; }
    public string? Summary { get; set; }
    public string? DataJson { get; set; }                 // small before/after details (jsonb)
    public string? CorrelationId { get; set; }
    public Guid? BusinessId { get; set; }
}

/// <summary>Key/value platform settings editable by super admins (booking limits, guest controls, ...).</summary>
public class PlatformSetting
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>Translated text for catalogue records, e.g. a sport's name in Albanian (story 51).</summary>
public class Translation : BaseEntity
{
    public string EntityType { get; set; } = default!;   // "Sport", "Amenity"
    public Guid EntityId { get; set; }
    public string Field { get; set; } = default!;        // "Name", "Description"
    public string Language { get; set; } = default!;     // sq, en, tr, sr
    public string Value { get; set; } = default!;
}

/// <summary>Outbox of emails/SMS. Rows are written with the business change and sent by a background worker.</summary>
public class NotificationMessage : BaseEntity
{
    public NotificationChannel Channel { get; set; }
    public string Recipient { get; set; } = default!;
    public string? Subject { get; set; }
    public string Body { get; set; } = default!;
    public string Template { get; set; } = default!;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? SentAt { get; set; }
    public Guid? BusinessId { get; set; }
    public Guid? BookingId { get; set; }
}

/// <summary>One-time SMS code for risk-triggered phone verification (story 61).</summary>
public class PhoneVerification : BaseEntity
{
    public string Phone { get; set; } = default!;
    public string CodeHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? VerifiedAt { get; set; }
    // Returned after a successful check and sent with the booking; stored hashed.
    public string? VerificationTokenHash { get; set; }
    public DateTime? TokenUsedAt { get; set; }
}
