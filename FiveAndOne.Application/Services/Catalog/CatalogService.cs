using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Catalog;
using FiveAndOne.Application.Services.Availability;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Catalog;

public interface ICatalogService
{
    Task<List<SportResponse>> GetSportsAsync(bool includeCounts, CancellationToken cancellationToken = default);
    Task<SportResponse> GetSportAsync(string idOrSlug, CancellationToken cancellationToken = default);
    Task<List<AmenityResponse>> GetAmenitiesAsync(CancellationToken cancellationToken = default);

    // Admin (stories 47/48: Sports/Categories)
    Task<List<AdminSportResponse>> GetAllSportsAsync(CancellationToken cancellationToken = default);
    Task<AdminSportResponse> SaveSportAsync(Guid actorId, Guid? sportId, SaveSportRequest request, CancellationToken cancellationToken = default);
    Task<List<AdminAmenityResponse>> GetAllAmenitiesAsync(CancellationToken cancellationToken = default);
    Task<AdminAmenityResponse> SaveAmenityAsync(Guid actorId, Guid? amenityId, SaveAmenityRequest request, CancellationToken cancellationToken = default);
    Task DeleteAmenityAsync(Guid actorId, Guid amenityId, CancellationToken cancellationToken = default);
}

public class CatalogService : ICatalogService
{
    private static readonly string[] Languages = { "sq", "tr", "sr" };

    private readonly ApplicationDbContext _db;
    private readonly ITranslationService _translations;
    private readonly IAuditService _audit;

    public CatalogService(ApplicationDbContext db, ITranslationService translations, IAuditService audit)
    {
        _db = db;
        _translations = translations;
        _audit = audit;
    }

    public async Task<List<SportResponse>> GetSportsAsync(bool includeCounts, CancellationToken cancellationToken = default)
    {
        var sports = await _db.Sports.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync(cancellationToken);
        var names = await _translations.GetNamesAsync("Sport", cancellationToken);
        var result = sports.Select(s => SportResponse.FromEntity(s, names)).ToList();

        if (includeCounts)
        {
            var publicIds = _db.Facilities.Public().Select(f => f.Id);
            var counts = await _db.FacilitySports.AsNoTracking()
                .Where(fs => publicIds.Contains(fs.FacilityId))
                .GroupBy(fs => fs.SportId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

            foreach (var sport in result)
            {
                sport.FacilityCount = counts.GetValueOrDefault(sport.Id);
            }
        }

        return result;
    }

    public async Task<SportResponse> GetSportAsync(string idOrSlug, CancellationToken cancellationToken = default)
    {
        var query = _db.Sports.AsNoTracking().Where(s => s.IsActive);
        var sport = Guid.TryParse(idOrSlug, out var id)
            ? await query.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            : await query.FirstOrDefaultAsync(s => s.Slug == idOrSlug.ToLower(), cancellationToken);

        if (sport is null)
        {
            throw new NotFoundException("Sport not found.");
        }

        var names = await _translations.GetNamesAsync("Sport", cancellationToken);
        return SportResponse.FromEntity(sport, names);
    }

    public async Task<List<AmenityResponse>> GetAmenitiesAsync(CancellationToken cancellationToken = default)
    {
        var amenities = await _db.Amenities.AsNoTracking().ToListAsync(cancellationToken);
        var names = await _translations.GetNamesAsync("Amenity", cancellationToken);
        return amenities.Select(a => AmenityResponse.FromEntity(a, names)).OrderBy(a => a.Name).ToList();
    }

    public async Task<List<AdminSportResponse>> GetAllSportsAsync(CancellationToken cancellationToken = default)
    {
        var sports = await _db.Sports.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync(cancellationToken);
        var translations = await _db.Translations.AsNoTracking().Where(t => t.EntityType == "Sport" && t.Field == "Name").ToListAsync(cancellationToken);

        return sports.Select(s => ToAdmin(s, translations.Where(t => t.EntityId == s.Id).ToDictionary(t => t.Language, t => t.Value))).ToList();
    }

    public async Task<AdminSportResponse> SaveSportAsync(Guid actorId, Guid? sportId, SaveSportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DefaultMaxParticipants < request.DefaultMinParticipants)
        {
            throw RequestValidationException.For("defaultMaxParticipants", "Max participants can't be lower than min participants.");
        }

        Sport sport;
        if (sportId.HasValue)
        {
            sport = await _db.Sports.FirstOrDefaultAsync(s => s.Id == sportId, cancellationToken) ?? throw new NotFoundException("Sport not found.");
            sport.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            sport = new Sport();
            _db.Sports.Add(sport);
        }

        var name = request.Name!.Trim();
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugFromName(name) : request.Slug.Trim();
        if (string.IsNullOrEmpty(slug))
        {
            slug = $"sport-{sport.Id.ToString("N")[..6]}";
        }

        if (await _db.Sports.AnyAsync(s => s.Id != sport.Id && (s.Name == name || s.Slug == slug), cancellationToken))
        {
            throw new ConflictException("A sport with this name or slug already exists.", "duplicate");
        }

        sport.Name = name;
        sport.Slug = slug;
        sport.Description = Text.NullIfEmpty(request.Description);
        sport.Category = request.Category;
        sport.IconUrl = Text.NullIfEmpty(request.IconUrl);
        sport.IsActive = request.IsActive;
        sport.SortOrder = request.SortOrder;
        sport.DefaultMinParticipants = request.DefaultMinParticipants;
        sport.DefaultMaxParticipants = request.DefaultMaxParticipants;

        await _translations.SetAsync("Sport", sport.Id, "Name", Filter(request.NameTranslations), cancellationToken);
        _audit.Log(actorId, sportId.HasValue ? "sport.updated" : "sport.created", "Sport", sport.Id.ToString(), $"{name} (active: {sport.IsActive})");
        await _db.SaveChangesAsync(cancellationToken);

        return ToAdmin(sport, await _translations.GetAllForAsync("Sport", sport.Id, "Name", cancellationToken));
    }

    public async Task<List<AdminAmenityResponse>> GetAllAmenitiesAsync(CancellationToken cancellationToken = default)
    {
        var amenities = await _db.Amenities.AsNoTracking()
            .Select(a => new { Amenity = a, Count = a.Facilities.Count })
            .OrderBy(x => x.Amenity.Name).ToListAsync(cancellationToken);
        var translations = await _db.Translations.AsNoTracking().Where(t => t.EntityType == "Amenity" && t.Field == "Name").ToListAsync(cancellationToken);

        return amenities.Select(x => new AdminAmenityResponse
        {
            Id = x.Amenity.Id,
            Name = x.Amenity.Name,
            Icon = x.Amenity.Icon,
            FacilityCount = x.Count,
            NameTranslations = translations.Where(t => t.EntityId == x.Amenity.Id).ToDictionary(t => t.Language, t => t.Value)
        }).ToList();
    }

    public async Task<AdminAmenityResponse> SaveAmenityAsync(Guid actorId, Guid? amenityId, SaveAmenityRequest request, CancellationToken cancellationToken = default)
    {
        Amenity amenity;
        if (amenityId.HasValue)
        {
            amenity = await _db.Amenities.FirstOrDefaultAsync(a => a.Id == amenityId, cancellationToken) ?? throw new NotFoundException("Amenity not found.");
            amenity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            amenity = new Amenity();
            _db.Amenities.Add(amenity);
        }

        var name = request.Name!.Trim();
        if (await _db.Amenities.AnyAsync(a => a.Id != amenity.Id && a.Name == name, cancellationToken))
        {
            throw new ConflictException("An amenity with this name already exists.", "duplicate");
        }

        amenity.Name = name;
        amenity.Icon = Text.NullIfEmpty(request.Icon);
        await _translations.SetAsync("Amenity", amenity.Id, "Name", Filter(request.NameTranslations), cancellationToken);
        _audit.Log(actorId, amenityId.HasValue ? "amenity.updated" : "amenity.created", "Amenity", amenity.Id.ToString(), name);
        await _db.SaveChangesAsync(cancellationToken);

        return new AdminAmenityResponse
        {
            Id = amenity.Id,
            Name = amenity.Name,
            Icon = amenity.Icon,
            FacilityCount = await _db.FacilityAmenities.CountAsync(x => x.AmenityId == amenity.Id, cancellationToken),
            NameTranslations = await _translations.GetAllForAsync("Amenity", amenity.Id, "Name", cancellationToken)
        };
    }

    public async Task DeleteAmenityAsync(Guid actorId, Guid amenityId, CancellationToken cancellationToken = default)
    {
        var amenity = await _db.Amenities.FirstOrDefaultAsync(a => a.Id == amenityId, cancellationToken) ?? throw new NotFoundException("Amenity not found.");
        if (await _db.FacilityAmenities.AnyAsync(x => x.AmenityId == amenityId, cancellationToken))
        {
            throw new ConflictException("This amenity is used by facilities and can't be deleted.", "amenity_in_use");
        }

        _db.Amenities.Remove(amenity);
        _db.Translations.RemoveRange(_db.Translations.Where(t => t.EntityType == "Amenity" && t.EntityId == amenityId));
        _audit.Log(actorId, "amenity.deleted", "Amenity", amenityId.ToString(), amenity.Name);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Only non-English languages are stored as translations (English is the base name).</summary>
    private static Dictionary<string, string> Filter(Dictionary<string, string> values) =>
        values.Where(kv => Languages.Contains(kv.Key.Trim().ToLowerInvariant()))
            .ToDictionary(kv => kv.Key.Trim().ToLowerInvariant(), kv => kv.Value);

    private static string SlugFromName(string name)
    {
        var slug = Slugs.Create(name, Guid.Empty, "sport");
        return slug[..slug.LastIndexOf('-')];
    }

    private static AdminSportResponse ToAdmin(Sport s, Dictionary<string, string> translations) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Slug = s.Slug,
        Description = s.Description,
        Category = s.Category,
        IconUrl = s.IconUrl,
        IsActive = s.IsActive,
        SortOrder = s.SortOrder,
        DefaultMinParticipants = s.DefaultMinParticipants,
        DefaultMaxParticipants = s.DefaultMaxParticipants,
        NameTranslations = translations
    };
}
