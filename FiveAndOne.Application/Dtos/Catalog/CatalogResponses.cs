using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Catalog;

public class SportResponse
{
    public Guid Id { get; set; }

    /// <summary>Name in the request language (Accept-Language: sq, en, tr, sr).</summary>
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public SportCategory Category { get; set; }
    public string? IconUrl { get; set; }
    public int DefaultMinParticipants { get; set; }
    public int DefaultMaxParticipants { get; set; }

    /// <summary>Published facilities offering this sport (sport browsing, story 22).</summary>
    public int? FacilityCount { get; set; }

    public static SportResponse FromEntity(Sport s, IReadOnlyDictionary<Guid, string>? names = null) => new()
    {
        Id = s.Id,
        Name = names is not null && names.TryGetValue(s.Id, out var n) ? n : s.Name,
        Slug = s.Slug,
        Category = s.Category,
        IconUrl = s.IconUrl,
        DefaultMinParticipants = s.DefaultMinParticipants,
        DefaultMaxParticipants = s.DefaultMaxParticipants
    };
}

public class AmenityResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Icon { get; set; }

    public static AmenityResponse FromEntity(Amenity a, IReadOnlyDictionary<Guid, string>? names = null) => new()
    {
        Id = a.Id,
        Name = names is not null && names.TryGetValue(a.Id, out var n) ? n : a.Name,
        Icon = a.Icon
    };
}

/// <summary>Admin view of a sport with every translation.</summary>
public class AdminSportResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public SportCategory Category { get; set; }
    public string? IconUrl { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int DefaultMinParticipants { get; set; }
    public int DefaultMaxParticipants { get; set; }
    public Dictionary<string, string> NameTranslations { get; set; } = new();
}

public class SaveSportRequest
{
    /// <summary>Base (English) name.</summary>
    [Required, StringLength(100)]
    public string? Name { get; set; }

    [StringLength(100), RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Slug can only contain lowercase letters, digits and '-'.")]
    public string? Slug { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public SportCategory Category { get; set; } = SportCategory.Other;

    [StringLength(500)]
    public string? IconUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    [Range(1, 200)]
    public int DefaultMinParticipants { get; set; } = 2;

    [Range(1, 200)]
    public int DefaultMaxParticipants { get; set; } = 2;

    /// <summary>Names in other languages: { "sq": "Futboll", "tr": "Futbol", "sr": "Fudbal" }.</summary>
    public Dictionary<string, string> NameTranslations { get; set; } = new();
}

public class AdminAmenityResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Icon { get; set; }
    public int FacilityCount { get; set; }
    public Dictionary<string, string> NameTranslations { get; set; } = new();
}

public class SaveAmenityRequest
{
    [Required, StringLength(100)]
    public string? Name { get; set; }

    [StringLength(100)]
    public string? Icon { get; set; }

    public Dictionary<string, string> NameTranslations { get; set; } = new();
}
