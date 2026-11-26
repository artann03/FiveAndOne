using FiveAndOne.Application.Common.Exceptions;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace FiveAndOne.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public const string BusinessHeader = "X-Business-Id";

    /// <summary>The current user's id from the access token's "sub" claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(sub, out var userId)
            ? userId
            : throw new UnauthorizedException("Invalid access token.");
    }

    /// <summary>The current user's id, or null for anonymous requests (guest booking, public pages).</summary>
    public static Guid? TryGetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

    /// <summary>
    /// The business to act on, from the optional X-Business-Id header. Only needed when the user
    /// belongs to more than one business; membership is always checked by the service.
    /// </summary>
    public static Guid? GetBusinessId(this HttpRequest request)
    {
        var value = request.Headers[BusinessHeader].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Guid.TryParse(value, out var id)
            ? id
            : throw new BadRequestException("X-Business-Id must be a business id (GUID).", "invalid_business_id");
    }
}
