using FiveAndOne.API.Extensions;
using FiveAndOne.Application.Common.Abstractions;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Availability;
using FiveAndOne.Application.Dtos.Catalog;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Discovery;
using FiveAndOne.Application.Dtos.Subscriptions;
using FiveAndOne.Application.Services.Availability;
using FiveAndOne.Application.Services.Catalog;
using FiveAndOne.Application.Services.Community;
using FiveAndOne.Application.Services.Discovery;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Application.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiveAndOne.API.Controllers;

/// <summary>Catalogue and lookups. Names follow Accept-Language (sq default, en, tr, sr).</summary>
[ApiController]
[Route("api")]
[AllowAnonymous]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalog;
    private readonly IFeatureFlags _flags;
    private readonly ISubscriptionService _subscriptions;
    private readonly IDiscoveryService _discovery;

    public CatalogController(ICatalogService catalog, IFeatureFlags flags, ISubscriptionService subscriptions, IDiscoveryService discovery)
    {
        _catalog = catalog;
        _flags = flags;
        _subscriptions = subscriptions;
        _discovery = discovery;
    }

    /// <summary>Active sports. includeCounts=true adds the number of bookable facilities per sport.</summary>
    [HttpGet("sports")]
    public async Task<ActionResult<List<SportResponse>>> Sports([FromQuery] bool includeCounts, CancellationToken ct) =>
        Ok(await _catalog.GetSportsAsync(includeCounts, ct));

    [HttpGet("sports/{idOrSlug}")]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SportResponse>> Sport(string idOrSlug, CancellationToken ct) =>
        Ok(await _catalog.GetSportAsync(idOrSlug, ct));

    [HttpGet("amenities")]
    public async Task<ActionResult<List<AmenityResponse>>> Amenities(CancellationToken ct) =>
        Ok(await _catalog.GetAmenitiesAsync(ct));

    /// <summary>Cities that have bookable facilities, with counts.</summary>
    [HttpGet("cities")]
    public async Task<ActionResult<List<CityCountResponse>>> Cities(CancellationToken ct) =>
        Ok(await _discovery.GetCitiesAsync(ct));

    /// <summary>Feature flags for the frontend (key → enabled). Never a replacement for authorization.</summary>
    [HttpGet("feature-flags")]
    public async Task<ActionResult<Dictionary<string, bool>>> FeatureFlags(CancellationToken ct) =>
        Ok(await _flags.GetPublicAsync(ct));

    /// <summary>Subscription plans businesses can choose (story 43).</summary>
    [HttpGet("plans")]
    public async Task<ActionResult<List<PlanResponse>>> Plans(CancellationToken ct) =>
        Ok(await _subscriptions.GetActivePlansAsync(ct));
}

/// <summary>Public discovery (stories 21-27, 59, 63, 66).</summary>
[ApiController]
[Route("api")]
[AllowAnonymous]
public class DiscoveryController : ControllerBase
{
    private readonly IDiscoveryService _discovery;
    private readonly IAvailabilityService _availability;
    private readonly IReviewService _reviews;

    public DiscoveryController(IDiscoveryService discovery, IAvailabilityService availability, IReviewService reviews)
    {
        _discovery = discovery;
        _availability = availability;
        _reviews = reviews;
    }

    /// <summary>Homepage: sports (with counts), cities, featured facilities, sponsored (if enabled).</summary>
    [HttpGet("home")]
    public async Task<ActionResult<HomeResponse>> Home(CancellationToken ct) => Ok(await _discovery.GetHomeAsync(ct));

    /// <summary>
    /// Search facilities: sport (id or slug), city, search text, date, time (preferred, ±timeFlexMinutes), durationMinutes,
    /// minPrice/maxPrice, availableOnly, latitude/longitude/radiusKm; sort = relevance | price_asc | distance | availability.
    /// Result cards include up to 3 indicative next free slots (revalidated at booking).
    /// </summary>
    [HttpGet("facilities")]
    public async Task<ActionResult<FacilitySearchResponse>> Search([FromQuery] FacilitySearchQuery query, CancellationToken ct) =>
        Ok(await _discovery.SearchAsync(query, ct));

    /// <summary>Map markers inside a bounding box (all optional), optionally for one sport.</summary>
    [HttpGet("facilities/map")]
    public async Task<ActionResult<List<MapMarkerResponse>>> Map([FromQuery] decimal? minLat, [FromQuery] decimal? maxLat,
        [FromQuery] decimal? minLng, [FromQuery] decimal? maxLng, [FromQuery] string? sport, CancellationToken ct) =>
        Ok(await _discovery.GetMapAsync(minLat, maxLat, minLng, maxLng, sport, ct));

    /// <summary>Full facility page: details, hours, prices, cancellation policy. Not public = 404.</summary>
    [HttpGet("facilities/{idOrSlug}")]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicFacilityDetailsResponse>> Details(string idOrSlug, CancellationToken ct) =>
        Ok(await _discovery.GetDetailsAsync(idOrSlug, ct));

    /// <summary>Time slots for one date (story 18). The backend is the source of truth for availability.</summary>
    [HttpGet("facilities/{idOrSlug}/availability")]
    public async Task<ActionResult<DaySlotsResponse>> Availability(string idOrSlug, [FromQuery] Guid sportId, [FromQuery] DateOnly date,
        [FromQuery] int? durationMinutes, CancellationToken ct) =>
        Ok(await _availability.GetPublicDayAsync(idOrSlug, sportId, date, durationMinutes, ct));

    /// <summary>Calendar strip: free slot counts per day (max 62 days).</summary>
    [HttpGet("facilities/{idOrSlug}/calendar")]
    public async Task<ActionResult<CalendarResponse>> Calendar(string idOrSlug, [FromQuery] Guid sportId, [FromQuery] DateOnly from,
        [FromQuery] int days = 14, [FromQuery] int? durationMinutes = null, CancellationToken ct = default) =>
        Ok(await _availability.GetPublicCalendarAsync(idOrSlug, sportId, from, days, durationMinutes, ct));

    /// <summary>Published reviews (flag "reviews").</summary>
    [HttpGet("facilities/{idOrSlug}/reviews")]
    public async Task<ActionResult<PagedResponse<ReviewResponse>>> Reviews(string idOrSlug, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _reviews.GetForFacilityAsync(idOrSlug, page, pageSize, ct));

    /// <summary>Parent venue with its courts (flag "venues").</summary>
    [HttpGet("venues/{idOrSlug}")]
    public async Task<ActionResult<PublicVenueResponse>> Venue(string idOrSlug, CancellationToken ct) =>
        Ok(await _discovery.GetVenueAsync(idOrSlug, ct));

    /// <summary>Personal recommendations with the reason each one is shown (flag "recommendations").</summary>
    [HttpGet("recommendations")]
    [Authorize]
    public async Task<ActionResult<List<RecommendationResponse>>> Recommendations([FromQuery] int count = 10, CancellationToken ct = default) =>
        Ok(await _discovery.GetRecommendationsAsync(User.GetUserId(), count, ct));
}

/// <summary>Address search through the backend so the map provider key stays secret (story 11).</summary>
[ApiController]
[Route("api/geocoding")]
[Authorize]
public class GeocodingController : ControllerBase
{
    private readonly IGeocodingService _geocoding;

    public GeocodingController(IGeocodingService geocoding) => _geocoding = geocoding;

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<GeocodingResult>>> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 200)
        {
            throw RequestValidationException.For("q", "Enter an address to search (max 200 characters).");
        }
        return Ok(await _geocoding.SearchAsync(q.Trim(), ct));
    }
}
