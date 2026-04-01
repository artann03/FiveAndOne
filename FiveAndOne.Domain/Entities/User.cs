using FiveAndOne.Domain.Entities;
using FiveAndOne.Domain.Enums;

public class User : BaseEntity
{
    public string FirstName { get; set; } = default!;
    public string? LastName { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = default!;
    public string? ProfileImageUrl { get; set; }
    public string? City { get; set; }

    public PreferredPosition? PreferredPosition { get; set; }
    public SkillLevel? SkillLevel { get; set; }

    public string? Bio { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<GamePost> CreatedGamePosts { get; set; } = new List<GamePost>();
    public ICollection<GameParticipant> GameParticipants { get; set; } = new List<GameParticipant>();
}