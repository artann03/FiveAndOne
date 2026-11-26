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

    // Plain-text templates in every supported language (stories 51, 60). Unknown languages use English.
    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["sq"] = new()
        {
            ["created"] = "Rezervim i ri",
            ["cancelled_by_customer"] = "Rezervimi u anulua nga klienti",
            ["cancelled_by_business"] = "Rezervimi u anulua nga biznesi",
            ["rescheduled_by_customer"] = "Rezervimi u ndryshua nga klienti",
            ["rescheduled_by_business"] = "Rezervimi u ndryshua nga biznesi",
            ["update"] = "Përditësim i rezervimit",
            ["reference"] = "Referenca", ["facility"] = "Objekti", ["sport"] = "Sporti",
            ["when"] = "Data dhe ora", ["previous"] = "Më parë"
        },
        ["en"] = new()
        {
            ["created"] = "New reservation",
            ["cancelled_by_customer"] = "Reservation cancelled by the customer",
            ["cancelled_by_business"] = "Reservation cancelled by the business",
            ["rescheduled_by_customer"] = "Reservation changed by the customer",
            ["rescheduled_by_business"] = "Reservation changed by the business",
            ["update"] = "Reservation update",
            ["reference"] = "Reference", ["facility"] = "Facility", ["sport"] = "Sport",
            ["when"] = "Date and time", ["previous"] = "Previously"
        },
        ["tr"] = new()
        {
            ["created"] = "Yeni rezervasyon",
            ["cancelled_by_customer"] = "Rezervasyon müşteri tarafından iptal edildi",
            ["cancelled_by_business"] = "Rezervasyon işletme tarafından iptal edildi",
            ["rescheduled_by_customer"] = "Rezervasyon müşteri tarafından değiştirildi",
            ["rescheduled_by_business"] = "Rezervasyon işletme tarafından değiştirildi",
            ["update"] = "Rezervasyon güncellemesi",
            ["reference"] = "Referans", ["facility"] = "Tesis", ["sport"] = "Spor",
            ["when"] = "Tarih ve saat", ["previous"] = "Önceki"
        },
        ["sr"] = new()
        {
            ["created"] = "Nova rezervacija",
            ["cancelled_by_customer"] = "Kupac je otkazao rezervaciju",
            ["cancelled_by_business"] = "Objekat je otkazao rezervaciju",
            ["rescheduled_by_customer"] = "Kupac je promenio rezervaciju",
            ["rescheduled_by_business"] = "Objekat je promenio rezervaciju",
            ["update"] = "Izmena rezervacije",
            ["reference"] = "Referenca", ["facility"] = "Objekat", ["sport"] = "Sport",
            ["when"] = "Datum i vreme", ["previous"] = "Ranije"
        }
    };

    private static (string Subject, string Body) Template(string language, string evt, string reference, string facility, string sport,
        string when, string? previous, string link)
    {
        var t = Texts.TryGetValue(language, out var texts) ? texts : Texts["en"];
        var title = t.TryGetValue(evt, out var value) ? value : t["update"];

        var lines = new List<string>
        {
            title,
            string.Empty,
            $"{t["reference"]}: {reference}",
            $"{t["facility"]}: {facility}",
            $"{t["sport"]}: {sport}",
            $"{t["when"]}: {when}"
        };

        if (previous is not null)
        {
            lines.Add($"{t["previous"]}: {previous}");
        }

        if (!string.IsNullOrEmpty(link))
        {
            lines.Add(string.Empty);
            lines.Add(link);
        }

        return ($"{title} – {reference}", string.Join('\n', lines));
    }
}
