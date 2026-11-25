namespace FiveAndOne.Application.Common.Settings;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;

    // HMAC-SHA256 signing key, at least 32 bytes. Keep it out of source control outside of Development.
    public string Key { get; set; } = default!;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
