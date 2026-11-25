using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Businesses;
using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Repositories.Businesses;
using FiveAndOne.Infrastructure.Repositories.Users;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Businesses;

public class BusinessProfileService : IBusinessProfileService
{
    private readonly IBusinessProfileRepository _profileRepository;
    private readonly IUserRepository _userRepository;

    public BusinessProfileService(IBusinessProfileRepository profileRepository, IUserRepository userRepository)
    {
        _profileRepository = profileRepository;
        _userRepository = userRepository;
    }

    public async Task<BusinessProfileResponse> GetMyProfileAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var owner = await GetBusinessOwnerAsync(currentUserId, cancellationToken);
        var profile = await _profileRepository.GetByOwnerUserIdAsync(owner.Id, cancellationToken);

        if (profile is not null)
        {
            return BusinessProfileResponse.FromEntity(profile);
        }

        // Nothing saved yet: return a pre-filled draft so the form isn't empty. Nothing is written.
        return new BusinessProfileResponse
        {
            Email = owner.Email,
            Phone = owner.PhoneNumber,
            City = owner.City,
            IsComplete = false
        };
    }

    public async Task<BusinessProfileResponse> UpdateMyProfileAsync(
        Guid currentUserId,
        UpdateBusinessProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var owner = await GetBusinessOwnerAsync(currentUserId, cancellationToken);
        var profile = await _profileRepository.GetByOwnerUserIdAsync(owner.Id, cancellationToken);

        if (profile is null)
        {
            profile = new BusinessProfile { OwnerUserId = owner.Id };
            await _profileRepository.AddAsync(profile, cancellationToken);
        }
        else
        {
            profile.UpdatedAt = DateTime.UtcNow;
        }

        // [Required] on the request already guarantees these are non-empty.
        profile.BusinessName = request.BusinessName!.Trim();
        profile.AddressLine = request.AddressLine!.Trim();
        profile.City = request.City!.Trim();
        profile.Country = request.Country!.Trim().ToUpperInvariant();
        profile.Phone = request.Phone!.Trim();
        profile.Email = request.Email!.Trim().ToLowerInvariant();
        profile.PreferredLanguage = request.PreferredLanguage!.Trim().ToLowerInvariant();

        await _profileRepository.SaveChangesAsync(cancellationToken);

        return BusinessProfileResponse.FromEntity(profile);
    }

    // The controller already requires the Business role; this re-checks against the database
    // so a stale token (e.g. after a role change) can't touch a profile.
    private async Task<User> GetBusinessOwnerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("User no longer exists.");
        }

        if (user.Role != UserRole.Business)
        {
            throw new ForbiddenException("Only business accounts have a business profile.");
        }

        return user;
    }
}
