using FiveAndOne.Application.Common.Abstractions;
using System.Diagnostics;

namespace FiveAndOne.API.Infrastructure;

/// <summary>Per-request values for the application layer, read from the current HTTP request.</summary>
public sealed class HttpRequestContext : IRequestContext
{
    public const string DeviceHeader = "X-Device-Id";
    private static readonly string[] Supported = { "sq", "en", "tr", "sr" };

    private readonly IHttpContextAccessor _accessor;

    public HttpRequestContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Http => _accessor.HttpContext;

    public string? CorrelationId => Activity.Current?.Id ?? Http?.TraceIdentifier;

    public string? IpAddress => Http?.Connection.RemoteIpAddress?.ToString();

    /// <summary>Optional random id the frontend keeps in local storage (guest abuse controls; stored hashed only).</summary>
    public string? DeviceId
    {
        get
        {
            var value = Http?.Request.Headers[DeviceHeader].ToString();
            return string.IsNullOrWhiteSpace(value) || value.Length > 100 ? null : value.Trim();
        }
    }

    /// <summary>First supported language from Accept-Language (sq, en, tr, sr); Albanian by default.</summary>
    public string Language
    {
        get
        {
            var header = Http?.Request.Headers.AcceptLanguage.ToString();
            if (string.IsNullOrWhiteSpace(header))
            {
                return "sq";
            }

            foreach (var part in header.Split(','))
            {
                var code = part.Split(';')[0].Trim().ToLowerInvariant();
                if (code.Length >= 2 && Supported.Contains(code[..2]))
                {
                    return code[..2];
                }
            }

            return "sq";
        }
    }
}
