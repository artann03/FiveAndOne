using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Availability;
using FiveAndOne.Application.Dtos.Discovery;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Application.Services.Availability;
using FiveAndOne.Application.Services.Catalog;
using FiveAndOne.Application.Services.Facilities;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Discovery;

/// <summary>Public discovery: homepage, browse by sport, search, map and facility details (stories 21-27, 59, 66).</summary>
public interface IDiscoveryService
{
    Task<HomeResponse> GetHomeAsync(CancellationToken cancellationToken = default);
    Task<FacilitySearchResponse> SearchAsync(FacilitySearchQuery query, CancellationToken cancellationToken = default);
    Task<List<MapMarkerResponse>> GetMapAsync(decimal? minLat, decimal? maxLat, decimal? minLng, decimal? maxLng, string? sport, CancellationToken cancellationToken = default);
    Task<PublicFacilityDetailsResponse> GetDetailsAsync(string idOrSlug, CancellationToken cancellationToken = default);
    Task<List<CityCountResponse>> GetCitiesAsync(CancellationToken cancellationToken = default);
    Task<PublicVenueResponse> GetVenueAsync(string idOrSlug, CancellationToken cancellationToken = default);
    Task<List<RecommendationResponse>> GetRecommendationsAsync(Guid userId, int count, CancellationToken cancellationToken = default);
}

public class DiscoveryService : IDiscoveryService
{
    private const int MaxCandidates = 300;
    private const int NextSlotCount = 3;

    private readonly ApplicationDbContext _db;
    private readonly IAvailabilityService _availability;
    private readonly ICatalogService _catalog;
    private readonly ITranslationService _translations;
    private readonly IFeatureFlags _flags;

    public DiscoveryService(ApplicationDbContext db, IAvailabilityService availability, ICatalogService catalog, ITranslationService translations, IFeatureFlags flags)
    {
        _db = db;
        _availability = availability;
        _catalog = catalog;
        _translations = translations;
        _flags = flags;
    }

    // =====================================================================
    // Homepage (story 21)
    // =====================================================================

    public async Task<HomeResponse> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        var featured = await LoadPublic().OrderByDescending(f => f.PublishedAt).Take(8).ToListAsync(cancellationToken);

        return new HomeResponse
        {
            Sports = await _catalog.GetSportsAsync(includeCounts: true, cancellationToken),
            Cities = await GetCitiesAsync(cancellationToken),
            FeaturedFacilities = await ToCardsAsync(featured, null, null, null, cancellationToken),
            Sponsored = await GetSponsoredAsync(null, null, cancellationToken)
        };
    }

    public async Task<List<CityCountResponse>> GetCitiesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Facilities.AsNoTracking().Public()
            .Where(f => f.City != null)
            .GroupBy(f => f.City!)
            .Select(g => new CityCountResponse { City = g.Key, FacilityCount = g.Count() })
            .OrderByDescending(x => x.FacilityCount).ThenBy(x => x.City)
            .ToListAsync(cancellationToken);
    }

    // =====================================================================
    // Search, filters, sort and result cards (stories 22-25)
    // =====================================================================

    public async Task<FacilitySearchResponse> SearchAsync(FacilitySearchQuery query, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize, 50);
        var sportId = await ResolveSportIdAsync(query.Sport, cancellationToken);

        if (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice > query.MaxPrice)
        {
            throw RequestValidationException.For("maxPrice", "Max price can't be lower than min price.");
        }

        var sort = (query.Sort ?? "relevance").ToLowerInvariant();
        if (sort == "distance" && (query.Latitude is null || query.Longitude is null))
        {
            throw RequestValidationException.For("sort", "Sorting by distance needs latitude and longitude.");
        }

        var q = LoadPublic();
        if (sportId.HasValue) q = q.Where(f => f.Sports.Any(s => s.SportId == sportId));
        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = Text.EscapeLike(query.City.Trim());
            q = q.Where(f => f.City != null && EF.Functions.ILike(f.City, city, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{Text.EscapeLike(query.Search.Trim())}%";
            q = q.Where(f => EF.Functions.ILike(f.Name, pattern, "\\") || EF.Functions.ILike(f.Business.BusinessName, pattern, "\\")
                             || (f.Description != null && EF.Functions.ILike(f.Description, pattern, "\\")));
        }

        var candidates = await q.OrderByDescending(f => f.PublishedAt).Take(MaxCandidates).ToListAsync(cancellationToken);

        var cards = await ToCardsAsync(candidates, sportId, query.Date, query, cancellationToken);

        if (query.RadiusKm.HasValue && query.Latitude.HasValue)
        {
            cards = cards.Where(c => c.DistanceKm is not null && c.DistanceKm <= query.RadiusKm).ToList();
        }
        if (query.MinPrice.HasValue) cards = cards.Where(c => c.FromPrice >= query.MinPrice).ToList();
        if (query.MaxPrice.HasValue) cards = cards.Where(c => c.FromPrice <= query.MaxPrice).ToList();
        if (query.AvailableOnly) cards = cards.Where(c => c.NextSlots.Count > 0).ToList();

        var searchTerm = query.Search?.Trim();
        cards = sort switch
        {
            "price_asc" => cards.OrderBy(c => c.FromPrice ?? decimal.MaxValue).ThenBy(c => c.Name).ToList(),
            "distance" => cards.OrderBy(c => c.DistanceKm ?? double.MaxValue).ToList(),
            "availability" => cards.OrderByDescending(c => c.NextSlots.Count > 0)
                .ThenBy(c => c.NextSlots.FirstOrDefault()?.StartsAt ?? DateTime.MaxValue).ToList(),
            // Relevance: name match first, then has availability, then rating, then newest (already ordered).
            _ => cards.OrderByDescending(c => !string.IsNullOrEmpty(searchTerm) && c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(c => c.NextSlots.Count > 0)
                .ThenByDescending(c => c.AverageRating ?? 0).ToList()
        };

        return new FacilitySearchResponse
        {
            Items = cards.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = cards.Count,
            Sponsored = await GetSponsoredAsync(sportId, query.City, cancellationToken),
            AppliedFilters = query
        };
    }

    // =====================================================================
    // Map (story 26)
    // =====================================================================

    public async Task<List<MapMarkerResponse>> GetMapAsync(decimal? minLat, decimal? maxLat, decimal? minLng, decimal? maxLng, string? sport, CancellationToken cancellationToken = default)
    {
        var sportId = await ResolveSportIdAsync(sport, cancellationToken);
        var q = _db.Facilities.AsNoTracking().Public().Where(f => f.Latitude != null && f.Longitude != null);

        if (minLat.HasValue) q = q.Where(f => f.Latitude >= minLat);
        if (maxLat.HasValue) q = q.Where(f => f.Latitude <= maxLat);
        if (minLng.HasValue) q = q.Where(f => f.Longitude >= minLng);
        if (maxLng.HasValue) q = q.Where(f => f.Longitude <= maxLng);
        if (sportId.HasValue) q = q.Where(f => f.Sports.Any(s => s.SportId == sportId));

        var facilities = await q.Include(f => f.Images).Include(f => f.Sports).Include(f => f.PriceRules)
            .AsSplitQuery().Take(1000).ToListAsync(cancellationToken);

        return facilities.Select(f => new MapMarkerResponse
        {
            Id = f.Id,
            Slug = f.Slug,
            Name = f.Name,
            Latitude = f.Latitude!.Value,
            Longitude = f.Longitude!.Value,
            City = f.City,
            CoverImageUrl = f.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
            SportIds = f.Sports.Select(s => s.SportId).ToList(),
            FromPrice = PricingEngine.FromPrice(f.PriceRules, sportId, f.DurationOptions)
        }).ToList();
    }

    // =====================================================================
    // Details (story 27)
    // =====================================================================

    public async Task<PublicFacilityDetailsResponse> GetDetailsAsync(string idOrSlug, CancellationToken cancellationToken = default)
    {
        var facility = await LoadPublic().ByIdOrSlug(idOrSlug).Include(f => f.Venue).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        var sportNames = await _translations.GetNamesAsync("Sport", cancellationToken);
        var amenityNames = await _translations.GetNamesAsync("Amenity", cancellationToken);
        var (rating, count) = await RatingAsync(facility.Id, cancellationToken);

        var sports = FacilityResponse.MapSports(facility);
        foreach (var s in sports)
        {
            if (sportNames.TryGetValue(s.SportId, out var name)) s.Name = name;
        }

        return new PublicFacilityDetailsResponse
        {
            Id = facility.Id,
            Slug = facility.Slug,
            Name = facility.Name,
            Description = facility.Description,
            Type = facility.Type,
            Capacity = facility.Capacity,
            IsIndoor = facility.IsIndoor,
            Surface = facility.Surface,
            BusinessName = facility.Business.BusinessName,
            BusinessPhone = facility.Business.Phone,
            AddressLine = facility.AddressLine,
            City = facility.City,
            Country = facility.Country,
            Latitude = facility.Latitude,
            Longitude = facility.Longitude,
            TimeZone = facility.TimeZone,
            Sports = sports,
            Amenities = facility.Amenities
                .Select(a => new IdNameResponse { Id = a.AmenityId, Name = amenityNames.GetValueOrDefault(a.AmenityId, a.Amenity.Name) })
                .OrderBy(a => a.Name).ToList(),
            Images = FacilityResponse.MapImages(facility),
            OperatingHours = FacilityConfigService.ToWeekly(facility.OperatingHours.Where(h => h.SportId == null), null),
            SportHours = facility.OperatingHours.Where(h => h.SportId != null).GroupBy(h => h.SportId)
                .Select(g => FacilityConfigService.ToWeekly(g, g.Key)).ToList(),
            Prices = facility.PriceRules.OrderBy(r => r.SportId).ThenBy(r => r.DayOfWeek).ThenBy(r => r.StartTime)
                .Select(r => new PublicPriceResponse
                {
                    SportId = r.SportId, DurationMinutes = r.DurationMinutes, DayOfWeek = r.DayOfWeek, StartTime = r.StartTime,
                    EndTime = r.EndTime, PriceType = r.PriceType, Amount = r.Amount, Currency = r.Currency, Label = r.Label
                }).ToList(),
            FromPrice = PricingEngine.FromPrice(facility.PriceRules, null, facility.DurationOptions),
            CancellationCutoffHours = facility.CancellationCutoffHours ?? facility.Business.CancellationCutoffHours,
            CancellationPolicyNote = facility.CancellationPolicyNote ?? facility.Business.CancellationPolicyNote,
            VenueId = facility.VenueId,
            VenueName = facility.Venue?.Name,
            AverageRating = rating,
            ReviewCount = count
        };
    }

    // =====================================================================
    // Venues (story 59)
    // =====================================================================

    public async Task<PublicVenueResponse> GetVenueAsync(string idOrSlug, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.Venues, cancellationToken);

        var query = _db.Venues.AsNoTracking().Include(v => v.Business)
            .Where(v => v.Business.Status == BusinessStatus.Active);
        var venue = Guid.TryParse(idOrSlug, out var id)
            ? await query.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            : await query.FirstOrDefaultAsync(v => v.Slug == idOrSlug.ToLower(), cancellationToken);
        if (venue is null)
        {
            throw new NotFoundException("Venue not found.");
        }

        var courts = await LoadPublic().Where(f => f.VenueId == venue.Id).ToListAsync(cancellationToken);
        if (courts.Count == 0)
        {
            throw new NotFoundException("Venue not found.");
        }

        var cards = await ToCardsAsync(courts, null, null, null, cancellationToken);
        return new PublicVenueResponse
        {
            Id = venue.Id,
            Slug = venue.Slug,
            Name = venue.Name,
            Description = venue.Description,
            BusinessName = venue.Business.BusinessName,
            AddressLine = venue.AddressLine ?? courts[0].AddressLine,
            City = venue.City ?? courts[0].City,
            Latitude = venue.Latitude ?? courts[0].Latitude,
            Longitude = venue.Longitude ?? courts[0].Longitude,
            CoverImageUrl = venue.CoverImageUrl ?? cards.FirstOrDefault()?.CoverImageUrl,
            // Aggregated from the child courts; availability and booking stay per court.
            Sports = cards.SelectMany(c => c.Sports).GroupBy(s => s.Id).Select(g => g.First()).OrderBy(s => s.Name).ToList(),
            Amenities = courts.SelectMany(c => c.Amenities).GroupBy(a => a.AmenityId)
                .Select(g => new IdNameResponse { Id = g.Key, Name = g.First().Amenity.Name }).OrderBy(a => a.Name).ToList(),
            Facilities = cards
        };
    }

    // =====================================================================
    // Recommendations (story 66) — transparent rules, each result says why it was shown
    // =====================================================================

    public async Task<List<RecommendationResponse>> GetRecommendationsAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.Recommendations, cancellationToken);
        count = Math.Clamp(count, 1, 20);

        var user = await _db.Users.AsNoTracking().Include(u => u.SportProfiles).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("User no longer exists.");

        var bookedFacilityIds = await _db.Bookings.AsNoTracking()
            .Where(b => b.CustomerUserId == userId && (b.Status == BookingStatus.Completed || b.Status == BookingStatus.Confirmed))
            .Select(b => b.FacilityId).Distinct().ToListAsync(cancellationToken);
        var bookedSportIds = await _db.Bookings.AsNoTracking()
            .Where(b => b.CustomerUserId == userId).Select(b => b.SportId).Distinct().ToListAsync(cancellationToken);
        var favouriteSports = user.SportProfiles.Select(p => p.SportId).Union(bookedSportIds).ToHashSet();

        var candidates = await LoadPublic().OrderByDescending(f => f.PublishedAt).Take(MaxCandidates).ToListAsync(cancellationToken);

        // Score: +3 a sport you play/book, +2 in your city, +1 a place you've booked before. Ties: newest first.
        var scored = candidates.Select(f =>
        {
            var reasons = new List<string>();
            var score = 0;
            if (f.Sports.Any(s => favouriteSports.Contains(s.SportId))) { score += 3; reasons.Add("Offers a sport you play"); }
            if (user.City is not null && string.Equals(f.City, user.City, StringComparison.OrdinalIgnoreCase)) { score += 2; reasons.Add("In your city"); }
            if (bookedFacilityIds.Contains(f.Id)) { score += 1; reasons.Add("You've booked here before"); }
            return (Facility: f, Score: score, Reasons: reasons);
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(count)
        .ToList();

        var cards = await ToCardsAsync(scored.Select(x => x.Facility).ToList(), null, null, null, cancellationToken);
        return scored.Select((x, i) => new RecommendationResponse { Facility = cards[i], Reasons = x.Reasons }).ToList();
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private IQueryable<Facility> LoadPublic() => _db.Facilities.AsNoTracking().Public()
        .Include(f => f.Business)
        .Include(f => f.Images)
        .Include(f => f.Amenities).ThenInclude(a => a.Amenity)
        .Include(f => f.Sports).ThenInclude(s => s.Sport)
        .Include(f => f.OperatingHours)
        .Include(f => f.ScheduleExceptions)
        .Include(f => f.PriceRules)
        .AsSplitQuery();

    private async Task<Guid?> ResolveSportIdAsync(string? sport, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sport))
        {
            return null;
        }

        if (Guid.TryParse(sport, out var id))
        {
            return id;
        }

        var slug = sport.Trim().ToLowerInvariant();
        return await _db.Sports.AsNoTracking().Where(s => s.Slug == slug).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("Sport not found.");
    }

    private async Task<List<FacilityCardResponse>> ToCardsAsync(List<Facility> facilities, Guid? sportId, DateOnly? date, FacilitySearchQuery? query, CancellationToken cancellationToken)
    {
        if (facilities.Count == 0)
        {
            return new List<FacilityCardResponse>();
        }

        var sportNames = await _translations.GetNamesAsync("Sport", cancellationToken);
        var timeZone = facilities[0].TimeZone;
        var fromDate = date ?? LocalTime.Today(timeZone);
        var days = date.HasValue ? 1 : 7;
        var (rangeStart, _) = LocalTime.DayRangeUtc(fromDate, timeZone);
        var (_, rangeEnd) = LocalTime.DayRangeUtc(fromDate.AddDays(days + 1), timeZone);
        var busy = await _availability.LoadBusyAsync(facilities.Select(f => f.Id).ToList(), rangeStart, rangeEnd, null, cancellationToken);
        var ratings = await RatingsAsync(facilities.Select(f => f.Id).ToList(), cancellationToken);

        var cards = new List<FacilityCardResponse>();
        foreach (var f in facilities)
        {
            var slotSport = sportId ?? f.Sports.OrderBy(s => s.Sport.SortOrder).Select(s => (Guid?)s.SportId).FirstOrDefault();
            var facilitySport = f.Sports.FirstOrDefault(s => s.SportId == slotSport);
            var slots = new List<SlotResponse>();

            if (slotSport.HasValue && facilitySport is not null)
            {
                var rules = SportRules.For(f, facilitySport);
                int? duration = query?.DurationMinutes is int d && rules.DurationOptions.Contains(d) ? d : null;

                for (var i = 0; i < days && slots.Count < NextSlotCount; i++)
                {
                    var day = _availability.BuildDay(f, slotSport.Value, fromDate.AddDays(i), duration, busy[f.Id], includeUnavailable: false);
                    var matching = day.Slots.AsEnumerable();
                    if (query?.Time is TimeOnly preferred)
                    {
                        var flex = Math.Clamp(query.TimeFlexMinutes, 0, 720);
                        matching = matching.Where(s => MinutesApart(TimeOnly.FromDateTime(s.LocalStartsAt), preferred) <= flex);
                    }
                    slots.AddRange(matching.Take(NextSlotCount - slots.Count));
                }
            }

            var rating = ratings.GetValueOrDefault(f.Id);
            cards.Add(new FacilityCardResponse
            {
                Id = f.Id,
                Slug = f.Slug,
                Name = f.Name,
                BusinessName = f.Business.BusinessName,
                Type = f.Type,
                IsIndoor = f.IsIndoor,
                AddressLine = f.AddressLine,
                City = f.City,
                Latitude = f.Latitude,
                Longitude = f.Longitude,
                CoverImageUrl = f.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
                Sports = f.Sports.OrderBy(s => s.Sport.SortOrder)
                    .Select(s => new IdNameResponse { Id = s.SportId, Name = sportNames.GetValueOrDefault(s.SportId, s.Sport.Name) }).ToList(),
                FromPrice = slots.Where(s => s.Price.HasValue).Select(s => s.Price).DefaultIfEmpty(null).Min()
                            ?? PricingEngine.FromPrice(f.PriceRules, sportId, f.DurationOptions),
                SlotsSportId = slotSport,
                NextSlots = slots,
                DistanceKm = query?.Latitude is decimal lat && query.Longitude is decimal lng && f.Latitude.HasValue && f.Longitude.HasValue
                    ? Math.Round(DistanceKm(lat, lng, f.Latitude.Value, f.Longitude.Value), 2)
                    : null,
                AverageRating = rating.Average,
                ReviewCount = rating.Count
            });
        }

        return cards;
    }

    private async Task<List<FacilityCardResponse>> GetSponsoredAsync(Guid? sportId, string? city, CancellationToken cancellationToken)
    {
        if (!await _flags.IsEnabledAsync(FeatureFlagKeys.PromotedListings, cancellationToken))
        {
            return new List<FacilityCardResponse>();
        }

        var now = DateTime.UtcNow;
        var promotions = await _db.FacilityPromotions.AsNoTracking()
            .Where(p => p.IsActive && p.StartsAt <= now && p.EndsAt > now)
            .Select(p => new { p.FacilityId, p.Label })
            .ToListAsync(cancellationToken);
        if (promotions.Count == 0)
        {
            return new List<FacilityCardResponse>();
        }

        var ids = promotions.Select(p => p.FacilityId).ToList();
        var q = LoadPublic().Where(f => ids.Contains(f.Id));
        if (sportId.HasValue) q = q.Where(f => f.Sports.Any(s => s.SportId == sportId));
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(f => f.City != null && EF.Functions.ILike(f.City, Text.EscapeLike(city.Trim()), "\\"));

        var cards = await ToCardsAsync(await q.Take(5).ToListAsync(cancellationToken), sportId, null, null, cancellationToken);
        foreach (var card in cards)
        {
            card.IsSponsored = true;
            card.SponsoredLabel = promotions.First(p => p.FacilityId == card.Id).Label;
        }
        return cards;
    }

    private async Task<(double? Average, int Count)> RatingAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        var ratings = await RatingsAsync(new List<Guid> { facilityId }, cancellationToken);
        return ratings.GetValueOrDefault(facilityId);
    }

    private async Task<Dictionary<Guid, (double? Average, int Count)>> RatingsAsync(List<Guid> facilityIds, CancellationToken cancellationToken)
    {
        if (!await _flags.IsEnabledAsync(FeatureFlagKeys.Reviews, cancellationToken))
        {
            return new Dictionary<Guid, (double?, int)>();
        }

        var rows = await _db.Reviews.AsNoTracking()
            .Where(r => facilityIds.Contains(r.FacilityId) && r.Status == ReviewStatus.Published)
            .GroupBy(r => r.FacilityId)
            .Select(g => new { g.Key, Average = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Key, r => ((double?)Math.Round(r.Average, 1), r.Count));
    }

    /// <summary>Distance between two clock times, wrapping around midnight (23:30 vs 00:30 = 60).</summary>
    private static int MinutesApart(TimeOnly a, TimeOnly b)
    {
        var diff = Math.Abs((a.Hour * 60 + a.Minute) - (b.Hour * 60 + b.Minute));
        return Math.Min(diff, 1440 - diff);
    }

    private static double DistanceKm(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        const double earthRadiusKm = 6371;
        double ToRad(decimal deg) => (double)deg * Math.PI / 180;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
