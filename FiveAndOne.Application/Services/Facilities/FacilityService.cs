using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Application.Services.Subscriptions;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FiveAndOne.Application.Services.Facilities;

public interface IFacilityService
{
    // ---- Business (always scoped to the caller's business) ----
    Task<FacilityUsageResponse> GetUsageAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default);
    Task<List<FacilitySummaryResponse>> GetMyFacilitiesAsync(Guid userId, Guid? businessId, FacilityStatus? status, CancellationToken cancellationToken = default);
    Task<FacilityResponse> GetMyFacilityAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default);
    Task<FacilityResponse> CreateAsync(Guid userId, Guid? businessId, SaveFacilityRequest request, CancellationToken cancellationToken = default);
    Task<FacilityResponse> UpdateAsync(Guid userId, Guid? businessId, Guid facilityId, SaveFacilityRequest request, CancellationToken cancellationToken = default);
    Task<FacilityResponse> PublishAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default);
    Task<FacilityResponse> HideAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default);

    // ---- Admin ----
    Task<PagedResponse<FacilitySummaryResponse>> GetAllAsync(FacilityStatus? status, Guid? businessId, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<FacilityResponse> GetAnyAsync(Guid facilityId, CancellationToken cancellationToken = default);
    Task<FacilityResponse> SuspendAsync(Guid actorId, Guid facilityId, string reason, CancellationToken cancellationToken = default);
    Task<FacilityResponse> UnsuspendAsync(Guid actorId, Guid facilityId, CancellationToken cancellationToken = default);
    Task<FacilityResponse> AdminHideAsync(Guid actorId, Guid facilityId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Loads a facility with everything FacilityResponse needs.</summary>
    Task<Facility> LoadDetailsAsync(Guid facilityId, CancellationToken cancellationToken = default);
}

public class FacilityService : IFacilityService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessAccess _access;
    private readonly IAuditService _audit;
    private readonly FacilitySettings _settings;

    public FacilityService(ApplicationDbContext db, IBusinessAccess access, IAuditService audit, IOptions<FacilitySettings> settings)
    {
        _db = db;
        _access = access;
        _audit = audit;
        _settings = settings.Value;
    }

    private IQueryable<Facility> WithDetails() => _db.Facilities
        .Include(x => x.Business)
        .Include(x => x.Images)
        .Include(x => x.Amenities).ThenInclude(x => x.Amenity)
        .Include(x => x.Sports).ThenInclude(x => x.Sport)
        .Include(x => x.OperatingHours)
        .Include(x => x.PriceRules)
        .AsSplitQuery();

    public async Task<Facility> LoadDetailsAsync(Guid facilityId, CancellationToken cancellationToken = default) =>
        await WithDetails().FirstOrDefaultAsync(x => x.Id == facilityId, cancellationToken)
        ?? throw new NotFoundException("Facility not found.");

    // =====================================================================
    // Business
    // =====================================================================

    public async Task<FacilityUsageResponse> GetUsageAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);
        var limits = await PlanLimits.GetAsync(_db, context.BusinessId, _settings.MaxFacilitiesWithoutSubscription, cancellationToken);

        return new FacilityUsageResponse
        {
            HasActiveSubscription = limits.HasActiveSubscription,
            PlanName = limits.PlanName,
            MaxFacilities = limits.MaxFacilities,
            TotalFacilities = await _db.Facilities.CountAsync(x => x.BusinessId == context.BusinessId, cancellationToken),
            PublishedFacilities = await _db.Facilities.CountAsync(x => x.BusinessId == context.BusinessId && x.Status == FacilityStatus.Published, cancellationToken)
        };
    }

    public async Task<List<FacilitySummaryResponse>> GetMyFacilitiesAsync(Guid userId, Guid? businessId, FacilityStatus? status, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);

        var query = WithDetails().AsNoTracking().Where(x => x.BusinessId == context.BusinessId);
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var facilities = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return facilities.Select(FacilitySummaryResponse.FromEntity).ToList();
    }

    public async Task<FacilityResponse> GetMyFacilityAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);
        return FacilityResponse.FromEntity(await GetOwnedAsync(context, facilityId, cancellationToken));
    }

    public async Task<FacilityResponse> CreateAsync(Guid userId, Guid? businessId, SaveFacilityRequest request, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageFacilities, cancellationToken);
        EnsureBusinessCanEdit(context);

        var limits = await PlanLimits.GetAsync(_db, context.BusinessId, _settings.MaxFacilitiesWithoutSubscription, cancellationToken);
        var total = await _db.Facilities.CountAsync(x => x.BusinessId == context.BusinessId, cancellationToken);
        if (total >= limits.MaxFacilities)
        {
            throw new PlanLimitExceededException(limits.HasActiveSubscription
                ? $"Your plan ({limits.PlanName}) allows up to {Facilities(limits.MaxFacilities)} and you already have {total}. " +
                  "Delete a facility you no longer need or upgrade your plan to add more."
                : $"Without a subscription you can prepare up to {Facilities(limits.MaxFacilities)}. Choose a plan to add more.");
        }

        // New facilities always start as Draft.
        var facility = new Facility
        {
            BusinessId = context.BusinessId,
            Business = context.Business,
            Status = FacilityStatus.Draft,
            CancellationCutoffHours = null
        };
        facility.Slug = Slugs.Create(request.Name!, facility.Id, "facility");

        await ApplyRequestAsync(context, facility, request, cancellationToken);

        _db.Facilities.Add(facility);
        _audit.Log(userId, "facility.created", "Facility", facility.Id.ToString(), facility.Name, businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> UpdateAsync(Guid userId, Guid? businessId, Guid facilityId, SaveFacilityRequest request, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageFacilities, cancellationToken);
        var facility = await GetOwnedAsync(context, facilityId, cancellationToken);

        await ApplyRequestAsync(context, facility, request, cancellationToken);
        EnsureStillComplete(facility);

        facility.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> PublishAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageFacilities, cancellationToken);
        var facility = await GetOwnedAsync(context, facilityId, cancellationToken);

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

        EnsureBusinessCanEdit(context);
        if (context.Business.Status != BusinessStatus.Active)
        {
            throw new ForbiddenException("Your business is not active, so facilities can't be published.", "business_not_active");
        }

        var missing = FacilityPublishRules.GetMissingFields(facility);
        if (missing.Count > 0)
        {
            throw new RequestValidationException("Complete the required information before publishing.", missing, "facility_incomplete");
        }

        var limits = await PlanLimits.GetAsync(_db, context.BusinessId, _settings.MaxFacilitiesWithoutSubscription, cancellationToken);
        if (!limits.HasActiveSubscription)
        {
            throw new PlanLimitExceededException("Choose a subscription plan to publish facilities.", "subscription_required");
        }

        var published = await _db.Facilities.CountAsync(x => x.BusinessId == context.BusinessId && x.Status == FacilityStatus.Published, cancellationToken);
        if (published >= limits.MaxFacilities)
        {
            throw new PlanLimitExceededException(
                $"Your plan ({limits.PlanName}) allows up to {limits.MaxFacilities} published {(limits.MaxFacilities == 1 ? "facility" : "facilities")} and you already have {published}. " +
                "Hide another facility or upgrade your plan to publish this one.");
        }

        var now = DateTime.UtcNow;
        facility.Status = FacilityStatus.Published;
        facility.PublishedAt = now;
        facility.UpdatedAt = now;
        _audit.Log(userId, "facility.published", "Facility", facility.Id.ToString(), facility.Name, businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> HideAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageFacilities, cancellationToken);
        var facility = await GetOwnedAsync(context, facilityId, cancellationToken);

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
        _audit.Log(userId, "facility.hidden", "Facility", facility.Id.ToString(), facility.Name, businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task DeleteAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageFacilities, cancellationToken);
        var facility = await GetOwnedAsync(context, facilityId, cancellationToken);

        // Reservation history must be kept, so a facility that was ever booked can only be hidden.
        if (await _db.Bookings.AnyAsync(x => x.FacilityId == facility.Id, cancellationToken))
        {
            throw new ConflictException("This facility has reservations and can't be deleted. Hide it instead.", "facility_has_bookings");
        }

        _db.Facilities.Remove(facility);
        _audit.Log(userId, "facility.deleted", "Facility", facility.Id.ToString(), facility.Name, businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // =====================================================================
    // Admin
    // =====================================================================

    public async Task<PagedResponse<FacilitySummaryResponse>> GetAllAsync(FacilityStatus? status, Guid? businessId, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = WithDetails().AsNoTracking();

        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (businessId.HasValue) query = query.Where(x => x.BusinessId == businessId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{Text.EscapeLike(search.Trim())}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\") || EF.Functions.ILike(x.Business.BusinessName, pattern, "\\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return PagedResponse<FacilitySummaryResponse>.Create(items.Select(FacilitySummaryResponse.FromEntity).ToList(), page, pageSize, total);
    }

    public async Task<FacilityResponse> GetAnyAsync(Guid facilityId, CancellationToken cancellationToken = default) =>
        FacilityResponse.FromEntity(await LoadDetailsAsync(facilityId, cancellationToken));

    public async Task<FacilityResponse> SuspendAsync(Guid actorId, Guid facilityId, string reason, CancellationToken cancellationToken = default)
    {
        var facility = await LoadDetailsAsync(facilityId, cancellationToken);

        var now = DateTime.UtcNow;
        facility.Status = FacilityStatus.Suspended;
        facility.SuspendedAt = now;
        facility.SuspensionReason = reason.Trim();
        facility.UpdatedAt = now;
        _audit.Log(actorId, "facility.suspended", "Facility", facility.Id.ToString(), reason.Trim(), businessId: facility.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> UnsuspendAsync(Guid actorId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        var facility = await LoadDetailsAsync(facilityId, cancellationToken);

        if (facility.Status != FacilityStatus.Suspended)
        {
            throw new ConflictException("This facility is not suspended.", "invalid_status");
        }

        // Comes back Hidden: the owner decides when to publish again.
        facility.Status = FacilityStatus.Hidden;
        facility.SuspendedAt = null;
        facility.SuspensionReason = null;
        facility.UpdatedAt = DateTime.UtcNow;
        _audit.Log(actorId, "facility.unsuspended", "Facility", facility.Id.ToString(), null, businessId: facility.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    public async Task<FacilityResponse> AdminHideAsync(Guid actorId, Guid facilityId, string? reason, CancellationToken cancellationToken = default)
    {
        var facility = await LoadDetailsAsync(facilityId, cancellationToken);
        if (facility.Status != FacilityStatus.Published)
        {
            throw new ConflictException("Only published facilities can be hidden.", "invalid_status");
        }

        facility.Status = FacilityStatus.Hidden;
        facility.UpdatedAt = DateTime.UtcNow;
        _audit.Log(actorId, "facility.hidden_by_admin", "Facility", facility.Id.ToString(), reason, businessId: facility.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return FacilityResponse.FromEntity(facility);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private async Task<Facility> GetOwnedAsync(BusinessContext context, Guid facilityId, CancellationToken cancellationToken)
    {
        var facility = await WithDetails().FirstOrDefaultAsync(x => x.Id == facilityId, cancellationToken);
        if (facility is null || facility.BusinessId != context.BusinessId)
        {
            throw new NotFoundException("Facility not found.");
        }
        return facility;
    }

    /// <summary>A live facility must stay complete; otherwise customers would see a broken listing.</summary>
    internal static void EnsureStillComplete(Facility facility)
    {
        if (facility.Status != FacilityStatus.Published)
        {
            return;
        }

        var missing = FacilityPublishRules.GetMissingFields(facility);
        if (missing.Count > 0)
        {
            throw new RequestValidationException(
                "A published facility must keep all required information. Hide it first if you need to save it incomplete.",
                missing, "facility_incomplete");
        }
    }

    private static void EnsureBusinessCanEdit(BusinessContext context)
    {
        if (context.Business.Status == BusinessStatus.Suspended)
        {
            throw new ForbiddenException("Your business is suspended by an administrator.", "business_suspended");
        }
    }

    /// <summary>Copies the request onto the entity after checking references (sports, amenities, images, venue).</summary>
    private async Task ApplyRequestAsync(BusinessContext context, Facility facility, SaveFacilityRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        var sportIds = request.SportIds.Distinct().ToList();
        var sports = await _db.Sports.Where(x => sportIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var keptSportIds = facility.Sports.Select(s => s.SportId).ToHashSet();
        // Deactivated catalogue sports can't be selected for new configurations (story 09).
        if (sports.Count != sportIds.Count || sports.Any(s => !s.IsActive && !keptSportIds.Contains(s.Id)))
        {
            errors["sportIds"] = new[] { "One or more selected sports do not exist or are no longer available." };
        }

        var amenityIds = request.AmenityIds.Distinct().ToList();
        var amenities = await _db.Amenities.Where(x => amenityIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (amenities.Count != amenityIds.Count)
        {
            errors["amenityIds"] = new[] { "One or more selected amenities do not exist." };
        }

        List<string>? imageUrls = null;
        if (request.ImageUrls is not null)
        {
            imageUrls = request.ImageUrls.Select(u => u?.Trim() ?? string.Empty).ToList();
            if (imageUrls.Count > _settings.MaxImagesPerFacility)
            {
                errors["imageUrls"] = new[] { $"A facility can have at most {_settings.MaxImagesPerFacility} images." };
            }

            for (var i = 0; i < imageUrls.Count; i++)
            {
                if (!IsValidImageUrl(imageUrls[i]))
                {
                    errors[$"imageUrls[{i}]"] = new[] { "Must be a valid URL of at most 500 characters." };
                }
            }
        }

        if (request.VenueId.HasValue &&
            !await _db.Venues.AnyAsync(v => v.Id == request.VenueId && v.BusinessId == context.BusinessId, cancellationToken))
        {
            errors["venueId"] = new[] { "Venue not found." };
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException("One or more validation errors occurred.", errors);
        }

        facility.Name = request.Name!.Trim();
        facility.Description = Text.NullIfEmpty(request.Description);
        facility.Type = request.Type;
        facility.Capacity = request.Capacity;
        facility.IsIndoor = request.IsIndoor;
        facility.Surface = Text.NullIfEmpty(request.Surface);
        facility.AddressLine = Text.NullIfEmpty(request.AddressLine);
        facility.City = Text.NullIfEmpty(request.City);
        facility.Country = string.IsNullOrWhiteSpace(request.Country) ? "XK" : request.Country.Trim().ToUpperInvariant();
        facility.Latitude = request.Latitude;
        facility.Longitude = request.Longitude;
        facility.VenueId = request.VenueId;

        // Sports: remove unselected, add newly selected (diff, so unchanged rows keep their settings).
        foreach (var existing in facility.Sports.Where(s => !sportIds.Contains(s.SportId)).ToList())
        {
            facility.Sports.Remove(existing);
            // Sport-specific hours and prices go with it.
            foreach (var hours in facility.OperatingHours.Where(h => h.SportId == existing.SportId).ToList()) facility.OperatingHours.Remove(hours);
            foreach (var rule in facility.PriceRules.Where(r => r.SportId == existing.SportId).ToList()) facility.PriceRules.Remove(rule);
        }
        foreach (var sport in sports.Where(s => facility.Sports.All(fs => fs.SportId != s.Id)))
        {
            facility.Sports.Add(new FacilitySport { FacilityId = facility.Id, SportId = sport.Id, Sport = sport });
        }

        foreach (var existing in facility.Amenities.Where(a => !amenityIds.Contains(a.AmenityId)).ToList())
        {
            facility.Amenities.Remove(existing);
        }
        foreach (var amenity in amenities.Where(a => facility.Amenities.All(fa => fa.AmenityId != a.Id)))
        {
            facility.Amenities.Add(new FacilityAmenity { FacilityId = facility.Id, AmenityId = amenity.Id, Amenity = amenity });
        }

        if (imageUrls is not null)
        {
            // Replace the list in the given order; keep rows whose URL didn't change.
            foreach (var image in facility.Images.Where(i => !imageUrls.Contains(i.Url)).ToList())
            {
                facility.Images.Remove(image);
            }

            for (var i = 0; i < imageUrls.Count; i++)
            {
                var image = facility.Images.FirstOrDefault(x => x.Url == imageUrls[i]);
                if (image is null)
                {
                    image = new FacilityImage { FacilityId = facility.Id, Url = imageUrls[i] };
                    facility.Images.Add(image);
                }
                image.SortOrder = i;
                image.IsCover = i == 0;
            }
        }
    }

    // Absolute http(s) URL, or a relative path served by this API (uploaded files).
    private static bool IsValidImageUrl(string url) =>
        url.Length is > 0 and <= 500 &&
        ((Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
         || url.StartsWith("/uploads/", StringComparison.Ordinal));

    private static string Facilities(int count) => count == 1 ? "1 facility" : $"{count} facilities";
}
