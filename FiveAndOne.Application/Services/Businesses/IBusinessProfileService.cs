using FiveAndOne.Application.Dtos.Businesses;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Businesses;

public interface IBusinessProfileService
{
    Task<BusinessProfileResponse> GetMyProfileAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task<BusinessProfileResponse> UpdateMyProfileAsync(Guid currentUserId, UpdateBusinessProfileRequest request, CancellationToken cancellationToken = default);
}
