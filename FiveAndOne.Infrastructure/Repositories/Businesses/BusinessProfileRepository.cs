using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Infrastructure.Repositories.Businesses;

public class BusinessProfileRepository : IBusinessProfileRepository
{
    private readonly ApplicationDbContext _context;

    public BusinessProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BusinessProfile?> GetByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _context.BusinessProfiles
            .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId, cancellationToken);
    }

    public async Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken = default)
    {
        await _context.BusinessProfiles.AddAsync(profile, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
