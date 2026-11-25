using FiveAndOne.Domain.Enums;

namespace FiveAndOne.Domain.Entities.Sports;

public class Sport : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public SportCategory Category { get; set; }
    public string? IconUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Sensible defaults when creating an activity for this sport (e.g. futsal 10, tennis 2).
    public int DefaultMinParticipants { get; set; } = 2;
    public int DefaultMaxParticipants { get; set; } = 2;

    // Sport-specific settings stored as JSON (jsonb) so new sports need no schema change.
    public string? MetadataJson { get; set; }
}
