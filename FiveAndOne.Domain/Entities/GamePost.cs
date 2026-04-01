using FiveAndOne.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiveAndOne.Domain.Entities;

public class GamePost : BaseEntity
{
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    public DateTime GameDate { get; set; }
    public DateTime? EndDate { get; set; }

    public int PlayersPerTeam { get; set; }
    public int TotalSlots { get; set; }

    public decimal? PricePerPlayer { get; set; }
    public string? Currency { get; set; }

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? LocationName { get; set; }
    public string? Address { get; set; }

    public GamePostStatus Status { get; set; } = GamePostStatus.Open;
    public GamePostVisibility Visibility { get; set; } = GamePostVisibility.Public;

    public int JoinedPlayersCount { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<GameParticipant> Participants { get; set; } = new List<GameParticipant>();
}
