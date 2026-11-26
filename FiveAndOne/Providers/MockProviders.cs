using FiveAndOne.Application.Common.Abstractions;
using FiveAndOne.Application.Common.Settings;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FiveAndOne.API.Providers;

// =====================================================================
// MOCK PROVIDERS — development stand-ins. Replace the registrations in Program.cs
// with real implementations when providers are chosen (see docs/OPEN_QUESTIONS.md).
// =====================================================================

/// <summary>Writes emails to the log instead of sending them. Real options: Brevo, Postmark, Amazon SES.</summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MOCK EMAIL] To: {To} | Subject: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

/// <summary>Writes SMS to the log. Real options: Infobip (strong Balkan coverage), Twilio.</summary>
public sealed class LoggingSmsSender : ISmsSender
{
    private readonly ILogger<LoggingSmsSender> _logger;

    public LoggingSmsSender(ILogger<LoggingSmsSender> logger) => _logger = logger;

    public Task SendAsync(string phone, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MOCK SMS] To: {Phone} | {Message}", phone, message);
        return Task.CompletedTask;
    }
}

/// <summary>Stores uploads under wwwroot/uploads (served by this API). Real options: Cloudflare R2, Amazon S3, Cloudinary.</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IWebHostEnvironment environment)
    {
        _root = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string folder, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        // Only our own generated names are used, never client file names, so paths can't be manipulated.
        var safeFolder = string.Join('/', folder.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Sanitize));
        var directory = Path.Combine(_root, safeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, Sanitize(fileName));
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);

        return $"/uploads/{safeFolder}/{Sanitize(fileName)}";
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!url.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            return Task.CompletedTask;   // external URL: nothing stored here
        }

        var relative = url["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (full.StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal) && File.Exists(full))
        {
            File.Delete(full);
        }
        return Task.CompletedTask;
    }

    private static string Sanitize(string value) =>
        new(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray());
}

/// <summary>
/// Offline geocoder for Kosovo cities. Real options: Google Maps Geocoding, Mapbox, or Nominatim (OpenStreetMap).
/// The real provider must be called from the backend so its API key never reaches the browser.
/// </summary>
public sealed class MockGeocodingService : IGeocodingService
{
    private static readonly (string City, decimal Lat, decimal Lng)[] Cities =
    {
        ("Prishtinë", 42.6629m, 21.1655m), ("Prizren", 42.2139m, 20.7397m), ("Pejë", 42.6593m, 20.2887m),
        ("Gjakovë", 42.3803m, 20.4308m), ("Ferizaj", 42.3706m, 21.1553m), ("Gjilan", 42.4635m, 21.4694m),
        ("Mitrovicë", 42.8914m, 20.8660m), ("Podujevë", 42.9106m, 21.1903m), ("Vushtrri", 42.8231m, 20.9675m),
        ("Suharekë", 42.3580m, 20.8250m), ("Rahovec", 42.3994m, 20.6547m), ("Lipjan", 42.5217m, 21.1258m)
    };

    public Task<IReadOnlyList<GeocodingResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = Normalize(query);
        IReadOnlyList<GeocodingResult> results = Cities
            .Where(c => q.Contains(Normalize(c.City)) || Normalize(c.City).StartsWith(q, StringComparison.Ordinal))
            .Select(c => new GeocodingResult($"{(query.Contains(',') ? query.Split(',')[0].Trim() + ", " : string.Empty)}{c.City}, Kosovo",
                query.Contains(',') ? query.Split(',')[0].Trim() : null, c.City, "XK", c.Lat, c.Lng))
            .Take(5)
            .ToList();
        return Task.FromResult(results);
    }

    private static string Normalize(string value) =>
        value.ToLowerInvariant().Replace('ë', 'e').Replace('ç', 'c').Trim();
}

/// <summary>
/// Fake payment provider. Checkout URLs point to a development page of this API where you choose
/// "pay" or "fail"; the result then arrives through the same webhook path a real provider would use.
/// Real options for a Kosovo business: local bank e-commerce acquiring (e.g. via a PSP), Paysera, 2Checkout.
/// Stripe does not support Kosovo-registered businesses.
/// </summary>
public sealed class MockPaymentGateway : IPaymentGateway
{
    public const string SignatureHeader = "X-Mock-Signature";

    private readonly AppSettings _app;
    private readonly PaymentSettings _settings;

    public MockPaymentGateway(IOptions<AppSettings> app, IOptions<PaymentSettings> settings)
    {
        _app = app.Value;
        _settings = settings.Value;
    }

    public string Name => "mock";

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CheckoutSession($"mock_{request.PaymentId:N}", $"{_app.ApiBaseUrl}/api/payments/mock/{request.PaymentId}"));

    public Task<ChargeResult> ChargeRenewalAsync(Guid paymentId, Guid businessId, decimal amount, string currency, CancellationToken cancellationToken = default) =>
        Task.FromResult(_settings.MockRenewalSucceeds
            ? new ChargeResult(true, $"mock_{paymentId:N}", null)
            : new ChargeResult(false, $"mock_{paymentId:N}", "Card declined (mock)"));

    public Task<bool> RefundAsync(string providerReference, decimal amount, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public PaymentWebhookEvent? ParseWebhook(string body, IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue(SignatureHeader, out var signature) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature), Encoding.UTF8.GetBytes(Sign(body, _settings.WebhookSecret))))
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<MockWebhookPayload>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return payload is null ? null : new PaymentWebhookEvent(payload.Reference, payload.PaymentId, payload.Succeeded, payload.FailureReason);
    }

    public static string Sign(string body, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));

    private sealed record MockWebhookPayload(string Reference, Guid? PaymentId, bool Succeeded, string? FailureReason);
}
