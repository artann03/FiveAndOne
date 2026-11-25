using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Enums;
using System;

namespace FiveAndOne.Application.Dtos.Users;

public class UserResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string? LastName { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; }
    public DateTime? EmailConfirmedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? City { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; }

    public static UserResponse FromUser(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Username = user.Username,
        Email = user.Email,
        Role = user.Role,
        Status = user.Status,
        EmailConfirmedAt = user.EmailConfirmedAt,
        DeactivatedAt = user.DeactivatedAt,
        PhoneNumber = user.PhoneNumber,
        ProfileImageUrl = user.ProfileImageUrl,
        City = user.City,
        Bio = user.Bio,
        CreatedAt = user.CreatedAt
    };
}
