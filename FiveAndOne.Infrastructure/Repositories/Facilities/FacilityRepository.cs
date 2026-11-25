using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Infrastructure.Repositories.Facilities;

public class FacilityRepository : IFacilityRepository
{
    private readonly ApplicationDbContext _context;

    public FacilityRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private IQueryable<Facility> WithDetails() => _context.Facilities
        .Include(x => x.Images)
        .Include(x => x.Amenities).ThenInclude(x => x.Amenity)
        .Include(x => x.Sports).ThenInclude(x => x.Sport)
        .AsSplitQuery();

    public async Task<Facility?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await WithDetails().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<Facility>> GetByOwnerAsync(Guid ownerUserId, FacilityStatus? status, CancellationToken cancellationToken = default)
    {
        var query = WithDetails().AsNoTracking().Where(x => x.OwnerUserId == ownerUserId);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<int> CountByOwnerAsync(Guid ownerUserId, FacilityStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _context.Facilities.Where(x => x.OwnerUserId == ownerUserId);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<(List<Facility> Items, int TotalCount)> SearchPublishedAsync(
        string? city,
        Guid? sportId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = WithDetails()
            .AsNoTracking()
            .Include(x => x.Owner).ThenInclude(x => x.BusinessProfile)
            // Facilities of a deactivated/suspended owner disappear from the public site (and return on reactivation).
            .Where(x => x.Status == FacilityStatus.Published && x.Owner.Status == UserStatus.Active);

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(x => x.City != null && EF.Functions.ILike(x.City, EscapeLike(city.Trim())));
        }

        if (sportId.HasValue)
        {
            query = query.Where(x => x.Sports.Any(s => s.SportId == sportId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, pattern) ||
                (x.Description != null && EF.Functions.ILike(x.Description, pattern)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Facility?> GetPublishedByIdOrSlugAsync(string idOrSlug, CancellationToken cancellationToken = default)
    {
        var query = WithDetails()
            .AsNoTracking()
            .Include(x => x.Owner).ThenInclude(x => x.BusinessProfile)
            // Facilities of a deactivated/suspended owner disappear from the public site (and return on reactivation).
            .Where(x => x.Status == FacilityStatus.Published && x.Owner.Status == UserStatus.Active);

        return Guid.TryParse(idOrSlug, out var id)
            ? await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            : await query.FirstOrDefaultAsync(x => x.Slug == idOrSlug.ToLowerInvariant(), cancellationToken);
    }

    public async Task<(List<Facility> Items, int TotalCount)> GetPagedAsync(
        FacilityStatus? status,
        Guid? ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = WithDetails().AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (ownerUserId.HasValue)
        {
            query = query.Where(x => x.OwnerUserId == ownerUserId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> HasBookingsAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings.AnyAsync(x => x.FacilityId == facilityId, cancellationToken);
    }

    public async Task AddAsync(Facility facility, CancellationToken cancellationToken = default)
    {
        await _context.Facilities.AddAsync(facility, cancellationToken);
    }

    public void Remove(Facility facility)
    {
        _context.Facilities.Remove(facility);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    // User input must not act as LIKE wildcards.
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
