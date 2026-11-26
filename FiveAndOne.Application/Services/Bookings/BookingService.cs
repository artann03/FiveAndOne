using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Abstractions;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Bookings;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Services.Payments;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Bookings;

/// <summary>Customer and guest reservations (stories 29-37).</summary>
public interface IBookingService
{
    /// <param name="customerId">Signed-in customer, or null for a guest.</param>
    Task<ReservationResponse> CreateAsync(Guid? customerId, CreateReservationRequest request, string? idempotencyKey, CancellationToken cancellationToken = default);

    Task<PagedResponse<ReservationResponse>> GetMineAsync(Guid customerId, string? scope, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ReservationResponse> GetMineAsync(Guid customerId, Guid bookingId, CancellationToken cancellationToken = default);
    Task<ReservationResponse> CancelAsync(Guid customerId, Guid bookingId, string? reason, CancellationToken cancellationToken = default);
    Task<ReservationResponse> RescheduleAsync(Guid customerId, Guid bookingId, RescheduleReservationRequest request, CancellationToken cancellationToken = default);

    Task<ReservationResponse> GetGuestAsync(string token, CancellationToken cancellationToken = default);
    Task<ReservationResponse> CancelGuestAsync(string token, string? reason, CancellationToken cancellationToken = default);
}

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _db;
    private readonly ReservationSupport _support;
    private readonly IBookingLifecycle _lifecycle;
    private readonly IBookingNotifier _notifier;
    private readonly IPlatformSettings _settings;
    private readonly IFeatureFlags _flags;
    private readonly IRequestContext _request;
    private readonly IReservationPaymentService _payments;
    private readonly IAuditService _audit;

    public BookingService(ApplicationDbContext db, ReservationSupport support, IBookingLifecycle lifecycle, IBookingNotifier notifier,
        IPlatformSettings settings, IFeatureFlags flags, IRequestContext request, IReservationPaymentService payments, IAuditService audit)
    {
        _db = db;
        _support = support;
        _lifecycle = lifecycle;
        _notifier = notifier;
        _settings = settings;
        _flags = flags;
        _request = request;
        _payments = payments;
        _audit = audit;
    }

    // =====================================================================
    // Create (stories 29-32)
    // =====================================================================

    public async Task<ReservationResponse> CreateAsync(Guid? customerId, CreateReservationRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var key = Text.NullIfEmpty(idempotencyKey);
        if (key is { Length: > 100 })
        {
            throw new BadRequestException("Idempotency-Key can be at most 100 characters.", "invalid_idempotency_key");
        }

        var deviceHash = _request.DeviceId is null ? null : SecureTokens.Hash("device:" + _request.DeviceId);
        var ipHash = _request.IpAddress is null ? null : SecureTokens.Hash("ip:" + _request.IpAddress);
        var guestPhone = Text.NormalizePhone(request.Guest?.Phone);

        // Duplicate submission (story 31): the same key returns the reservation created the first time.
        if (key is not null)
        {
            var existing = await _db.Bookings.FirstOrDefaultAsync(b => b.IdempotencyKey == key, cancellationToken);
            if (existing is not null)
            {
                return await ReplayAsync(existing, customerId, guestPhone, cancellationToken);
            }
        }

        var facility = await _support.LoadBookableFacilityAsync(request.FacilityId!.Value, cancellationToken);
        var sportId = request.SportId!.Value;
        var duration = request.DurationMinutes!.Value;

        Domain.Entities.Identity.User? customer = null;
        if (customerId.HasValue)
        {
            customer = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == customerId, cancellationToken);
            if (customer is null || !customer.IsActive)
            {
                throw new UnauthorizedException("User no longer exists.");
            }
        }
        else
        {
            await _flags.EnsureEnabledAsync(FeatureFlagKeys.GuestBooking, cancellationToken);
            if (request.Guest is null || guestPhone is null || string.IsNullOrWhiteSpace(request.Guest.Name))
            {
                throw RequestValidationException.For("guest", "Enter your name and phone number, or log in to book.", "guest_details_required");
            }
        }

        var slot = await _support.ValidateCustomerSlotAsync(facility, sportId, request.StartsAt!.Value, duration, null, cancellationToken);

        if (customer is not null)
        {
            await EnforceCustomerLimitsAsync(customer.Id, facility.TimeZone, slot.StartsAt, null, cancellationToken);
        }
        else
        {
            await EnforceGuestControlsAsync(guestPhone!, deviceHash, ipHash, facility.TimeZone, slot.StartsAt, request.PhoneVerificationToken, cancellationToken);
        }

        var requiresPayment = facility.RequiresOnlinePayment && slot.Quote.Amount > 0
                              && await _flags.IsEnabledAsync(FeatureFlagKeys.CustomerPayments, cancellationToken);

        var booking = new Booking
        {
            BookingNumber = await _support.NewBookingNumberAsync(cancellationToken),
            Source = BookingSource.SportX,
            BusinessId = facility.BusinessId,
            FacilityId = facility.Id,
            SportId = sportId,
            CustomerUserId = customer?.Id,
            IsGuest = customer is null,
            ContactName = customer is null ? request.Guest!.Name!.Trim() : $"{customer.FirstName} {customer.LastName}".Trim(),
            ContactPhone = customer is null ? guestPhone : customer.PhoneNumber,
            ContactEmail = customer is null ? Text.NullIfEmpty(request.Guest!.Email)?.ToLowerInvariant() : customer.Email,
            StartsAt = slot.StartsAt,
            EndsAt = slot.EndsAt,
            BlockedUntil = slot.BlockedUntil,
            Price = slot.Quote.Amount,
            Currency = slot.Quote.Currency,
            Status = requiresPayment ? BookingStatus.Pending : BookingStatus.Confirmed,
            PaymentStatus = requiresPayment ? PaymentStatus.Pending : PaymentStatus.NotRequired,
            CustomerNotes = Text.NullIfEmpty(request.Notes),
            IdempotencyKey = key,
            CreatedByUserId = customer?.Id,
            DeviceHash = customer is null ? deviceHash : null,
            IpHash = customer is null ? ipHash : null
        };

        string? guestToken = null;
        if (booking.IsGuest)
        {
            guestToken = SecureTokens.Create();
            booking.GuestAccessTokenHash = SecureTokens.Hash(guestToken);
        }

        _db.Bookings.Add(booking);
        _lifecycle.AddHistory(booking, BookingEventType.Created, null, booking.Status, customer?.Id, null);

        string? checkoutUrl = null;
        if (requiresPayment)
        {
            checkoutUrl = await _payments.StartAsync(booking, facility.Name, cancellationToken);
        }
        else
        {
            await _notifier.CreatedAsync(booking, cancellationToken);
        }

        _audit.Log(customer?.Id, "reservation.created", "Booking", booking.Id.ToString(), booking.BookingNumber, businessId: booking.BusinessId);

        try
        {
            await _support.SaveWithSlotGuardAsync(cancellationToken);
        }
        catch (DbUpdateException) when (key is not null)
        {
            // Two identical submits raced: the other one won on the unique idempotency key.
            _db.ChangeTracker.Clear();
            var winner = await _db.Bookings.FirstOrDefaultAsync(b => b.IdempotencyKey == key, cancellationToken);
            if (winner is null) throw;
            return await ReplayAsync(winner, customerId, guestPhone, cancellationToken);
        }

        var response = await MapAsync(booking.Id, cancellationToken);
        response.GuestAccessToken = guestToken;
        response.GuestAccessUrl = guestToken is null ? null : _support.GuestUrl(guestToken);
        response.CheckoutUrl = checkoutUrl;
        return response;
    }

    private async Task<ReservationResponse> ReplayAsync(Booking existing, Guid? customerId, string? guestPhone, CancellationToken cancellationToken)
    {
        var sameRequester = customerId.HasValue
            ? existing.CustomerUserId == customerId
            : existing.IsGuest && existing.ContactPhone == guestPhone;

        if (!sameRequester)
        {
            throw new ConflictException("This Idempotency-Key was already used for another request.", "idempotency_key_reused");
        }

        var response = await MapAsync(existing.Id, cancellationToken);
        if (existing.IsGuest)
        {
            // Only the hash is stored, so a replay issues a fresh access token (the old one stops working).
            var token = SecureTokens.Create();
            var tracked = await _db.Bookings.FirstAsync(b => b.Id == existing.Id, cancellationToken);
            tracked.GuestAccessTokenHash = SecureTokens.Hash(token);
            await _db.SaveChangesAsync(cancellationToken);
            response.GuestAccessToken = token;
            response.GuestAccessUrl = _support.GuestUrl(token);
        }

        return response;
    }

    /// <summary>Max reservations per day and max upcoming reservations (story 32). Cancelled ones don't count.</summary>
    private async Task EnforceCustomerLimitsAsync(Guid customerId, string timeZone, DateTime startsAt, Guid? excludeBookingId, CancellationToken cancellationToken)
    {
        var maxPerDay = await _settings.GetIntAsync(PlatformSettingKeys.MaxReservationsPerDay, 2, cancellationToken);
        var maxActive = await _settings.GetIntAsync(PlatformSettingKeys.MaxActiveReservations, 2, cancellationToken);

        var date = DateOnly.FromDateTime(LocalTime.ToLocal(startsAt, timeZone));
        var (dayStart, dayEnd) = LocalTime.DayRangeUtc(date, timeZone);
        var counted = new[] { BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.Completed, BookingStatus.NoShow };

        var sameDay = await _db.Bookings.CountAsync(b => b.CustomerUserId == customerId && b.Id != excludeBookingId
                                                         && counted.Contains(b.Status) && b.StartsAt >= dayStart && b.StartsAt < dayEnd, cancellationToken);
        if (sameDay >= maxPerDay)
        {
            throw new ConflictException($"You can have at most {maxPerDay} reservations on the same day.", "daily_limit_reached");
        }

        var now = DateTime.UtcNow;
        var active = await _db.Bookings.CountAsync(b => b.CustomerUserId == customerId && b.Id != excludeBookingId
                                                        && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) && b.EndsAt > now, cancellationToken);
        if (active >= maxActive)
        {
            throw new ConflictException($"You can have at most {maxActive} upcoming reservations at a time.", "active_limit_reached");
        }
    }

    /// <summary>
    /// Guest abuse controls (story 32): the same daily/active limits per phone number and per device, plus a
    /// rate limit per IP/device. Device and IP are stored only as hashes and are never the only identity check.
    /// </summary>
    private async Task EnforceGuestControlsAsync(string phone, string? deviceHash, string? ipHash, string timeZone, DateTime startsAt,
        string? verificationToken, CancellationToken cancellationToken)
    {
        var maxPerDay = await _settings.GetIntAsync(PlatformSettingKeys.GuestMaxReservationsPerDay, 2, cancellationToken);
        var maxActive = await _settings.GetIntAsync(PlatformSettingKeys.MaxActiveReservations, 2, cancellationToken);
        var maxAttempts = await _settings.GetIntAsync(PlatformSettingKeys.GuestMaxAttemptsPerHour, 10, cancellationToken);

        var now = DateTime.UtcNow;
        var hourAgo = now.AddHours(-1);
        var recent = await _db.Bookings.CountAsync(b => b.IsGuest && b.CreatedAt >= hourAgo
                                                        && ((deviceHash != null && b.DeviceHash == deviceHash) || (ipHash != null && b.IpHash == ipHash)), cancellationToken);
        if (recent >= maxAttempts)
        {
            throw new TooManyRequestsException("Too many reservations from this device. Please try again later or create an account.", "guest_rate_limited");
        }

        var date = DateOnly.FromDateTime(LocalTime.ToLocal(startsAt, timeZone));
        var (dayStart, dayEnd) = LocalTime.DayRangeUtc(date, timeZone);
        var guestBookings = _db.Bookings.Where(b => b.IsGuest
                                                    && b.Status != BookingStatus.CancelledByCustomer && b.Status != BookingStatus.CancelledByBusiness
                                                    && b.Status != BookingStatus.Expired
                                                    && (b.ContactPhone == phone || (deviceHash != null && b.DeviceHash == deviceHash)));

        if (await guestBookings.CountAsync(b => b.StartsAt >= dayStart && b.StartsAt < dayEnd, cancellationToken) >= maxPerDay)
        {
            throw new ConflictException($"You can have at most {maxPerDay} reservations on the same day.", "daily_limit_reached");
        }

        if (await guestBookings.CountAsync(b => (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) && b.EndsAt > now, cancellationToken) >= maxActive)
        {
            throw new ConflictException($"You can have at most {maxActive} upcoming reservations at a time.", "active_limit_reached");
        }

        // Risk-triggered SMS verification (story 61, post-MVP): a phone with recent no-shows, or a busy device.
        if (await _flags.IsEnabledAsync(FeatureFlagKeys.SmsVerification, cancellationToken))
        {
            var monthAgo = now.AddDays(-30);
            var suspicious = recent >= Math.Max(1, maxAttempts / 2)
                             || await _db.Bookings.AnyAsync(b => b.ContactPhone == phone && b.Status == BookingStatus.NoShow && b.StartsAt >= monthAgo, cancellationToken);

            if (suspicious && !await ConsumeVerificationAsync(phone, verificationToken, cancellationToken))
            {
                throw new ForbiddenException("Please verify your phone number to complete this reservation.", "phone_verification_required");
            }
        }
    }

    private async Task<bool> ConsumeVerificationAsync(string phone, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var hash = SecureTokens.Hash(token);
        var since = DateTime.UtcNow.AddMinutes(-30);
        var verification = await _db.PhoneVerifications.FirstOrDefaultAsync(v => v.VerificationTokenHash == hash && v.Phone == phone
                                                                                 && v.VerifiedAt >= since && v.TokenUsedAt == null, cancellationToken);
        if (verification is null)
        {
            return false;
        }

        verification.TokenUsedAt = DateTime.UtcNow;
        return true;
    }

    // =====================================================================
    // Customer: list / detail / cancel / reschedule (stories 33-36)
    // =====================================================================

    public async Task<PagedResponse<ReservationResponse>> GetMineAsync(Guid customerId, string? scope, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize, 50);
        var now = DateTime.UtcNow;
        var query = _db.Bookings.AsNoTracking().Where(b => b.CustomerUserId == customerId);

        // upcoming: active and not finished; past: finished or closed (history, story 36); cancelled; all.
        query = (scope ?? "upcoming").ToLowerInvariant() switch
        {
            "past" => query.Where(b => b.EndsAt <= now || b.Status == BookingStatus.Completed || b.Status == BookingStatus.NoShow),
            "cancelled" => query.Where(b => b.Status == BookingStatus.CancelledByCustomer || b.Status == BookingStatus.CancelledByBusiness || b.Status == BookingStatus.Expired),
            "all" => query,
            _ => query.Where(b => (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending) && b.EndsAt > now)
        };

        var ordered = (scope ?? "upcoming").ToLowerInvariant() == "upcoming" ? query.OrderBy(b => b.StartsAt) : query.OrderByDescending(b => b.StartsAt);
        var total = await query.CountAsync(cancellationToken);
        var ids = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(b => b.Id).ToListAsync(cancellationToken);

        var items = new List<ReservationResponse>();
        foreach (var id in ids)
        {
            items.Add(await MapAsync(id, cancellationToken));
        }

        return PagedResponse<ReservationResponse>.Create(items, page, pageSize, total);
    }

    public async Task<ReservationResponse> GetMineAsync(Guid customerId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(customerId, bookingId, cancellationToken);
        return await MapAsync(bookingId, cancellationToken);
    }

    public async Task<ReservationResponse> CancelAsync(Guid customerId, Guid bookingId, string? reason, CancellationToken cancellationToken = default)
    {
        var booking = await GetOwnedAsync(customerId, bookingId, cancellationToken);
        await CancelByCustomerAsync(booking, customerId, reason, cancellationToken);
        return await MapAsync(bookingId, cancellationToken);
    }

    public async Task<ReservationResponse> RescheduleAsync(Guid customerId, Guid bookingId, RescheduleReservationRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await GetOwnedAsync(customerId, bookingId, cancellationToken);
        if (booking.Status != BookingStatus.Confirmed)
        {
            throw new ConflictException("Only confirmed reservations can be changed.", "invalid_status");
        }

        var facility = await _support.LoadBookableFacilityAsync(booking.FacilityId, cancellationToken);
        EnsureBeforeDeadline(booking, facility, "changed");

        // MVP: date/time only, same facility, sport and duration (story 34).
        var duration = booking.DurationMinutes;
        if (request.DurationMinutes.HasValue && request.DurationMinutes != duration)
        {
            throw RequestValidationException.For("durationMinutes", "The duration can't be changed. Cancel and book again instead.");
        }

        var slot = await _support.ValidateCustomerSlotAsync(facility, booking.SportId, request.StartsAt!.Value, duration, booking.Id, cancellationToken);
        await EnforceCustomerLimitsAsync(customerId, facility.TimeZone, slot.StartsAt, booking.Id, cancellationToken);

        var oldStart = booking.StartsAt;
        var oldEnd = booking.EndsAt;
        booking.StartsAt = slot.StartsAt;
        booking.EndsAt = slot.EndsAt;
        booking.BlockedUntil = slot.BlockedUntil;
        // New time, new price snapshot (the old one belonged to the old time).
        booking.Price = slot.Quote.Amount;
        booking.Currency = slot.Quote.Currency;
        booking.UpdatedAt = DateTime.UtcNow;

        _lifecycle.AddHistory(booking, BookingEventType.Rescheduled, null, null, customerId, Text.NullIfEmpty(request.Reason), oldStart, oldEnd);
        await _notifier.RescheduledAsync(booking, oldStart, byBusiness: false, cancellationToken);
        _audit.Log(customerId, "reservation.rescheduled", "Booking", booking.Id.ToString(), $"{oldStart:u} -> {booking.StartsAt:u}", businessId: booking.BusinessId);

        await _support.SaveWithSlotGuardAsync(cancellationToken);
        return await MapAsync(bookingId, cancellationToken);
    }

    // =====================================================================
    // Guests (story 37)
    // =====================================================================

    public async Task<ReservationResponse> GetGuestAsync(string token, CancellationToken cancellationToken = default)
    {
        var booking = await GetByTokenAsync(token, cancellationToken);
        return await MapAsync(booking.Id, cancellationToken);
    }

    public async Task<ReservationResponse> CancelGuestAsync(string token, string? reason, CancellationToken cancellationToken = default)
    {
        var booking = await GetByTokenAsync(token, cancellationToken);
        await CancelByCustomerAsync(booking, null, reason, cancellationToken);
        return await MapAsync(booking.Id, cancellationToken);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private async Task CancelByCustomerAsync(Booking booking, Guid? actorId, string? reason, CancellationToken cancellationToken)
    {
        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Pending))
        {
            throw new ConflictException("This reservation can no longer be cancelled.", "invalid_status");
        }

        var facility = await _db.Facilities.AsNoTracking().Include(f => f.Business).FirstAsync(f => f.Id == booking.FacilityId, cancellationToken);
        EnsureBeforeDeadline(booking, facility, "cancelled");

        await _lifecycle.ChangeStatusAsync(booking, BookingStatus.CancelledByCustomer, actorId, reason, cancellationToken);
        await _payments.RefundIfPaidAsync(booking, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureBeforeDeadline(Booking booking, Facility facility, string action)
    {
        var cutoff = ReservationSupport.CutoffHours(facility);
        if (DateTime.UtcNow >= booking.StartsAt.AddHours(-cutoff))
        {
            throw new ConflictException(
                $"This reservation can only be {action} up to {cutoff} hour(s) before it starts. Please contact the facility.",
                "cancellation_deadline_passed");
        }
    }

    private async Task<Booking> GetOwnedAsync(Guid customerId, Guid bookingId, CancellationToken cancellationToken) =>
        await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.CustomerUserId == customerId, cancellationToken)
        ?? throw new NotFoundException("Reservation not found.");

    private async Task<Booking> GetByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var hash = SecureTokens.Hash(token.Trim());
        return await _db.Bookings.FirstOrDefaultAsync(b => b.GuestAccessTokenHash == hash, cancellationToken)
               ?? throw new NotFoundException("Reservation not found. Check the reference and try again.");
    }

    private async Task<ReservationResponse> MapAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings.AsNoTracking().FirstAsync(b => b.Id == bookingId, cancellationToken);
        var facility = await _db.Facilities.AsNoTracking().Include(f => f.Business).Include(f => f.Images)
            .FirstAsync(f => f.Id == booking.FacilityId, cancellationToken);
        var sport = await _db.Sports.AsNoTracking().Where(s => s.Id == booking.SportId).Select(s => s.Name).FirstAsync(cancellationToken);

        var response = new ReservationResponse();
        ReservationSupport.Fill(response, booking, facility, sport);

        if (booking.Status == BookingStatus.Completed && booking.CustomerUserId.HasValue)
        {
            response.CanReview = !await _db.Reviews.AnyAsync(r => r.BookingId == booking.Id, cancellationToken)
                                 && await _flags.IsEnabledAsync(FeatureFlagKeys.Reviews, cancellationToken);
        }

        if (booking.Status == BookingStatus.Pending)
        {
            response.CheckoutUrl = await _db.Payments.AsNoTracking()
                .Where(p => p.BookingId == booking.Id && p.Status == PaymentRecordStatus.Pending)
                .OrderByDescending(p => p.CreatedAt).Select(p => p.CheckoutUrl).FirstOrDefaultAsync(cancellationToken);
        }

        return response;
    }
}
