using FiveAndOne.Application.Common;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Application.Services.Availability;

public readonly record struct UtcRange(DateTime Start, DateTime End)
{
    public bool Overlaps(DateTime start, DateTime end) => Start < end && start < End;
}

public readonly record struct LocalWindow(TimeOnly Opens, TimeOnly Closes)
{
    public bool IsOvernight => Closes <= Opens;
}

/// <summary>Something that occupies time on a facility: a reservation (with its buffer) or a manual block.</summary>
public readonly record struct BusyInterval(DateTime Start, DateTime End, Guid? SportId, bool IsBlock);

/// <summary>The effective reservation rules for one sport on one facility.</summary>
public sealed record SportRules(int[] DurationOptions, int BufferMinutes, int SlotStepMinutes, int MinAdvanceMinutes, int MaxAdvanceDays)
{
    public static SportRules For(Facility facility, FacilitySport? sport) => new(
        sport?.DurationOptions is { Length: > 0 } own ? own : facility.DurationOptions,
        sport?.BufferMinutes ?? facility.BufferMinutes,
        facility.SlotStepMinutes,
        facility.MinAdvanceMinutes,
        facility.MaxAdvanceDays);
}

public sealed record SlotCandidate(DateTime StartUtc, DateTime EndUtc, bool Available, string? Reason);

/// <summary>
/// Pure scheduling rules (no database): which hours apply on a date, and which slots are free.
/// The backend is the single source of truth for availability (story 17).
/// </summary>
public static class ScheduleEngine
{
    /// <summary>
    /// Opening windows for a local date. A schedule exception for that date wins over weekly hours;
    /// sport-specific weekly hours win over facility hours when the sport has any.
    /// </summary>
    public static List<LocalWindow> WindowsFor(
        DateOnly date,
        Guid? sportId,
        IReadOnlyCollection<FacilityOperatingHours> hours,
        IReadOnlyCollection<FacilityScheduleException> exceptions)
    {
        var exception = exceptions
            .Where(x => x.StartDate <= date && x.EndDate >= date)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (exception is not null)
        {
            return exception.IsClosed || exception.OpensAt is null || exception.ClosesAt is null
                ? new List<LocalWindow>()
                : new List<LocalWindow> { new(exception.OpensAt.Value, exception.ClosesAt.Value) };
        }

        var useSportHours = sportId.HasValue && hours.Any(h => h.SportId == sportId);
        return hours
            .Where(h => h.DayOfWeek == date.DayOfWeek && (useSportHours ? h.SportId == sportId : h.SportId == null))
            .OrderBy(h => h.OpensAt)
            .Select(h => new LocalWindow(h.OpensAt, h.ClosesAt))
            .ToList();
    }

    /// <summary>Converts local windows of a date to UTC. Overnight windows end on the next day.</summary>
    public static List<UtcRange> ToUtc(DateOnly date, IEnumerable<LocalWindow> windows, string timeZone)
    {
        var result = new List<UtcRange>();
        foreach (var window in windows)
        {
            var start = ResolveUtc(date, window.Opens, timeZone);
            var end = ResolveUtc(window.IsOvernight ? date.AddDays(1) : date, window.Closes, timeZone);
            if (end > start)
            {
                result.Add(new UtcRange(start, end));
            }
        }
        return result;
    }

    /// <summary>
    /// Every slot start for one duration within the opening windows, flagged available or not.
    /// A slot is free when [start, end + buffer) touches no reservation and [start, end) touches no block.
    /// </summary>
    public static List<SlotCandidate> Slots(
        IEnumerable<UtcRange> windows,
        int durationMinutes,
        SportRules rules,
        Guid sportId,
        IReadOnlyCollection<BusyInterval> busy,
        DateTime nowUtc)
    {
        var earliest = nowUtc.AddMinutes(rules.MinAdvanceMinutes);
        var latest = nowUtc.AddDays(rules.MaxAdvanceDays);
        var duration = TimeSpan.FromMinutes(durationMinutes);
        var step = TimeSpan.FromMinutes(Math.Max(rules.SlotStepMinutes, 5));
        var buffer = TimeSpan.FromMinutes(rules.BufferMinutes);
        var slots = new List<SlotCandidate>();

        foreach (var window in windows)
        {
            for (var start = window.Start; start + duration <= window.End; start += step)
            {
                var end = start + duration;
                string? reason = null;

                if (start < earliest) reason = "too_soon";
                else if (start > latest) reason = "too_far_ahead";
                else if (busy.Any(b => b.IsBlock
                             ? (b.SportId is null || b.SportId == sportId) && b.Start < end && start < b.End
                             : b.Start < end + buffer && start < b.End))
                    reason = "taken";

                slots.Add(new SlotCandidate(start, end, reason is null, reason));
            }
        }

        return slots;
    }

    /// <summary>True when [start, end) lies entirely inside one opening window.</summary>
    public static bool FitsInWindows(IEnumerable<UtcRange> windows, DateTime start, DateTime end) =>
        windows.Any(w => w.Start <= start && end <= w.End);

    /// <summary>Busy intervals of other reservations/blocks colliding with [start, blockedUntil).</summary>
    public static bool Collides(IEnumerable<BusyInterval> busy, Guid sportId, DateTime start, DateTime end, DateTime blockedUntil) =>
        busy.Any(b => b.IsBlock
            ? (b.SportId is null || b.SportId == sportId) && b.Start < end && start < b.End
            : b.Start < blockedUntil && start < b.End);

    private static DateTime ResolveUtc(DateOnly date, TimeOnly time, string timeZone)
    {
        // A wall-clock time inside the spring-forward gap doesn't exist: use the first valid minute after it.
        for (var shift = 0; shift <= 120; shift += 15)
        {
            var utc = LocalTime.ToUtc(date.ToDateTime(time).AddMinutes(shift), timeZone);
            if (utc.HasValue)
            {
                return utc.Value;
            }
        }
        return DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Utc);
    }
}

public sealed record PriceQuote(decimal Amount, string Currency, Guid RuleId, PriceType PriceType, decimal RuleAmount);

/// <summary>Picks the price rule for a reservation (story 13).</summary>
public static class PricingEngine
{
    public static PriceQuote? Quote(IEnumerable<FacilityPriceRule> rules, Guid sportId, int durationMinutes, DayOfWeek day, TimeOnly localStart)
    {
        var best = rules
            .Where(r => (r.SportId is null || r.SportId == sportId)
                        && (r.DurationMinutes is null || r.DurationMinutes == durationMinutes)
                        && (r.DayOfWeek is null || r.DayOfWeek == day)
                        && InWindow(r, localStart))
            .OrderByDescending(Specificity)
            .ThenByDescending(r => r.Priority)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefault();

        if (best is null)
        {
            return null;
        }

        var amount = best.PriceType == PriceType.PerHour
            ? Math.Round(best.Amount * durationMinutes / 60m, 2, MidpointRounding.AwayFromZero)
            : best.Amount;

        return new PriceQuote(amount, best.Currency, best.Id, best.PriceType, best.Amount);
    }

    /// <summary>Lowest price any rule gives for the sport (used for "from €X" on cards).</summary>
    public static decimal? FromPrice(IEnumerable<FacilityPriceRule> rules, Guid? sportId, IEnumerable<int> durations)
    {
        var list = rules.Where(r => sportId is null || r.SportId is null || r.SportId == sportId).ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var minDuration = durations.DefaultIfEmpty(60).Min();
        return list.Min(r => r.PriceType == PriceType.PerHour
            ? Math.Round(r.Amount * (r.DurationMinutes ?? minDuration) / 60m, 2, MidpointRounding.AwayFromZero)
            : r.Amount);
    }

    private static bool InWindow(FacilityPriceRule rule, TimeOnly time)
    {
        if (rule.StartTime is null || rule.EndTime is null)
        {
            return true;
        }

        var start = rule.StartTime.Value;
        var end = rule.EndTime.Value;
        return start < end ? time >= start && time < end : time >= start || time < end;
    }

    private static int Specificity(FacilityPriceRule r) =>
        (r.SportId.HasValue ? 8 : 0) + (r.DurationMinutes.HasValue ? 4 : 0) + (r.DayOfWeek.HasValue ? 2 : 0) + (r.StartTime.HasValue ? 1 : 0);
}
