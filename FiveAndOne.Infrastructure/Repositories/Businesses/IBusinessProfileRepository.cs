using FiveAndOne.Domain.Entities.Businesses;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Infrastructure.Repositories.Businesses;

public interface IBusinessProfileRepository
{
    Task<BusinessProfile?> GetByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
