using FiveAndOne.Domain.Enums;
using System;

namespace FiveAndOne.Application.Dtos.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = default!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = default!;
    public DateTime RefreshTokenExpiresAt { get; set; }

    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string Username { get; set; } = default!;
    public UserRole Role { get; set; }
    public string FirstName { get; set; } = default!;
    public string? LastName { get; set; }
}
