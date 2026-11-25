using FiveAndOne.Application.Services.Facilities;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

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

/// <summary>Full facility as seen by its owner (and admins).</summary>
public class FacilityResponse
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
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

    public FacilityStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? SuspensionReason { get; set; }

    /// <summary>True when every field required for publishing is filled in.</summary>
    public bool IsReadyToPublish { get; set; }

    /// <summary>What is still missing before it can be published (field → message). Empty when ready.</summary>
    public IDictionary<string, string[]> MissingForPublish { get; set; } = new Dictionary<string, string[]>();

    public List<IdNameResponse> Sports { get; set; } = new();
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
            OwnerUserId = f.OwnerUserId,
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
            Status = f.Status,
            PublishedAt = f.PublishedAt,
            SuspensionReason = f.SuspensionReason,
            IsReadyToPublish = missing.Count == 0,
            MissingForPublish = missing,
            Sports = f.Sports.Select(s => new IdNameResponse { Id = s.SportId, Name = s.Sport.Name }).OrderBy(x => x.Name).ToList(),
            Amenities = f.Amenities.Select(a => new IdNameResponse { Id = a.AmenityId, Name = a.Amenity.Name }).OrderBy(x => x.Name).ToList(),
            Images = MapImages(f),
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        };
    }

    internal static List<FacilityImageResponse> MapImages(Facility f) => f.Images
        .OrderBy(i => i.SortOrder)
        .Select(i => new FacilityImageResponse { Id = i.Id, Url = i.Url, SortOrder = i.SortOrder, IsCover = i.IsCover })
        .ToList();
}

/// <summary>Compact row for the owner's / admin's facility list.</summary>
public class FacilitySummaryResponse
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
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
        OwnerUserId = f.OwnerUserId,
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

/// <summary>What customers see. No status, owner id or other internal fields.</summary>
public class PublicFacilityResponse
{
    public Guid Id { get; set; }
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
    public string? BusinessName { get; set; }
    public List<IdNameResponse> Sports { get; set; } = new();
    public List<IdNameResponse> Amenities { get; set; } = new();
    public List<FacilityImageResponse> Images { get; set; } = new();

    public static PublicFacilityResponse FromEntity(Facility f) => new()
    {
        Id = f.Id,
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
        BusinessName = f.Owner?.BusinessProfile?.BusinessName,
        Sports = f.Sports.Select(s => new IdNameResponse { Id = s.SportId, Name = s.Sport.Name }).OrderBy(x => x.Name).ToList(),
        Amenities = f.Amenities.Select(a => new IdNameResponse { Id = a.AmenityId, Name = a.Amenity.Name }).OrderBy(x => x.Name).ToList(),
        Images = FacilityResponse.MapImages(f)
    };
}

/// <summary>How many facilities the business has vs. its limit, so the UI can explain it.</summary>
public class FacilityUsageResponse
{
    public int MaxFacilities { get; set; }
    public int TotalFacilities { get; set; }
    public int PublishedFacilities { get; set; }
    public int RemainingFacilities => Math.Max(0, MaxFacilities - TotalFacilities);
    public bool CanCreate => TotalFacilities < MaxFacilities;
}

public class SuspendFacilityRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "A reason is required.")]
    [System.ComponentModel.DataAnnotations.StringLength(500, ErrorMessage = "Reason can be at most 500 characters.")]
    public string? Reason { get; set; }
}
