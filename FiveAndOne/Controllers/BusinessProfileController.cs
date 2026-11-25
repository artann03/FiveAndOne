using FiveAndOne.Application.Common.Auth;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Businesses;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Services.Businesses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FiveAndOne.API.Controllers;

/// <summary>
/// The logged-in business account's own profile. There is no {id} in the route:
/// a business can only ever read or edit its own profile.
/// </summary>
[ApiController]
[Route("api/business/profile")]
[Authorize(Roles = RoleNames.Business)]
public class BusinessProfileController : ControllerBase
{
    private readonly IBusinessProfileService _businessProfileService;

    public BusinessProfileController(IBusinessProfileService businessProfileService)
    {
        _businessProfileService = businessProfileService;
    }

    [HttpGet]
    [ProducesResponseType<BusinessProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BusinessProfileResponse>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _businessProfileService.GetMyProfileAsync(GetCurrentUserId(), cancellationToken));
    }

    [HttpPut]
    [ProducesResponseType<BusinessProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BusinessProfileResponse>> Update(
        UpdateBusinessProfileRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _businessProfileService.UpdateMyProfileAsync(GetCurrentUserId(), request, cancellationToken));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(sub, out var userId)
            ? userId
            : throw new UnauthorizedException("Invalid access token.");
    }
}
