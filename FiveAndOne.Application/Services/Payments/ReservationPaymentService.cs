using FiveAndOne.Application.Common.Abstractions;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Services.Bookings;
using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FiveAndOne.Application.Services.Payments;

/// <summary>
/// Online payment for reservations (story 62, post-MVP, behind the "customer_payments" flag).
/// Kept separate from business subscription payments. A pending reservation holds its slot until ExpiresAt.
/// </summary>
public interface IReservationPaymentService
{
    /// <summary>Creates the payment + checkout for a new pending reservation. Returns the checkout URL (caller saves).</summary>
    Task<string> StartAsync(Booking booking, string description, CancellationToken cancellationToken = default);

    /// <summary>Applies a verified payment result from the provider webhook.</summary>
    Task ApplyResultAsync(Payment payment, bool succeeded, string? failureReason, CancellationToken cancellationToken = default);

    /// <summary>Refunds a paid reservation that was cancelled (caller saves).</summary>
    Task RefundIfPaidAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>Releases slots of reservations whose payment window ran out. Returns how many expired.</summary>
    Task<int> ExpireUnpaidAsync(CancellationToken cancellationToken = default);
}

public class ReservationPaymentService : IReservationPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly IBookingLifecycle _lifecycle;
    private readonly IBookingNotifier _notifier;
    private readonly PaymentSettings _settings;

    public ReservationPaymentService(ApplicationDbContext db, IPaymentGateway gateway, IBookingLifecycle lifecycle, IBookingNotifier notifier, IOptions<PaymentSettings> settings)
    {
        _db = db;
        _gateway = gateway;
        _lifecycle = lifecycle;
        _notifier = notifier;
        _settings = settings.Value;
    }

    public async Task<string> StartAsync(Booking booking, string description, CancellationToken cancellationToken = default)
    {
        var payment = new Payment
        {
            Purpose = PaymentPurpose.Reservation,
            BusinessId = booking.BusinessId,
            BookingId = booking.Id,
            PayerUserId = booking.CustomerUserId,
            Amount = booking.Price,
            Currency = booking.Currency,
            Provider = _gateway.Name
        };

        var session = await _gateway.CreateCheckoutAsync(
            new CheckoutRequest(payment.Id, payment.Amount, payment.Currency, $"Reservation {booking.BookingNumber} – {description}", booking.ContactEmail),
            cancellationToken);

        payment.ProviderReference = session.ProviderReference;
        payment.CheckoutUrl = session.CheckoutUrl;
        _db.Payments.Add(payment);

        booking.ExpiresAt = DateTime.UtcNow.AddMinutes(_settings.ReservationPaymentHoldMinutes);
        return session.CheckoutUrl;
    }

    public async Task ApplyResultAsync(Payment payment, bool succeeded, string? failureReason, CancellationToken cancellationToken = default)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == payment.BookingId, cancellationToken);
        if (booking is null || payment.Status != PaymentRecordStatus.Pending)
        {
            return;   // already processed (webhooks can arrive more than once)
        }

        var now = DateTime.UtcNow;
        if (succeeded && booking.Status == BookingStatus.Pending)
        {
            payment.Status = PaymentRecordStatus.Succeeded;
            payment.PaidAt = now;
            booking.PaymentStatus = PaymentStatus.Paid;
            booking.ExpiresAt = null;
            await _lifecycle.ChangeStatusAsync(booking, BookingStatus.Confirmed, null, "Payment received", cancellationToken);
            await _notifier.CreatedAsync(booking, cancellationToken);
        }
        else if (succeeded)
        {
            // Paid after the hold expired: the slot may be gone, so refund.
            payment.Status = PaymentRecordStatus.Succeeded;
            payment.PaidAt = now;
            if (payment.ProviderReference is not null && await _gateway.RefundAsync(payment.ProviderReference, payment.Amount, cancellationToken))
            {
                payment.Status = PaymentRecordStatus.Refunded;
                payment.RefundedAt = now;
            }
        }
        else
        {
            payment.Status = PaymentRecordStatus.Failed;
            payment.FailureReason = failureReason;
            booking.PaymentStatus = PaymentStatus.Failed;
            if (booking.Status == BookingStatus.Pending)
            {
                await _lifecycle.ChangeStatusAsync(booking, BookingStatus.Expired, null, "Payment failed", cancellationToken);
            }
        }

        payment.UpdatedAt = now;
    }

    public async Task RefundIfPaidAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        if (booking.PaymentStatus != PaymentStatus.Paid)
        {
            return;
        }

        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id && p.Status == PaymentRecordStatus.Succeeded, cancellationToken);
        if (payment?.ProviderReference is null)
        {
            return;
        }

        // Refund policy (MVP of story 62): full refund when cancelled within the allowed window.
        if (await _gateway.RefundAsync(payment.ProviderReference, payment.Amount, cancellationToken))
        {
            payment.Status = PaymentRecordStatus.Refunded;
            payment.RefundedAt = DateTime.UtcNow;
            booking.PaymentStatus = PaymentStatus.Refunded;
        }
    }

    public async Task<int> ExpireUnpaidAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.Bookings
            .Where(b => b.Status == BookingStatus.Pending && b.ExpiresAt != null && b.ExpiresAt < now)
            .Take(200)
            .ToListAsync(cancellationToken);

        foreach (var booking in expired)
        {
            await _lifecycle.ChangeStatusAsync(booking, BookingStatus.Expired, null, "Payment not completed in time", cancellationToken);
            booking.PaymentStatus = PaymentStatus.Failed;

            var payments = await _db.Payments.Where(p => p.BookingId == booking.Id && p.Status == PaymentRecordStatus.Pending).ToListAsync(cancellationToken);
            foreach (var payment in payments)
            {
                payment.Status = PaymentRecordStatus.Cancelled;
                payment.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
