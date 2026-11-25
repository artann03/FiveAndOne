namespace FiveAndOne.Domain.Entities.Identity;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    // Only a SHA-256 hash of the token is stored; the raw value is only ever sent to the client.
    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Set when this token was rotated (used to get a new one). Re-using a rotated token is treated as theft.
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;
}
