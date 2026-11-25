using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Facilities;

public interface IFacilityService
{
    // ---- Business owner (always scoped to the current user's own facilities) ----
    Task<FacilityUsageResponse> GetUsageAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<List<FacilitySummaryResponse>> GetMyFacilitiesAsync(Guid ownerId, FacilityStatus? status, CancellationToken cancellationToken = default);
    Task<FacilityResponse> GetMyFacilityAsync(Guid ownerId, Guid facilityId, CancellationToken cancellationToken = default);
    Task<FacilityResponse> CreateAsync(Guid ownerId, SaveFacilityRequest request, CancellationToken cancellationToken = default);
    Task<FacilityResponse> UpdateAsync(Guid ownerId, Guid facilityId, SaveFacilityRequest request, CancellationToken cancellationToken = default);
    Task<FacilityResponse> PublishAsync(Guid ownerId, Guid facilityId, CancellationToken cancellationToken = default);
    Task<FacilityResponse> HideAsync(Guid ownerId, Guid facilityId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid ownerId, Guid facilityId, CancellationToken cancellationToken = default);

    // ---- Public (Published only) ----
    Task<PagedResponse<PublicFacilityResponse>> SearchPublishedAsync(
        string? city, Guid? sportId, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PublicFacilityResponse> GetPublishedAsync(string idOrSlug, CancellationToken cancellationToken = default);

    // ---- Admin ----
    Task<PagedResponse<FacilitySummaryResponse>> GetAllAsync(
        FacilityStatus? status, Guid? ownerUserId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<FacilityResponse> GetAnyAsync(Guid facilityId, CancellationToken cancellationToken = default);
    Task<FacilityResponse> SuspendAsync(Guid facilityId, string reason, CancellationToken cancellationToken = default);
    Task<FacilityResponse> UnsuspendAsync(Guid facilityId, CancellationToken cancellationToken = default);
}
