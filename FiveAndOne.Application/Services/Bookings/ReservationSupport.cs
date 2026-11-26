using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Dtos.Bookings;
using FiveAndOne.Application.Services.Availability;
using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FiveAndOne.Application.Services.Bookings;

/// <summary>Validated slot: the reservation's times and the price snapshot.</summary>
public sealed record ValidatedSlot(DateTime StartsAt, DateTime EndsAt, DateTime BlockedUntil, PriceQuote Quote);

/// <summary>Helpers shared by customer, guest, business and admin reservation flows.</summary>
public class ReservationSupport
{
    public const string SlotUnavailableMessage = "This time slot is no longer available. Please choose another time.";

    private readonly ApplicationDbContext _db;
    private readonly IAvailabilityService _availability;
    private readonly AppSettings _app;

    public ReservationSupport(ApplicationDbContext db, IAvailabilityService availability, Microsoft.Extensions.Options.IOptions<AppSettings> app)
    {
        _db = db;
        _availability = availability;
        _app = app.Value;
    }

    /// <summary>
    /// Backend revalidation of a customer-selected slot (stories 17, 30): it must be one of the generated
    /// slots for that facility/sport/duration and still free. The database constraint is the final guard.
    /// </summary>
    public async Task<ValidatedSlot> ValidateCustomerSlotAsync(Facility facility, Guid sportId, DateTime startsAtUtc, int durationMinutes,
        Guid? excludeBookingId, CancellationToken cancellationToken)
    {
        var start = DateTime.SpecifyKind(startsAtUtc.ToUniversalTime(), DateTimeKind.Utc);
        var localDate = DateOnly.FromDateTime(LocalTime.ToLocal(start, facility.TimeZone));

        // The slot can belong to the previous day's overnight window, so check both dates.
        var (rangeStart, _) = LocalTime.DayRangeUtc(localDate.AddDays(-1), facility.TimeZone);
        var (_, rangeEnd) = LocalTime.DayRangeUtc(localDate.AddDays(2), facility.TimeZone);
        var busy = (await _availability.LoadBusyAsync(new[] { facility.Id }, rangeStart, rangeEnd, excludeBookingId, cancellationToken))[facility.Id];

        foreach (var date in new[] { localDate, localDate.AddDays(-1) })
        {
            var day = _availability.BuildDay(facility, sportId, date, durationMinutes, busy);
            var slot = day.Slots.FirstOrDefault(s => s.StartsAt == start);
            if (slot is null)
            {
                continue;
            }

            if (!slot.Available)
            {
                throw slot.UnavailableReason switch
                {
                    "too_soon" => new ConflictException("This time is too close to start to book online.", "too_soon"),
                    "too_far_ahead" => new ConflictException($"Reservations can be made at most {facility.MaxAdvanceDays} days ahead.", "too_far_ahead"),
                    "no_price" => new ConflictException("This time can't be booked yet: the facility has no price for it.", "pricing_missing"),
                    _ => new ConflictException(SlotUnavailableMessage, "slot_unavailable")
                };
            }

            var rules = SportRules.For(facility, facility.Sports.First(s => s.SportId == sportId));
            var localStart = LocalTime.ToLocal(start, facility.TimeZone);
            var quote = PricingEngine.Quote(facility.PriceRules, sportId, durationMinutes, localStart.DayOfWeek, TimeOnly.FromDateTime(localStart))!;
            return new ValidatedSlot(slot.StartsAt, slot.EndsAt, slot.EndsAt.AddMinutes(rules.BufferMinutes), quote);
        }

        throw new ConflictException("This is not a bookable start time for this facility. Pick a time from the list.", "invalid_slot");
    }

    /// <summary>The facility with everything needed to validate slots, if the public can book it.</summary>
    public async Task<Facility> LoadBookableFacilityAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        var facility = await _db.Facilities.AsNoTracking().Where(f => f.Id == facilityId).WithSchedule()
            .Include(f => f.Images)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        var isPublic = await _db.Facilities.Public().AnyAsync(f => f.Id == facilityId, cancellationToken);
        if (!isPublic)
        {
            // Hidden/suspended facility or a business without an active subscription: no new reservations.
            throw new ConflictException("This facility is not accepting reservations right now.", "facility_unavailable");
        }

        return facility;
    }

    public static int CutoffHours(Facility facility) => facility.CancellationCutoffHours ?? facility.Business.CancellationCutoffHours;

    public static CancellationPolicyResponse Policy(Booking booking, Facility facility)
    {
        var cutoff = CutoffHours(facility);
        var until = booking.StartsAt.AddHours(-cutoff);
        var open = booking.Status is BookingStatus.Confirmed or BookingStatus.Pending && DateTime.UtcNow < until;

        return new CancellationPolicyResponse
        {
            CutoffHours = cutoff,
            Note = facility.CancellationPolicyNote ?? facility.Business.CancellationPolicyNote,
            CancellableUntil = until,
            CanCancel = open,
            CanModify = open && booking.Status == BookingStatus.Confirmed && booking.Source == BookingSource.SportX
        };
    }

    public async Task<string> NewBookingNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var number = SecureTokens.Reference("SX");
            if (!await _db.Bookings.AnyAsync(b => b.BookingNumber == number, cancellationToken))
            {
                return number;
            }
        }

        return SecureTokens.Reference("SX", 10);
    }

    public string GuestUrl(string token) => $"{_app.FrontendBaseUrl}/reservations/guest/{token}";

    /// <summary>Saves, turning the no-overlap constraint into the story's friendly 409.</summary>
    public async Task SaveWithSlotGuardAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            throw new ConflictException(SlotUnavailableMessage, "slot_unavailable");
        }
    }

    public static void Fill(ReservationResponse r, Booking b, Facility facility, string sportName)
    {
        r.Id = b.Id;
        r.BookingNumber = b.BookingNumber;
        r.Status = b.Status;
        r.Source = b.Source;
        r.PaymentStatus = b.PaymentStatus;
        r.Facility = new ReservationFacilityDto
        {
            Id = facility.Id,
            Name = facility.Name,
            Slug = facility.Slug,
            AddressLine = facility.AddressLine,
            City = facility.City,
            Latitude = facility.Latitude,
            Longitude = facility.Longitude,
            CoverImageUrl = facility.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url
        };
        r.BusinessName = facility.Business.BusinessName;
        r.SportId = b.SportId;
        r.SportName = sportName;
        r.StartsAt = b.StartsAt;
        r.EndsAt = b.EndsAt;
        r.LocalStartsAt = LocalTime.ToLocal(b.StartsAt, facility.TimeZone);
        r.LocalEndsAt = LocalTime.ToLocal(b.EndsAt, facility.TimeZone);
        r.TimeZone = facility.TimeZone;
        r.DurationMinutes = b.DurationMinutes;
        r.Price = b.Price;
        r.Currency = b.Currency;
        r.IsGuest = b.IsGuest;
        r.ContactName = b.ContactName;
        r.ContactPhone = b.ContactPhone;
        r.ContactEmail = b.ContactEmail;
        r.Notes = b.CustomerNotes;
        r.CancelledAt = b.CancelledAt;
        r.CancellationReason = b.CancellationReason;
        r.CancellationPolicy = Policy(b, facility);
        r.ExpiresAt = b.Status == BookingStatus.Pending ? b.ExpiresAt : null;
        r.CreatedAt = b.CreatedAt;
    }
}
