using FiveAndOne.Application.Services.Facilities;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Application.Dtos.Facilities;

public class IdNameResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
}

public class FacilityImageResponse
{
    public Guid Id { get; set; }
    public string Url { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsCover { get; set; }
}

/// <summary>A sport on a facility with its effective reservation rules.</summary>
public class FacilitySportResponse
{
    public Guid SportId { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;

    /// <summary>Sport-specific override; null = uses the facility's durations.</summary>
    public int[]? DurationOptions { get; set; }
    public int? BufferMinutes { get; set; }

    /// <summary>What actually applies (override or facility default).</summary>
    public int[] EffectiveDurationOptions { get; set; } = Array.Empty<int>();
    public int EffectiveBufferMinutes { get; set; }

    public bool HasOwnHours { get; set; }
}

/// <summary>Full facility as seen by its business (and admins).</summary>
public class FacilityResponse
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid? VenueId { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public FacilityType? Type { get; set; }
    public int? Capacity { get; set; }
    public bool IsIndoor { get; set; }
    public string? Surface { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = default!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string TimeZone { get; set; } = default!;

    public FacilityStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? SuspensionReason { get; set; }

    /// <summary>True when every field required for publishing is filled in.</summary>
    public bool IsReadyToPublish { get; set; }

    /// <summary>What is still missing before it can be published (field → message). Empty when ready.</summary>
    public IDictionary<string, string[]> MissingForPublish { get; set; } = new Dictionary<string, string[]>();

    // Reservation rules (story 16)
    public int[] DurationOptions { get; set; } = Array.Empty<int>();
    public int BufferMinutes { get; set; }
    public int SlotStepMinutes { get; set; }
    public int MinAdvanceMinutes { get; set; }
    public int MaxAdvanceDays { get; set; }

    // Cancellation policy (story 33); null = business default
    public int? CancellationCutoffHours { get; set; }
    public string? CancellationPolicyNote { get; set; }

    public List<FacilitySportResponse> Sports { get; set; } = new();
    public List<IdNameResponse> Amenities { get; set; } = new();
    public List<FacilityImageResponse> Images { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static FacilityResponse FromEntity(Facility f)
    {
        var missing = FacilityPublishRules.GetMissingFields(f);

        return new FacilityResponse
        {
            Id = f.Id,
            BusinessId = f.BusinessId,
            VenueId = f.VenueId,
            Name = f.Name,
            Slug = f.Slug,
            Description = f.Description,
            Type = f.Type,
            Capacity = f.Capacity,
            IsIndoor = f.IsIndoor,
            Surface = f.Surface,
            AddressLine = f.AddressLine,
            City = f.City,
            Country = f.Country,
            Latitude = f.Latitude,
            Longitude = f.Longitude,
            TimeZone = f.TimeZone,
            Status = f.Status,
            PublishedAt = f.PublishedAt,
            SuspensionReason = f.SuspensionReason,
            IsReadyToPublish = missing.Count == 0,
            MissingForPublish = missing,
            DurationOptions = f.DurationOptions.OrderBy(x => x).ToArray(),
            BufferMinutes = f.BufferMinutes,
            SlotStepMinutes = f.SlotStepMinutes,
            MinAdvanceMinutes = f.MinAdvanceMinutes,
            MaxAdvanceDays = f.MaxAdvanceDays,
            CancellationCutoffHours = f.CancellationCutoffHours,
            CancellationPolicyNote = f.CancellationPolicyNote,
            Sports = MapSports(f),
            Amenities = f.Amenities.Select(a => new IdNameResponse { Id = a.AmenityId, Name = a.Amenity.Name }).OrderBy(x => x.Name).ToList(),
            Images = MapImages(f),
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        };
    }

    internal static List<FacilitySportResponse> MapSports(Facility f) => f.Sports
        .OrderBy(s => s.Sport.SortOrder)
        .Select(s => new FacilitySportResponse
        {
            SportId = s.SportId,
            Name = s.Sport.Name,
            Slug = s.Sport.Slug,
            DurationOptions = s.DurationOptions,
            BufferMinutes = s.BufferMinutes,
            EffectiveDurationOptions = (s.DurationOptions is { Length: > 0 } own ? own : f.DurationOptions).OrderBy(x => x).ToArray(),
            EffectiveBufferMinutes = s.BufferMinutes ?? f.BufferMinutes,
            HasOwnHours = f.OperatingHours.Any(h => h.SportId == s.SportId)
        })
        .ToList();

    internal static List<FacilityImageResponse> MapImages(Facility f) => f.Images
        .OrderBy(i => i.SortOrder)
        .Select(i => new FacilityImageResponse { Id = i.Id, Url = i.Url, SortOrder = i.SortOrder, IsCover = i.IsCover })
        .ToList();
}

/// <summary>Compact row for the business's / admin's facility list.</summary>
public class FacilitySummaryResponse
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string? BusinessName { get; set; }
    public string Name { get; set; } = default!;
    public FacilityType? Type { get; set; }
    public string? City { get; set; }
    public FacilityStatus Status { get; set; }
    public bool IsReadyToPublish { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<string> Sports { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static FacilitySummaryResponse FromEntity(Facility f) => new()
    {
        Id = f.Id,
        BusinessId = f.BusinessId,
        BusinessName = f.Business?.BusinessName,
        Name = f.Name,
        Type = f.Type,
        City = f.City,
        Status = f.Status,
        IsReadyToPublish = FacilityPublishRules.GetMissingFields(f).Count == 0,
        CoverImageUrl = f.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
        Sports = f.Sports.Select(s => s.Sport.Name).OrderBy(n => n).ToList(),
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt
    };
}

/// <summary>How many facilities the business has vs. its plan, so the UI can explain/disable "Add facility".</summary>
public class FacilityUsageResponse
{
    public bool HasActiveSubscription { get; set; }
    public string? PlanName { get; set; }
    public int MaxFacilities { get; set; }
    public int TotalFacilities { get; set; }
    public int PublishedFacilities { get; set; }
    public int RemainingFacilities => Math.Max(0, MaxFacilities - TotalFacilities);
    public bool CanCreate => TotalFacilities < MaxFacilities;

    /// <summary>Publishing needs an active (or in-grace) subscription.</summary>
    public bool CanPublish => HasActiveSubscription && PublishedFacilities < MaxFacilities;
}

public class SuspendFacilityRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "A reason is required.")]
    [System.ComponentModel.DataAnnotations.StringLength(500, ErrorMessage = "Reason can be at most 500 characters.")]
    public string? Reason { get; set; }
}
