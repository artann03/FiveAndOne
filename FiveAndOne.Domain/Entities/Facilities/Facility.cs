using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Facilities;

/// <summary>
/// One independently bookable court / pitch / lane, owned by a business.
/// Availability and reservations are tracked per facility.
/// </summary>
public class Facility : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    // Optional parent venue (sports centre) this court belongs to.
    public Guid? VenueId { get; set; }
    public Venue? Venue { get; set; }

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

    // ---- Reservation rules (story 16) ----
    // Bookable lengths in minutes. A sport can override them (FacilitySport.DurationOptions).
    public int[] DurationOptions { get; set; } = { 60 };
    // Free time kept after every reservation (cleaning, changeover). Default 0.
    public int BufferMinutes { get; set; }
    // Slots start every N minutes from opening time.
    public int SlotStepMinutes { get; set; } = 30;
    public int MinAdvanceMinutes { get; set; }
    public int MaxAdvanceDays { get; set; } = 30;

    // ---- Cancellation policy (story 33). Null = use the business default. ----
    public int? CancellationCutoffHours { get; set; }
    public string? CancellationPolicyNote { get; set; }

    // Post-MVP (story 62): reservations must be paid online. Only used when the feature flag is on.
    public bool RequiresOnlinePayment { get; set; }

    // Postgres xmin: two simultaneous edits can't silently overwrite each other.
    public uint Version { get; set; }

    public ICollection<FacilityImage> Images { get; set; } = new List<FacilityImage>();
    public ICollection<FacilityAmenity> Amenities { get; set; } = new List<FacilityAmenity>();
    public ICollection<FacilitySport> Sports { get; set; } = new List<FacilitySport>();
    public ICollection<FacilityOperatingHours> OperatingHours { get; set; } = new List<FacilityOperatingHours>();
    public ICollection<FacilityScheduleException> ScheduleExceptions { get; set; } = new List<FacilityScheduleException>();
    public ICollection<FacilityPriceRule> PriceRules { get; set; } = new List<FacilityPriceRule>();
    public ICollection<AvailabilityBlock> AvailabilityBlocks { get; set; } = new List<AvailabilityBlock>();
}
