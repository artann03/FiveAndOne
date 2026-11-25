using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>
/// One independently bookable court / pitch / lane, owned by a business account.
/// Availability and reservations are tracked per facility.
/// </summary>
public class Facility : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public User Owner { get; set; } = default!;

    // ---- Listing details ----
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public FacilityType? Type { get; set; }          // required to publish
    public int? Capacity { get; set; }               // max people, where relevant
    public bool IsIndoor { get; set; }
    public string? Surface { get; set; }

    // ---- Location ----
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = "XK";      // ISO 3166-1 alpha-2
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // IANA time zone used to interpret operating hours. Kosovo uses Europe/Belgrade.
    public string TimeZone { get; set; } = "Europe/Belgrade";

    // ---- Publication ----
    public FacilityStatus Status { get; set; } = FacilityStatus.Draft;
    public DateTime? PublishedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? SuspensionReason { get; set; }

    // ---- Booking rules (configured in a later feature; sensible defaults for now) ----
    public BookingMode BookingMode { get; set; } = BookingMode.FixedSlots;
    public int? SlotDurationMinutes { get; set; } = 60;
    public int? MinDurationMinutes { get; set; }
    public int? MaxDurationMinutes { get; set; }
    public int? DurationStepMinutes { get; set; }
    public int BufferMinutes { get; set; }
    public ConfirmationMode ConfirmationMode { get; set; } = ConfirmationMode.Automatic;
    public int MinAdvanceMinutes { get; set; }
    public int MaxAdvanceDays { get; set; } = 30;
    public int CancellationCutoffHours { get; set; } = 24;

    // Postgres xmin: two simultaneous edits can't silently overwrite each other.
    public uint Version { get; set; }

    public ICollection<FacilityImage> Images { get; set; } = new List<FacilityImage>();
    public ICollection<FacilityAmenity> Amenities { get; set; } = new List<FacilityAmenity>();
    public ICollection<FacilitySport> Sports { get; set; } = new List<FacilitySport>();
    public ICollection<FacilityMember> Members { get; set; } = new List<FacilityMember>();
    public ICollection<FacilityOperatingHours> OperatingHours { get; set; } = new List<FacilityOperatingHours>();
    public ICollection<FacilityPriceRule> PriceRules { get; set; } = new List<FacilityPriceRule>();
    public ICollection<FacilitySession> Sessions { get; set; } = new List<FacilitySession>();
    public ICollection<AvailabilityBlock> AvailabilityBlocks { get; set; } = new List<AvailabilityBlock>();
}
