using FiveAndOne.Domain.Entities.Facilities;
using System.Collections.Generic;

namespace FiveAndOne.Application.Services.Facilities;

/// <summary>
/// What a facility must have before it can be published (and must keep while published).
/// Keys match the request field names so the frontend can highlight the right inputs.
/// </summary>
public static class FacilityPublishRules
{
    public static Dictionary<string, string[]> GetMissingFields(Facility facility)
    {
        var missing = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(facility.Name))
            missing["name"] = new[] { "Facility name is required." };

        if (facility.Type is null)
            missing["type"] = new[] { "Facility type is required." };

        if (facility.Sports.Count == 0)
            missing["sportIds"] = new[] { "Select at least one sport." };

        if (string.IsNullOrWhiteSpace(facility.AddressLine))
            missing["addressLine"] = new[] { "Address is required." };

        if (string.IsNullOrWhiteSpace(facility.City))
            missing["city"] = new[] { "City is required." };

        if (facility.Latitude is null)
            missing["latitude"] = new[] { "Map location (latitude) is required." };

        if (facility.Longitude is null)
            missing["longitude"] = new[] { "Map location (longitude) is required." };

        if (facility.Images.Count == 0)
            missing["imageUrls"] = new[] { "Add at least one image." };

        return missing;
    }
}
