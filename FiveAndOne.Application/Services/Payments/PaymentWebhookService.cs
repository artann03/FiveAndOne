using FiveAndOne.Application.Common.Abstractions;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Services.Subscriptions;
using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Payments;

/// <summary>
/// Trusted payment results from the provider (story 43: never activate based on frontend success).
/// Routes subscription and reservation payments to their own services.
/// </summary>
public interface IPaymentWebhookService
{
    Task HandleAsync(string body, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);

    /// <summary>Development only (mock provider): simulates the provider's webhook for a payment.</summary>
    Task SimulateAsync(Guid paymentId, bool succeeded, CancellationToken cancellationToken = default);
}

public class PaymentWebhookService : IPaymentWebhookService
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly ISubscriptionService _subscriptions;
    private readonly IReservationPaymentService _reservations;

    public PaymentWebhookService(ApplicationDbContext db, IPaymentGateway gateway, ISubscriptionService subscriptions, IReservationPaymentService reservations)
    {
        _db = db;
        _gateway = gateway;
        _subscriptions = subscriptions;
        _reservations = reservations;
    }

    public async Task HandleAsync(string body, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        var evt = _gateway.ParseWebhook(body, headers)
            ?? throw new UnauthorizedException("Invalid webhook signature.", "invalid_signature");

        var payment = await _db.Payments.FirstOrDefaultAsync(p =>
                (evt.PaymentId != null && p.Id == evt.PaymentId) || (p.Provider == _gateway.Name && p.ProviderReference == evt.ProviderReference), cancellationToken)
            ?? throw new NotFoundException("Payment not found.");

        await ApplyAsync(payment, evt.Succeeded, evt.FailureReason, cancellationToken);
    }

    public async Task SimulateAsync(Guid paymentId, bool succeeded, CancellationToken cancellationToken = default)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException("Payment not found.");

        if (payment.Provider != "mock")
        {
            throw new ForbiddenException("Only mock payments can be simulated.");
        }

        await ApplyAsync(payment, succeeded, succeeded ? null : "Card declined (simulated)", cancellationToken);
    }

    private async Task ApplyAsync(Payment payment, bool succeeded, string? failureReason, CancellationToken cancellationToken)
    {
        if (payment.Purpose == PaymentPurpose.Subscription)
        {
            await _subscriptions.ApplyPaymentResultAsync(payment, succeeded, failureReason, cancellationToken);
        }
        else
        {
            await _reservations.ApplyResultAsync(payment, succeeded, failureReason, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
