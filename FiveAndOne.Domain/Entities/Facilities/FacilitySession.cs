using FiveAndOne.Domain.Entities.Bookings;
using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>A scheduled time slot with several spots (swimming session, group class, driving range session).</summary>
public class FacilitySession : BaseEntity
{
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = default!;

    public Guid SportId { get; set; }
    public Sport Sport { get; set; } = default!;

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public int Capacity { get; set; }
    public int BookedCount { get; set; }

    public decimal PricePerSpot { get; set; }
    public string Currency { get; set; } = "EUR";

    public FacilitySessionStatus Status { get; set; } = FacilitySessionStatus.Scheduled;

    // Postgres xmin: stops two bookings from both taking the last spot.
    public uint Version { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
