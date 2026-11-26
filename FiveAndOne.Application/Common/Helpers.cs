using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FiveAndOne.Application.Common;

/// <summary>Random secrets and their stored hashes (refresh tokens, guest links, invitations, codes).</summary>
public static class SecureTokens
{
    public static string Create(int bytes = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Numeric one-time code, e.g. for SMS verification.</summary>
    public static string NumericCode(int digits = 6) =>
        RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, digits)).ToString(new string('0', digits));

    /// <summary>Short human-friendly reference without look-alike characters, e.g. "SX-7K3M9Q".</summary>
    public static string Reference(string prefix, int length = 6)
    {
        const string alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }
        return $"{prefix}-{new string(chars)}";
    }
}

public static class Slugs
{
    /// <summary>"Futsal Pitch 1 – Prishtinë" -> "futsal-pitch-1-prishtine-3f9a1c". The id suffix keeps it unique.</summary>
    public static string Create(string name, Guid id, string fallback = "item")
    {
        var normalized = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        var slug = Regex.Replace(ascii, "[^a-z0-9]+", "-").Trim('-');

        if (slug.Length > 120)
        {
            slug = slug[..120].Trim('-');
        }

        var suffix = id.ToString("N")[..6];
        return string.IsNullOrEmpty(slug) ? $"{fallback}-{suffix}" : $"{slug}-{suffix}";
    }
}

public static class Text
{
    public static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Keeps digits and a leading '+' so "+383 44 123-456" and "+38344123456" compare equal.</summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var trimmed = phone.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return trimmed.StartsWith('+') ? "+" + digits : digits;
    }

    public static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

/// <summary>
/// Converts between UTC instants (stored) and a facility's local date/time (shown and entered).
/// Handles daylight-saving changes: times that don't exist (spring forward) are reported as invalid.
/// </summary>
public static class LocalTime
{
    private static readonly Dictionary<string, TimeZoneInfo> Cache = new();
    private static readonly object Lock = new();

    public static TimeZoneInfo Zone(string timeZoneId)
    {
        lock (Lock)
        {
            if (!Cache.TryGetValue(timeZoneId, out var zone))
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                Cache[timeZoneId] = zone;
            }
            return zone;
        }
    }

    /// <summary>Local wall-clock time to UTC. Null if that local time doesn't exist (DST gap).</summary>
    public static DateTime? ToUtc(DateOnly date, TimeOnly time, string timeZoneId) =>
        ToUtc(date.ToDateTime(time), timeZoneId);

    public static DateTime? ToUtc(DateTime localDateTime, string timeZoneId)
    {
        var zone = Zone(timeZoneId);
        var local = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            return null;
        }

        // Ambiguous times (autumn fall-back) resolve to the first occurrence (daylight time).
        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            var offset = offsets.Max();
            return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public static DateTime ToLocal(DateTime utc, string timeZoneId) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone(timeZoneId));

    public static DateOnly Today(string timeZoneId) => DateOnly.FromDateTime(ToLocal(DateTime.UtcNow, timeZoneId));

    /// <summary>UTC range [start, end) covering one local calendar day.</summary>
    public static (DateTime Start, DateTime End) DayRangeUtc(DateOnly date, string timeZoneId)
    {
        var start = ToUtc(date.ToDateTime(TimeOnly.MinValue), timeZoneId)
                    ?? ToUtc(date.ToDateTime(new TimeOnly(1, 0)), timeZoneId)!.Value;
        var next = date.AddDays(1);
        var end = ToUtc(next.ToDateTime(TimeOnly.MinValue), timeZoneId)
                  ?? ToUtc(next.ToDateTime(new TimeOnly(1, 0)), timeZoneId)!.Value;
        return (start, end);
    }
}

public static class Paging
{
    public static (int Page, int PageSize) Normalize(int page, int pageSize, int max = 100) =>
        (Math.Max(page, 1), Math.Clamp(pageSize, 1, max));
}
