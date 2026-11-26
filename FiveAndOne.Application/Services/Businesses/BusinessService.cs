using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Businesses;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace FiveAndOne.Application.Services.Businesses;

public interface IBusinessService
{
    /// <summary>Validates and adds a business + owner membership for <paramref name="owner"/> (caller saves).</summary>
    Task<Business> AddBusinessAsync(User owner, UpdateBusinessProfileRequest details, string? registrationNumber, CancellationToken cancellationToken = default);

    Task<BusinessProfileResponse> GetProfileAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default);
    Task<BusinessProfileResponse> UpdateProfileAsync(Guid userId, Guid? businessId, UpdateBusinessProfileRequest request, CancellationToken cancellationToken = default);
    Task<BusinessProfileResponse> UpdateCancellationPolicyAsync(Guid userId, Guid? businessId, CancellationPolicyRequest request, CancellationToken cancellationToken = default);
    Task<List<MyBusinessResponse>> GetMyBusinessesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BusinessProfileResponse> CreateAdditionalBusinessAsync(Guid userId, CreateBusinessRequest request, CancellationToken cancellationToken = default);
}

public class BusinessService : IBusinessService
{
    // Kosovo ARBK business numbers: 8 digits (older "numri i biznesit") or 9 digits (NUI, unique identification number).
    private static readonly Regex KosovoRegistrationNumber = new(@"^\d{8,9}$", RegexOptions.Compiled);
    private static readonly Regex GenericRegistrationNumber = new(@"^[A-Za-z0-9\-/]{4,30}$", RegexOptions.Compiled);

    private readonly ApplicationDbContext _db;
    private readonly IBusinessAccess _access;
    private readonly IFeatureFlags _flags;
    private readonly IAuditService _audit;

    public BusinessService(ApplicationDbContext db, IBusinessAccess access, IFeatureFlags flags, IAuditService audit)
    {
        _db = db;
        _access = access;
        _flags = flags;
        _audit = audit;
    }

    public async Task<Business> AddBusinessAsync(User owner, UpdateBusinessProfileRequest details, string? registrationNumber, CancellationToken cancellationToken = default)
    {
        var country = details.Country!.Trim().ToUpperInvariant();
        var number = NormalizeRegistrationNumber(registrationNumber);

        if (number is not null)
        {
            var valid = country == "XK" ? KosovoRegistrationNumber.IsMatch(number) : GenericRegistrationNumber.IsMatch(number);
            if (!valid)
            {
                throw RequestValidationException.For("registrationNumber", country == "XK"
                    ? "Kosovo business registration numbers have 8 or 9 digits."
                    : "Registration number must be 4-30 letters, digits, '-' or '/'.");
            }

            if (await _db.Businesses.AnyAsync(x => x.RegistrationNumber == number, cancellationToken))
            {
                throw new ConflictException("A business with this registration number is already registered.", "business_already_registered");
            }
        }

        var business = new Business
        {
            OwnerUserId = owner.Id,
            Owner = owner,
            RegistrationNumber = number,
            Status = BusinessStatus.Active   // registration activates the business immediately
        };
        Apply(business, details);

        business.Members.Add(new BusinessMember
        {
            BusinessId = business.Id,
            UserId = owner.Id,
            Role = BusinessMemberRole.Owner,
            Permissions = BusinessPermissions.All
        });

        _db.Businesses.Add(business);
        _audit.Log(owner.Id, "business.registered", "Business", business.Id.ToString(), business.BusinessName, businessId: business.Id);
        return business;
    }

    public async Task<BusinessProfileResponse> GetProfileAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default)
    {
        var membershipCount = await _db.BusinessMembers.CountAsync(x => x.UserId == userId && x.IsActive, cancellationToken);
        if (membershipCount == 0 && businessId is null)
        {
            // Business account without a business yet (registered before one-step signup): pre-filled draft.
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new UnauthorizedException("User no longer exists.");
            EnsureBusinessAccount(user);

            return new BusinessProfileResponse { Email = user.Email, Phone = user.PhoneNumber, City = user.City, IsComplete = false };
        }

        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);
        return BusinessProfileResponse.FromEntity(context.Business);
    }

    public async Task<BusinessProfileResponse> UpdateProfileAsync(Guid userId, Guid? businessId, UpdateBusinessProfileRequest request, CancellationToken cancellationToken = default)
    {
        var hasBusiness = await _db.BusinessMembers.AnyAsync(x => x.UserId == userId && x.IsActive, cancellationToken);
        if (!hasBusiness && businessId is null)
        {
            // First save creates the business (legacy flow for Business accounts registered without one).
            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new UnauthorizedException("User no longer exists.");
            EnsureBusinessAccount(user);

            var created = await AddBusinessAsync(user, request, null, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return BusinessProfileResponse.FromEntity(created);
        }

        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageProfile, cancellationToken);
        var business = context.Business;
        Apply(business, request);
        business.UpdatedAt = DateTime.UtcNow;

        _audit.Log(userId, "business.profile_updated", "Business", business.Id.ToString(), null, businessId: business.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return BusinessProfileResponse.FromEntity(business);
    }

    public async Task<BusinessProfileResponse> UpdateCancellationPolicyAsync(Guid userId, Guid? businessId, CancellationPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageProfile, cancellationToken);
        var business = context.Business;

        business.CancellationCutoffHours = request.CancellationCutoffHours;
        business.CancellationPolicyNote = Text.NullIfEmpty(request.CancellationPolicyNote);
        business.UpdatedAt = DateTime.UtcNow;

        _audit.Log(userId, "business.cancellation_policy_updated", "Business", business.Id.ToString(),
            $"Cutoff {request.CancellationCutoffHours}h", businessId: business.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return BusinessProfileResponse.FromEntity(business);
    }

    public async Task<List<MyBusinessResponse>> GetMyBusinessesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var memberships = await _db.BusinessMembers.AsNoTracking()
            .Include(x => x.Business)
            .Where(x => x.UserId == userId && x.IsActive && x.Business.AnonymizedAt == null)
            .OrderBy(x => x.Business.BusinessName)
            .ToListAsync(cancellationToken);

        return memberships.Select(m => new MyBusinessResponse
        {
            Id = m.BusinessId,
            BusinessName = m.Business.BusinessName,
            Status = m.Business.Status,
            Role = m.Role,
            Permissions = m.EffectivePermissions,
            City = m.Business.City
        }).ToList();
    }

    public async Task<BusinessProfileResponse> CreateAdditionalBusinessAsync(Guid userId, CreateBusinessRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("User no longer exists.");
        EnsureBusinessAccount(user);

        var ownsOne = await _db.Businesses.AnyAsync(x => x.OwnerUserId == userId, cancellationToken);
        if (ownsOne)
        {
            // A second business is a post-MVP capability that needs product approval (story 58).
            await _flags.EnsureEnabledAsync(FeatureFlagKeys.MultipleBusinesses, cancellationToken);
        }

        var business = await AddBusinessAsync(user, request, request.RegistrationNumber, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return BusinessProfileResponse.FromEntity(business);
    }

    private static void Apply(Business business, UpdateBusinessProfileRequest request)
    {
        business.BusinessName = request.BusinessName!.Trim();
        business.AddressLine = request.AddressLine!.Trim();
        business.City = request.City!.Trim();
        business.Country = request.Country!.Trim().ToUpperInvariant();
        business.Phone = request.Phone!.Trim();
        business.Email = Text.NormalizeEmail(request.Email!);
        business.PreferredLanguage = request.PreferredLanguage!.Trim().ToLowerInvariant();
    }

    private static void EnsureBusinessAccount(User user)
    {
        if (!user.IsActive)
        {
            throw new UnauthorizedException("User no longer exists.");
        }

        if (user.Role != UserRole.Business)
        {
            throw new ForbiddenException("Only business accounts can own a business.", "not_business_account");
        }
    }

    private static string? NormalizeRegistrationNumber(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Regex.Replace(value.Trim(), @"\s+", string.Empty).ToUpperInvariant();
}
