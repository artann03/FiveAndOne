using FiveAndOne.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Auth;

public class RegisterRequest
{
    [Required, StringLength(100)]
    public required string FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression("^[a-zA-Z0-9_.]+$", ErrorMessage = "Username can only contain letters, numbers, '_' and '.'.")]
    public required string Username { get; set; }

    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; set; }

    [Required, StringLength(128, MinimumLength = 8)]
    public required string Password { get; set; }

    // Only "User" (default) or "Business" can be chosen at sign-up.
    // Admin / SuperAdmin are assigned by a SuperAdmin via /api/admin/users/{id}/role.
    public UserRole Role { get; set; } = UserRole.User;
}
