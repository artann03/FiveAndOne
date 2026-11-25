using FiveAndOne.Application.Common.Exceptions;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace FiveAndOne.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The current user's id from the access token's "sub" claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(sub, out var userId)
            ? userId
            : throw new UnauthorizedException("Invalid access token.");
    }
}
