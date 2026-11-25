using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Infrastructure.Repositories.Facilities;

public interface IFacilityRepository
{
    /// <summary>Tracked, with images, amenities and sports loaded. No owner/status filter.</summary>
    Task<Facility?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<Facility>> GetByOwnerAsync(Guid ownerUserId, FacilityStatus? status, CancellationToken cancellationToken = default);
    Task<int> CountByOwnerAsync(Guid ownerUserId, FacilityStatus? status, CancellationToken cancellationToken = default);

    /// <summary>Public search: only Published facilities.</summary>
    Task<(List<Facility> Items, int TotalCount)> SearchPublishedAsync(
        string? city, Guid? sportId, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Public detail by id or slug: only Published facilities.</summary>
    Task<Facility?> GetPublishedByIdOrSlugAsync(string idOrSlug, CancellationToken cancellationToken = default);

    /// <summary>Admin listing across all owners.</summary>
    Task<(List<Facility> Items, int TotalCount)> GetPagedAsync(
        FacilityStatus? status, Guid? ownerUserId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<bool> HasBookingsAsync(Guid facilityId, CancellationToken cancellationToken = default);

    Task AddAsync(Facility facility, CancellationToken cancellationToken = default);
    void Remove(Facility facility);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
