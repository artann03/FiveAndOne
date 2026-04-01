using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities;

public class GameParticipant : BaseEntity
{
    public Guid GamePostId { get; set; }
    public GamePost GamePost { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public ParticipantStatus Status { get; set; } = ParticipantStatus.Joined;
    public TeamSide? TeamSide { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }

    public bool IsCreator { get; set; } = false;
}
