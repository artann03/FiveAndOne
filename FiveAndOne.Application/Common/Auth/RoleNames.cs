using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Application.Common.Auth;

/// <summary>
/// Role names as they appear in the JWT "role" claim, for use in [Authorize(Roles = ...)].
/// </summary>
public static class RoleNames
{
    public const string ClaimType = "role";

    public const string User = nameof(UserRole.User);
    public const string Business = nameof(UserRole.Business);
    public const string Admin = nameof(UserRole.Admin);
    public const string SuperAdmin = nameof(UserRole.SuperAdmin);

    public const string AdminOrAbove = Admin + "," + SuperAdmin;
}
