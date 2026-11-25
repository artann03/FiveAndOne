using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Activities;

/// <summary>A game/session people can join. Optionally tied to a booking at a facility.</summary>
public class Activity : BaseEntity
{
    public Guid OrganizerUserId { get; set; }
    public User Organizer { get; set; } = default!;

    public Guid SportId { get; set; }
    public Sport Sport { get; set; } = default!;

    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }

    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }

    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    // Free location for activities not tied to a facility (park court, school yard, ...).
    public string? LocationName { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public int MinParticipants { get; set; }
    public int MaxParticipants { get; set; }
    public int ParticipantCount { get; set; }

    public SkillLevel? SkillLevel { get; set; }
    public ActivityVisibility Visibility { get; set; } = ActivityVisibility.Public;
    public ActivityJoinPolicy JoinPolicy { get; set; } = ActivityJoinPolicy.Open;
    public ActivityStatus Status { get; set; } = ActivityStatus.Open;

    public decimal? PricePerParticipant { get; set; }
    public string? Currency { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    // Postgres xmin: protects ParticipantCount when several people join at once.
    public uint Version { get; set; }

    public ICollection<ActivityParticipant> Participants { get; set; } = new List<ActivityParticipant>();
}
