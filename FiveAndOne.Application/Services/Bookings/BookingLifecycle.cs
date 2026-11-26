using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Bookings;

/// <summary>
/// Status transitions shared by customers, businesses, admins and background jobs, so the rules
/// (allowed transitions, history, notifications) live in one place. Callers save the changes.
/// </summary>
public interface IBookingLifecycle
{
    void AddHistory(Booking booking, BookingEventType type, BookingStatus? from, BookingStatus? to, Guid? actorId, string? reason,
        DateTime? oldStart = null, DateTime? oldEnd = null);

    Task ChangeStatusAsync(Booking booking, BookingStatus to, Guid? actorId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Reservations holding time on a facility in [fromUtc, toUtc), optionally only one sport's.</summary>
    Task<List<Booking>> FindActiveAsync(Guid facilityId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    List<ReservationConflictResponse> ToConflicts(IEnumerable<Booking> bookings, string timeZone);

    /// <summary>
    /// Shared "conflicts need explicit resolution" rule (stories 15 and 19): throws 409 with the list when there
    /// are conflicts and no resolution, cancels them when asked to.
    /// </summary>
    Task ResolveConflictsAsync(List<Booking> conflicts, ConflictResolution? resolution, string timeZone, Guid actorId, string reason, CancellationToken cancellationToken = default);
}

public class BookingLifecycle : IBookingLifecycle
{
    private static readonly Dictionary<BookingStatus, BookingStatus[]> Allowed = new()
    {
        [BookingStatus.Pending] = new[] { BookingStatus.Confirmed, BookingStatus.Expired, BookingStatus.CancelledByCustomer, BookingStatus.CancelledByBusiness },
        [BookingStatus.Confirmed] = new[] { BookingStatus.CancelledByCustomer, BookingStatus.CancelledByBusiness, BookingStatus.Completed, BookingStatus.NoShow },
        // A business can correct a mistaken outcome.
        [BookingStatus.Completed] = new[] { BookingStatus.NoShow },
        [BookingStatus.NoShow] = new[] { BookingStatus.Completed },
    };

    private readonly ApplicationDbContext _db;
    private readonly IBookingNotifier _notifier;
    private readonly IAuditService _audit;

    public BookingLifecycle(ApplicationDbContext db, IBookingNotifier notifier, IAuditService audit)
    {
        _db = db;
        _notifier = notifier;
        _audit = audit;
    }

    public void AddHistory(Booking booking, BookingEventType type, BookingStatus? from, BookingStatus? to, Guid? actorId, string? reason,
        DateTime? oldStart = null, DateTime? oldEnd = null)
    {
        _db.BookingHistory.Add(new BookingHistory
        {
            BookingId = booking.Id,
            EventType = type,
            FromStatus = from,
            ToStatus = to,
            OldStartsAt = oldStart,
            OldEndsAt = oldEnd,
            NewStartsAt = type == BookingEventType.Rescheduled ? booking.StartsAt : null,
            NewEndsAt = type == BookingEventType.Rescheduled ? booking.EndsAt : null,
            ChangedByUserId = actorId,
            Reason = reason
        });
    }

    public async Task ChangeStatusAsync(Booking booking, BookingStatus to, Guid? actorId, string? reason, CancellationToken cancellationToken = default)
    {
        var from = booking.Status;
        if (from == to)
        {
            return;
        }

        if (!Allowed.TryGetValue(from, out var targets) || !targets.Contains(to))
        {
            throw new ConflictException($"A {Describe(from)} reservation can't be changed to {Describe(to)}.", "invalid_status_transition");
        }

        var now = DateTime.UtcNow;
        booking.Status = to;
        booking.UpdatedAt = now;

        if (to is BookingStatus.CancelledByCustomer or BookingStatus.CancelledByBusiness)
        {
            booking.CancelledAt = now;
            booking.CancelledByUserId = actorId;
            booking.CancellationReason = Text.NullIfEmpty(reason);
        }

        AddHistory(booking, BookingEventType.StatusChanged, from, to, actorId, reason);
        _audit.Log(actorId, $"reservation.{to.ToString().ToLowerInvariant()}", "Booking", booking.Id.ToString(), booking.BookingNumber, businessId: booking.BusinessId);

        if (to is BookingStatus.CancelledByCustomer or BookingStatus.CancelledByBusiness)
        {
            await _notifier.CancelledAsync(booking, to == BookingStatus.CancelledByBusiness, cancellationToken);
        }
    }

    public Task<List<Booking>> FindActiveAsync(Guid facilityId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
        _db.Bookings
            .Where(b => b.FacilityId == facilityId
                        && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                        && b.StartsAt < toUtc && b.EndsAt > fromUtc)
            .OrderBy(b => b.StartsAt)
            .ToListAsync(cancellationToken);

    public List<ReservationConflictResponse> ToConflicts(IEnumerable<Booking> bookings, string timeZone) => bookings
        .Select(b => new ReservationConflictResponse
        {
            BookingId = b.Id,
            BookingNumber = b.BookingNumber,
            Source = b.Source,
            ContactName = b.ContactName,
            StartsAt = b.StartsAt,
            EndsAt = b.EndsAt,
            LocalStartsAt = LocalTime.ToLocal(b.StartsAt, timeZone),
            Status = b.Status
        })
        .ToList();

    public async Task ResolveConflictsAsync(List<Booking> conflicts, ConflictResolution? resolution, string timeZone, Guid actorId, string reason, CancellationToken cancellationToken = default)
    {
        if (conflicts.Count == 0 || resolution == ConflictResolution.KeepReservations)
        {
            return;
        }

        if (resolution is null)
        {
            throw new ConflictException(
                $"{conflicts.Count} existing reservation(s) conflict with this change. Choose to cancel them or keep them, then save again.",
                "reservation_conflicts",
                new { conflicts = ToConflicts(conflicts, timeZone) });
        }

        foreach (var booking in conflicts)
        {
            await ChangeStatusAsync(booking, BookingStatus.CancelledByBusiness, actorId, reason, cancellationToken);
        }
    }

    private static string Describe(BookingStatus status) => status switch
    {
        BookingStatus.CancelledByCustomer => "cancelled",
        BookingStatus.CancelledByBusiness => "cancelled",
        BookingStatus.NoShow => "no-show",
        _ => status.ToString().ToLowerInvariant()
    };
}
