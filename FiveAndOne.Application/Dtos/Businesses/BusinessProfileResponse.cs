using FiveAndOne.Domain.Entities.Businesses;
using System;

namespace FiveAndOne.Application.Dtos.Businesses;

public class BusinessProfileResponse
{
    public string? BusinessName { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = "XK";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string PreferredLanguage { get; set; } = "sq";

    /// <summary>False until the business saves its profile for the first time.</summary>
    public bool IsComplete { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public static BusinessProfileResponse FromEntity(BusinessProfile profile) => new()
    {
        BusinessName = profile.BusinessName,
        AddressLine = profile.AddressLine,
        City = profile.City,
        Country = profile.Country,
        Phone = profile.Phone,
        Email = profile.Email,
        PreferredLanguage = profile.PreferredLanguage,
        IsComplete = true,
        UpdatedAt = profile.UpdatedAt ?? profile.CreatedAt
    };
}
