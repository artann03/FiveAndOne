using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Entities.Sports;
using FiveAndOne.Domain.Enums;
using System;

namespace FiveAndOne.Application.Dtos.Catalog;

public class SportResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public SportCategory Category { get; set; }
    public string? IconUrl { get; set; }
    public int DefaultMinParticipants { get; set; }
    public int DefaultMaxParticipants { get; set; }

    public static SportResponse FromEntity(Sport s) => new()
    {
        Id = s.Id,
        Name = s.Name,
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

    public static AmenityResponse FromEntity(Amenity a) => new() { Id = a.Id, Name = a.Name, Icon = a.Icon };
}
