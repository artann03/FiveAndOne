using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Identity;

/// <summary>A user's level and preferences for one sport (replaces the old football-only fields on User).</summary>
public class UserSportProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid SportId { get; set; }
    public Sport Sport { get; set; } = default!;

    public SkillLevel SkillLevel { get; set; } = SkillLevel.Beginner;

    // Free text because positions differ per sport ("Goalkeeper", "Point guard", "Doubles - left side", ...).
    public string? PreferredPosition { get; set; }
}
