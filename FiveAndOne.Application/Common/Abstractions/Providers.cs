namespace FiveAndOne.Application.Common.Abstractions;

// External providers. Each one has a mock implementation in the API project (Providers/),
// selected in Program.cs. Swap the registration for a real provider when one is chosen.

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

public interface ISmsSender
{
    Task SendAsync(string phone, string message, CancellationToken cancellationToken = default);
}

public interface IFileStorage
{
    /// <summary>Stores the file and returns its public URL.</summary>
    Task<string> SaveAsync(Stream content, string folder, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file previously returned by <see cref="SaveAsync"/>. Unknown URLs are ignored.</summary>
    Task DeleteAsync(string url, CancellationToken cancellationToken = default);
}

public record GeocodingResult(string Label, string? AddressLine, string? City, string Country, decimal Latitude, decimal Longitude);

public interface IGeocodingService
{
    Task<IReadOnlyList<GeocodingResult>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

public record CheckoutRequest(Guid PaymentId, decimal Amount, string Currency, string Description, string? CustomerEmail);

public record CheckoutSession(string ProviderReference, string CheckoutUrl);

public record ChargeResult(bool Succeeded, string? ProviderReference, string? FailureReason);

public record PaymentWebhookEvent(string ProviderReference, Guid? PaymentId, bool Succeeded, string? FailureReason);

public interface IPaymentGateway
{
    string Name { get; }

    /// <summary>Creates a hosted checkout page; the result only becomes final through the webhook.</summary>
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>Charges a saved payment method (subscription renewals).</summary>
    Task<ChargeResult> ChargeRenewalAsync(Guid paymentId, Guid businessId, decimal amount, string currency, CancellationToken cancellationToken = default);

    Task<bool> RefundAsync(string providerReference, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>Validates the provider's signature and parses the webhook body. Null = invalid.</summary>
    PaymentWebhookEvent? ParseWebhook(string body, IReadOnlyDictionary<string, string> headers);
}

/// <summary>Per-request information the application layer needs (implemented in the API).</summary>
public interface IRequestContext
{
    string? CorrelationId { get; }
    string? IpAddress { get; }
    string? DeviceId { get; }
    string Language { get; }
}
