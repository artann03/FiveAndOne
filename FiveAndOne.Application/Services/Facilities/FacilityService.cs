using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Repositories.Catalog;
using FiveAndOne.Infrastructure.Repositories.Facilities;
using FiveAndOne.Infrastructure.Repositories.Users;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Facilities;

public class FacilityService : IFacilityService
{
    private readonly IFacilityRepository _facilities;
    private readonly ICatalogRepository _catalog;
    private readonly IUserRepository _users;
    private readonly FacilitySettings _settings;

    public FacilityService(
        IFacilityRepository facilities,
        ICatalogRepository catalog,
        IUserRepository users,
        IOptions<FacilitySettings> settings)
    {
        _facilities = facilities;
        _catalog = catalog;
        _users = users;
        _settings = settings.Value;
    }

    // =====================================================================
    // Business owner
    // =====================================================================

    public async Task<FacilityUsageResponse> GetUsageAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        await EnsureBusinessOwnerAsync(ownerId, cancellationToken);

        return new FacilityUsageResponse
        {
            MaxFacilities = _settings.MaxFacilitiesPerBusiness,
            TotalFacilities = await _facilities.CountByOwnerAsync(ownerId, null, cancellationToken),
            PublishedFacilities = await _facilities.CountByOwnerAsync(ownerId, FacilityStatus.Published, cancellationToken)
        };
    }

    public async Task<List<FacilitySummaryResponse>> GetMyFacilitiesAsync(
        Guid ownerId, FacilityStatus? status, CancellationToken cancellationToken = default)
    {
        await EnsureBusinessOwnerAsync(ownerId, cancellationToken);

        var facilities = await _facilities.GetByOwnerAsync(ownerId, status, cancellationToken);
        return facilities.Select(FacilitySummaryResponse.FromEntity).ToList();
    }

    public async Task<FacilityResponse> GetMyFacilityAsync(Guid ownerId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var facility = await GetOwnedFacilityAsync(ownerId, facilityId, cancellationToken);
        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> CreateAsync(Guid ownerId, SaveFacilityRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureBusinessOwnerAsync(ownerId, cancellationToken);

        var max = _settings.MaxFacilitiesPerBusiness;
        var total = await _facilities.CountByOwnerAsync(ownerId, null, cancellationToken);
        if (total >= max)
        {
            throw new PlanLimitExceededException(
                $"Your plan allows up to {Facilities(max)} and you already have {total}. " +
                "Delete a facility you no longer need or upgrade your plan to add more.");
        }

        // New facilities always start as Draft.
        var facility = new Facility { OwnerUserId = ownerId, Status = FacilityStatus.Draft };
        facility.Slug = CreateSlug(request.Name!, facility.Id);

        await ApplyRequestAsync(facility, request, cancellationToken);

        await _facilities.AddAsync(facility, cancellationToken);
        await _facilities.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> UpdateAsync(
        Guid ownerId, Guid facilityId, SaveFacilityRequest request, CancellationToken cancellationToken = default)
    {
        var facility = await GetOwnedFacilityAsync(ownerId, facilityId, cancellationToken);

        await ApplyRequestAsync(facility, request, cancellationToken);

        // A live facility must stay complete; otherwise customers would see a broken listing.
        if (facility.Status == FacilityStatus.Published)
        {
            var missing = FacilityPublishRules.GetMissingFields(facility);
            if (missing.Count > 0)
            {
                throw new RequestValidationException(
                    "A published facility must keep all required information. Hide it first if you need to save it incomplete.",
                    missing,
                    "facility_incomplete");
            }
        }

        facility.UpdatedAt = DateTime.UtcNow;
        await _facilities.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> PublishAsync(Guid ownerId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var facility = await GetOwnedFacilityAsync(ownerId, facilityId, cancellationToken);

        if (facility.Status == FacilityStatus.Suspended)
        {
            throw new ForbiddenException(
                $"This facility was suspended by an administrator and can't be published. Reason: {facility.SuspensionReason}",
                "facility_suspended");
        }

        if (facility.Status == FacilityStatus.Published)
        {
            return FacilityResponse.FromEntity(facility);   // already live: nothing to do
        }

        var missing = FacilityPublishRules.GetMissingFields(facility);
        if (missing.Count > 0)
        {
            throw new RequestValidationException(
                "Complete the required information before publishing.",
                missing,
                "facility_incomplete");
        }

        var max = _settings.MaxFacilitiesPerBusiness;
        var published = await _facilities.CountByOwnerAsync(ownerId, FacilityStatus.Published, cancellationToken);
        if (published >= max)
        {
            throw new PlanLimitExceededException(
                $"Your plan allows up to {max} published {(max == 1 ? "facility" : "facilities")} and you already have {published}. " +
                "Hide another facility or upgrade your plan to publish this one.");
        }

        var now = DateTime.UtcNow;
        facility.Status = FacilityStatus.Published;
        facility.PublishedAt = now;
        facility.UpdatedAt = now;
        await _facilities.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> HideAsync(Guid ownerId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var facility = await GetOwnedFacilityAsync(ownerId, facilityId, cancellationToken);

        switch (facility.Status)
        {
            case FacilityStatus.Hidden:
                return FacilityResponse.FromEntity(facility);   // already hidden
            case FacilityStatus.Draft:
                throw new ConflictException("Only published facilities can be hidden. Drafts are already not public.", "invalid_status");
            case FacilityStatus.Suspended:
                throw new ForbiddenException("This facility is suspended by an administrator.", "facility_suspended");
        }

        facility.Status = FacilityStatus.Hidden;
        facility.UpdatedAt = DateTime.UtcNow;
        await _facilities.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task DeleteAsync(Guid ownerId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var facility = await GetOwnedFacilityAsync(ownerId, facilityId, cancellationToken);

        // Booking history must be kept, so a facility that was ever booked can only be hidden.
        if (await _facilities.HasBookingsAsync(facility.Id, cancellationToken))
        {
            throw new ConflictException(
                "This facility has bookings and can't be deleted. Hide it instead.",
                "facility_has_bookings");
        }

        _facilities.Remove(facility);
        await _facilities.SaveChangesAsync(cancellationToken);
    }

    // =====================================================================
    // Public
    // =====================================================================

    public async Task<PagedResponse<PublicFacilityResponse>> SearchPublishedAsync(
        string? city, Guid? sportId, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var (items, total) = await _facilities.SearchPublishedAsync(city, sportId, search, page, pageSize, cancellationToken);

        return new PagedResponse<PublicFacilityResponse>
        {
            Items = items.Select(PublicFacilityResponse.FromEntity).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<PublicFacilityResponse> GetPublishedAsync(string idOrSlug, CancellationToken cancellationToken = default)
    {
        // Draft / Hidden / Suspended facilities look exactly like non-existent ones to the public.
        var facility = await _facilities.GetPublishedByIdOrSlugAsync(idOrSlug, cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        return PublicFacilityResponse.FromEntity(facility);
    }

    // =====================================================================
    // Admin
    // =====================================================================

    public async Task<PagedResponse<FacilitySummaryResponse>> GetAllAsync(
        FacilityStatus? status, Guid? ownerUserId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _facilities.GetPagedAsync(status, ownerUserId, page, pageSize, cancellationToken);

        return new PagedResponse<FacilitySummaryResponse>
        {
            Items = items.Select(FacilitySummaryResponse.FromEntity).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<FacilityResponse> GetAnyAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        var facility = await _facilities.GetByIdWithDetailsAsync(facilityId, cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> SuspendAsync(Guid facilityId, string reason, CancellationToken cancellationToken = default)
    {
        var facility = await _facilities.GetByIdWithDetailsAsync(facilityId, cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        var now = DateTime.UtcNow;
        facility.Status = FacilityStatus.Suspended;
        facility.SuspendedAt = now;
        facility.SuspensionReason = reason.Trim();
        facility.UpdatedAt = now;
        await _facilities.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> UnsuspendAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        var facility = await _facilities.GetByIdWithDetailsAsync(facilityId, cancellationToken)
            ?? throw new NotFoundException("Facility not found.");

        if (facility.Status != FacilityStatus.Suspended)
        {
            throw new ConflictException("This facility is not suspended.", "invalid_status");
        }

        // Comes back Hidden: the owner decides when to publish again.
        facility.Status = FacilityStatus.Hidden;
        facility.SuspendedAt = null;
        facility.SuspensionReason = null;
        facility.UpdatedAt = DateTime.UtcNow;
        await _facilities.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    /// <summary>Copies the request onto the entity after checking references (sports, amenities, images).</summary>
    private async Task ApplyRequestAsync(Facility facility, SaveFacilityRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        var sportIds = request.SportIds.Distinct().ToList();
        var sports = await _catalog.GetSportsByIdsAsync(sportIds, cancellationToken);
        if (sports.Count != sportIds.Count || sports.Any(s => !s.IsActive))
        {
            errors["sportIds"] = new[] { "One or more selected sports do not exist." };
        }

        var amenityIds = request.AmenityIds.Distinct().ToList();
        var amenities = await _catalog.GetAmenitiesByIdsAsync(amenityIds, cancellationToken);
        if (amenities.Count != amenityIds.Count)
        {
            errors["amenityIds"] = new[] { "One or more selected amenities do not exist." };
        }

        var imageUrls = request.ImageUrls.Select(u => u?.Trim() ?? string.Empty).ToList();
        if (imageUrls.Count > _settings.MaxImagesPerFacility)
        {
            errors["imageUrls"] = new[] { $"A facility can have at most {_settings.MaxImagesPerFacility} images." };
        }

        for (var i = 0; i < imageUrls.Count; i++)
        {
            if (!IsValidImageUrl(imageUrls[i]))
            {
                errors[$"imageUrls[{i}]"] = new[] { "Must be a valid http(s) URL of at most 500 characters." };
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException("One or more validation errors occurred.", errors);
        }

        facility.Name = request.Name!.Trim();
        facility.Description = NullIfEmpty(request.Description);
        facility.Type = request.Type;
        facility.Capacity = request.Capacity;
        facility.IsIndoor = request.IsIndoor;
        facility.Surface = NullIfEmpty(request.Surface);
        facility.AddressLine = NullIfEmpty(request.AddressLine);
        facility.City = NullIfEmpty(request.City);
        facility.Country = string.IsNullOrWhiteSpace(request.Country) ? "XK" : request.Country.Trim().ToUpperInvariant();
        facility.Latitude = request.Latitude;
        facility.Longitude = request.Longitude;

        // Sports: remove unselected, add newly selected (diff, so unchanged rows aren't re-inserted).
        foreach (var existing in facility.Sports.Where(s => !sportIds.Contains(s.SportId)).ToList())
        {
            facility.Sports.Remove(existing);
        }
        foreach (var sport in sports.Where(s => facility.Sports.All(fs => fs.SportId != s.Id)))
        {
            facility.Sports.Add(new FacilitySport { FacilityId = facility.Id, SportId = sport.Id, Sport = sport });
        }

        // Amenities: same diff approach.
        foreach (var existing in facility.Amenities.Where(a => !amenityIds.Contains(a.AmenityId)).ToList())
        {
            facility.Amenities.Remove(existing);
        }
        foreach (var amenity in amenities.Where(a => facility.Amenities.All(fa => fa.AmenityId != a.Id)))
        {
            facility.Amenities.Add(new FacilityAmenity { FacilityId = facility.Id, AmenityId = amenity.Id, Amenity = amenity });
        }

        // Images: replace the list in the given order; the first is the cover.
        facility.Images.Clear();
        for (var i = 0; i < imageUrls.Count; i++)
        {
            facility.Images.Add(new FacilityImage
            {
                FacilityId = facility.Id,
                Url = imageUrls[i],
                SortOrder = i,
                IsCover = i == 0
            });
        }
    }

    /// <summary>Returns the facility only if it belongs to this owner; otherwise 404 (we don't reveal others' facilities).</summary>
    private async Task<Facility> GetOwnedFacilityAsync(Guid ownerId, Guid facilityId, CancellationToken cancellationToken)
    {
        await EnsureBusinessOwnerAsync(ownerId, cancellationToken);

        var facility = await _facilities.GetByIdWithDetailsAsync(facilityId, cancellationToken);
        if (facility is null || facility.OwnerUserId != ownerId)
        {
            throw new NotFoundException("Facility not found.");
        }

        return facility;
    }

    // The controller already requires the Business role; re-check against the database so a stale token
    // (e.g. issued before a role change or suspension) can't manage facilities.
    private async Task EnsureBusinessOwnerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("User no longer exists.");
        }

        if (user.Role != UserRole.Business)
        {
            throw new ForbiddenException("Only business accounts can manage facilities.");
        }
    }

    private static bool IsValidImageUrl(string url) =>
        url.Length is > 0 and <= 500 &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string Facilities(int count) => count == 1 ? "1 facility" : $"{count} facilities";

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>"Futsal Pitch 1 – Prishtinë" -> "futsal-pitch-1-prishtine-3f9a1c". Suffix keeps it unique.</summary>
    private static string CreateSlug(string name, Guid id)
    {
        var normalized = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        var slug = Regex.Replace(ascii, "[^a-z0-9]+", "-").Trim('-');

        if (slug.Length > 160)
        {
            slug = slug[..160].Trim('-');
        }

        var suffix = id.ToString("N")[..6];
        return string.IsNullOrEmpty(slug) ? $"facility-{suffix}" : $"{slug}-{suffix}";
    }
}
