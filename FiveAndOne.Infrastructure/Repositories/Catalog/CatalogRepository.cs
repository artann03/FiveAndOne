using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Infrastructure.Repositories.Catalog;

public class CatalogRepository : ICatalogRepository
{
    private readonly ApplicationDbContext _context;

    public CatalogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Sport>> GetSportsAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _context.Sports.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    // Tracked on purpose: the result is attached to facilities in the same unit of work.
    public async Task<List<Sport>> GetSportsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new List<Sport>();
        }

        return await _context.Sports.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public async Task<List<Amenity>> GetAmenitiesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Amenities.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Amenity>> GetAmenitiesByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new List<Amenity>();
        }

        return await _context.Amenities.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
    }
}
