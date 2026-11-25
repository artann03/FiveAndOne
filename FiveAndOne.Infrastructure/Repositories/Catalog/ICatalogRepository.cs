using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Sports;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Infrastructure.Repositories.Catalog;

/// <summary>Read access to lookup lists: sports and amenities.</summary>
public interface ICatalogRepository
{
    Task<List<Sport>> GetSportsAsync(bool activeOnly, CancellationToken cancellationToken = default);
    Task<List<Sport>> GetSportsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<List<Amenity>> GetAmenitiesAsync(CancellationToken cancellationToken = default);
    Task<List<Amenity>> GetAmenitiesByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
