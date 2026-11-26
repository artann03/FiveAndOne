using FiveAndOne.Application.Dtos.Availability;
using FiveAndOne.Application.Dtos.Catalog;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Application.Dtos.Discovery;

/// <summary>Search filters (stories 23, 24). Everything is optional.</summary>
public class FacilitySearchQuery
{
    /// <summary>Sport id or slug.</summary>
    public string? Sport { get; set; }

    /// <summary>City / location name.</summary>
    public string? City { get; set; }

    /// <summary>Free text: facility or business name.</summary>
    public string? Search { get; set; }

    /// <summary>Local date to check availability for.</summary>
    public DateOnly? Date { get; set; }

    /// <summary>Preferred local start time, e.g. 18:00. Slots within ±TimeFlexMinutes match.</summary>
    public TimeOnly? Time { get; set; }

    public int TimeFlexMinutes { get; set; } = 120;

    public int? DurationMinutes { get; set; }

    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    /// <summary>Only facilities with at least one free slot matching date/time.</summary>
    public bool AvailableOnly { get; set; }

    /// <summary>For distance sorting / radius filtering.</summary>
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public double? RadiusKm { get; set; }

    /// <summary>relevance (default), price_asc, distance, availability.</summary>
    public string? Sort { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>A search result card (story 25).</summary>
public class FacilityCardResponse
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string BusinessName { get; set; } = default!;
    public FacilityType? Type { get; set; }
    public bool IsIndoor { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<IdNameResponse> Sports { get; set; } = new();

    /// <summary>Lowest price for the selected sport ("from €X").</summary>
    public decimal? FromPrice { get; set; }
    public string Currency { get; set; } = "EUR";

    /// <summary>The sport the availability snippet is for.</summary>
    public Guid? SlotsSportId { get; set; }

    /// <summary>Indicative next free slots (revalidated at booking time).</summary>
    public List<SlotResponse> NextSlots { get; set; } = new();

    public double? DistanceKm { get; set; }

    /// <summary>Always shown with a visible label when true (story 66).</summary>
    public bool IsSponsored { get; set; }
    public string? SponsoredLabel { get; set; }

    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class FacilitySearchResponse
{
    public List<FacilityCardResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }

    /// <summary>Sponsored facilities matching the filters, shown separately and labelled (empty unless enabled).</summary>
    public List<FacilityCardResponse> Sponsored { get; set; } = new();

    /// <summary>The filters that were applied (so the UI can keep them when navigating).</summary>
    public FacilitySearchQuery AppliedFilters { get; set; } = new();
}

public class MapMarkerResponse
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? City { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<Guid> SportIds { get; set; } = new();
    public decimal? FromPrice { get; set; }
}

public class CityCountResponse
{
    public string City { get; set; } = default!;
    public int FacilityCount { get; set; }
}

/// <summary>Homepage data (story 21).</summary>
public class HomeResponse
{
    public List<SportResponse> Sports { get; set; } = new();
    public List<CityCountResponse> Cities { get; set; } = new();
    public List<FacilityCardResponse> FeaturedFacilities { get; set; } = new();
    public List<FacilityCardResponse> Sponsored { get; set; } = new();
    public string DefaultLanguage { get; set; } = "sq";
}

public class PublicPriceResponse
{
    public Guid? SportId { get; set; }
    public int? DurationMinutes { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public PriceType PriceType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Label { get; set; }
}

/// <summary>Full public facility page (story 27).</summary>
public class PublicFacilityDetailsResponse
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public FacilityType? Type { get; set; }
    public int? Capacity { get; set; }
    public bool IsIndoor { get; set; }
    public string? Surface { get; set; }

    public string BusinessName { get; set; } = default!;
    public string? BusinessPhone { get; set; }

    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = default!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string TimeZone { get; set; } = default!;

    public List<FacilitySportResponse> Sports { get; set; } = new();
    public List<IdNameResponse> Amenities { get; set; } = new();
    public List<FacilityImageResponse> Images { get; set; } = new();

    public WeeklyHoursResponse OperatingHours { get; set; } = new();
    public List<WeeklyHoursResponse> SportHours { get; set; } = new();
    public List<PublicPriceResponse> Prices { get; set; } = new();
    public decimal? FromPrice { get; set; }

    /// <summary>Shown before booking (story 27/33).</summary>
    public int CancellationCutoffHours { get; set; }
    public string? CancellationPolicyNote { get; set; }

    public Guid? VenueId { get; set; }
    public string? VenueName { get; set; }

    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

/// <summary>Parent venue page with its courts (story 59).</summary>
public class PublicVenueResponse
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string BusinessName { get; set; } = default!;
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<IdNameResponse> Sports { get; set; } = new();
    public List<IdNameResponse> Amenities { get; set; } = new();
    public List<FacilityCardResponse> Facilities { get; set; } = new();
}

/// <summary>A recommendation with the transparent reason it was shown (story 66).</summary>
public class RecommendationResponse
{
    public FacilityCardResponse Facility { get; set; } = default!;
    public List<string> Reasons { get; set; } = new();
}
