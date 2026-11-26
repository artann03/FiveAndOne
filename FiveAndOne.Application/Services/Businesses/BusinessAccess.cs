using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FiveAndOne.Application.Services.Businesses;

/// <summary>The business the current request acts on, and what the caller may do there.</summary>
public sealed record BusinessContext(Business Business, Guid UserId, BusinessMemberRole Role, BusinessPermissions Permissions)
{
    public Guid BusinessId => Business.Id;
    public bool IsOwner => Role == BusinessMemberRole.Owner;
    public bool Has(BusinessPermissions permission) => (Permissions & permission) == permission;
}

public interface IBusinessAccess
{
    /// <summary>
    /// Resolves the business for this request and checks the caller's permission.
    /// <paramref name="businessId"/> comes from the optional X-Business-Id header; it's only needed
    /// when the user belongs to more than one business.
    /// </summary>
    Task<BusinessContext> GetAsync(Guid userId, Guid? businessId, BusinessPermissions required, CancellationToken cancellationToken = default);

    /// <summary>A facility of the current business, or 404 (other businesses' facilities look like they don't exist).</summary>
    Task<Facility> GetFacilityAsync(BusinessContext context, Guid facilityId, CancellationToken cancellationToken = default);
}

public class BusinessAccess : IBusinessAccess
{
    private readonly ApplicationDbContext _db;

    public BusinessAccess(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<BusinessContext> GetAsync(Guid userId, Guid? businessId, BusinessPermissions required, CancellationToken cancellationToken = default)
    {
        // Re-checked against the database on every call: a stale token can't keep access after
        // a membership was removed or the account was deactivated.
        var memberships = await _db.BusinessMembers
            .Include(x => x.Business)
            .Where(x => x.UserId == userId && x.IsActive && x.User.Status == UserStatus.Active && x.Business.AnonymizedAt == null)
            .ToListAsync(cancellationToken);

        BusinessMember? membership;
        if (businessId.HasValue)
        {
            membership = memberships.FirstOrDefault(x => x.BusinessId == businessId.Value)
                ?? throw new NotFoundException("Business not found.");
        }
        else if (memberships.Count == 1)
        {
            membership = memberships[0];
        }
        else if (memberships.Count == 0)
        {
            throw new ForbiddenException("This account doesn't manage a business yet. Register a business first.", "no_business");
        }
        else
        {
            throw new BadRequestException(
                "You manage more than one business. Send the X-Business-Id header to choose one (see GET /api/business/mine).",
                "business_selection_required");
        }

        var context = new BusinessContext(membership.Business, userId, membership.Role, membership.EffectivePermissions);

        if (required != BusinessPermissions.None && !context.Has(required))
        {
            throw new ForbiddenException("You don't have permission to do this for this business.", "missing_permission");
        }

        return context;
    }

    public async Task<Facility> GetFacilityAsync(BusinessContext context, Guid facilityId, CancellationToken cancellationToken = default)
    {
        return await _db.Facilities
            .FirstOrDefaultAsync(x => x.Id == facilityId && x.BusinessId == context.BusinessId, cancellationToken)
            ?? throw new NotFoundException("Facility not found.");
    }
}
