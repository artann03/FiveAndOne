using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FiveAndOne.Application.Services.Bookings;

/// <summary>
/// Reservation emails. Business notifications are MVP (story 38); customer emails (story 60) and SMS (story 61)
/// only go out when their feature flags are on. Messages go to the outbox and are sent by a background worker.
/// </summary>
public interface IBookingNotifier
{
    Task CreatedAsync(Booking booking, CancellationToken cancellationToken = default);
    Task CancelledAsync(Booking booking, bool byBusiness, CancellationToken cancellationToken = default);
    Task RescheduledAsync(Booking booking, DateTime oldStart, bool byBusiness, CancellationToken cancellationToken = default);
}

public class BookingNotifier : IBookingNotifier
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IFeatureFlags _flags;
    private readonly AppSettings _app;

    public BookingNotifier(ApplicationDbContext db, INotificationService notifications, IFeatureFlags flags, IOptions<AppSettings> app)
    {
        _db = db;
        _notifications = notifications;
        _flags = flags;
        _app = app.Value;
    }

    public Task CreatedAsync(Booking booking, CancellationToken cancellationToken = default) =>
        SendAsync(booking, "created", null, notifyBusiness: booking.Source == BookingSource.SportX, cancellationToken);

    public Task CancelledAsync(Booking booking, bool byBusiness, CancellationToken cancellationToken = default) =>
        SendAsync(booking, byBusiness ? "cancelled_by_business" : "cancelled_by_customer", null, notifyBusiness: true, cancellationToken);

    public Task RescheduledAsync(Booking booking, DateTime oldStart, bool byBusiness, CancellationToken cancellationToken = default) =>
        SendAsync(booking, byBusiness ? "rescheduled_by_business" : "rescheduled_by_customer", oldStart, notifyBusiness: true, cancellationToken);

    private async Task SendAsync(Booking booking, string evt, DateTime? oldStart, bool notifyBusiness, CancellationToken cancellationToken)
    {
        var info = await _db.Facilities.AsNoTracking()
            .Where(f => f.Id == booking.FacilityId)
            .Select(f => new
            {
                f.Name,
                f.TimeZone,
                BusinessEmail = f.Business.Email,
                BusinessLanguage = f.Business.PreferredLanguage,
                Sport = _db.Sports.Where(s => s.Id == booking.SportId).Select(s => s.Name).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (info is null)
        {
            return;
        }

        var start = LocalTime.ToLocal(booking.StartsAt, info.TimeZone);
        var end = LocalTime.ToLocal(booking.EndsAt, info.TimeZone);
        var when = $"{start:dd.MM.yyyy HH:mm}–{end:HH:mm}";
        var previous = oldStart.HasValue ? LocalTime.ToLocal(oldStart.Value, info.TimeZone).ToString("dd.MM.yyyy HH:mm") : null;

        // Business: only the reservation reference and logistics, no unnecessary personal data.
        if (notifyBusiness && booking.Source == BookingSource.SportX)
        {
            var (subject, body) = Template(info.BusinessLanguage, evt, booking.BookingNumber, info.Name, info.Sport ?? string.Empty, when, previous,
                link: $"{_app.FrontendBaseUrl}/business/reservations/{booking.Id}");
            _notifications.QueueEmail(info.BusinessEmail, subject, body, $"business.reservation_{evt}", booking.BusinessId, booking.Id);
        }

        // Customer copy (post-MVP, behind a feature flag).
        if (booking.Source == BookingSource.SportX && !string.IsNullOrWhiteSpace(booking.ContactEmail)
            && await _flags.IsEnabledAsync(FeatureFlagKeys.CustomerEmailNotifications, cancellationToken))
        {
            var language = booking.CustomerUserId.HasValue
                ? await _db.Users.Where(u => u.Id == booking.CustomerUserId).Select(u => u.PreferredLanguage).FirstOrDefaultAsync(cancellationToken) ?? "sq"
                : "sq";
            var link = booking.IsGuest ? $"{_app.FrontendBaseUrl}/reservations/guest" : $"{_app.FrontendBaseUrl}/account/reservations/{booking.Id}";
            var (subject, body) = Template(language, evt, booking.BookingNumber, info.Name, info.Sport ?? string.Empty, when, previous, link);
            _notifications.QueueEmail(booking.ContactEmail!, subject, body, $"customer.reservation_{evt}", booking.BusinessId, booking.Id);
        }

        if (booking.Source == BookingSource.SportX && !string.IsNullOrWhiteSpace(booking.ContactPhone)
            && await _flags.IsEnabledAsync(FeatureFlagKeys.SmsNotifications, cancellationToken))
        {
            var (subject, _) = Template("sq", evt, booking.BookingNumber, info.Name, info.Sport ?? string.Empty, when, previous, string.Empty);
            _notifications.QueueSms(booking.ContactPhone!, $"SportX: {subject} – {info.Name}, {when}", $"sms.reservation_{evt}", booking.BusinessId, booking.Id);
        }
    }

    /// <summary>Plain-text templates (sq / en; tr and sr fall back to en until translations are provided).</summary>
    private static (string Subject, string Body) Template(string language, string evt, string reference, string facility, string sport,
        string when, string? previous, string link)
    {
        var sq = language == "sq";
        var title = evt switch
        {
            "created" => sq ? "Rezervim i ri" : "New reservation",
            "cancelled_by_customer" => sq ? "Rezervimi u anulua nga klienti" : "Reservation cancelled by the customer",
            "cancelled_by_business" => sq ? "Rezervimi u anulua nga biznesi" : "Reservation cancelled by the business",
            "rescheduled_by_customer" => sq ? "Rezervimi u ndryshua nga klienti" : "Reservation changed by the customer",
            "rescheduled_by_business" => sq ? "Rezervimi u ndryshua nga biznesi" : "Reservation changed by the business",
            _ => sq ? "Përditësim i rezervimit" : "Reservation update"
        };

        var lines = new List<string>
        {
            title,
            string.Empty,
            $"{(sq ? "Referenca" : "Reference")}: {reference}",
            $"{(sq ? "Objekti" : "Facility")}: {facility}",
            $"{(sq ? "Sporti" : "Sport")}: {sport}",
            $"{(sq ? "Data dhe ora" : "Date and time")}: {when}"
        };

        if (previous is not null)
        {
            lines.Add($"{(sq ? "Më parë" : "Previously")}: {previous}");
        }

        if (!string.IsNullOrEmpty(link))
        {
            lines.Add(string.Empty);
            lines.Add(link);
        }

        return ($"{title} – {reference}", string.Join('\n', lines));
    }
}
