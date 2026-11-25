namespace FiveAndOne.Application.Common.Settings;

/// <summary>
/// Facility rules from configuration ("Facilities" section).
/// Temporary stand-in for real subscription plans.
/// </summary>
public class FacilitySettings
{
    public const string SectionName = "Facilities";

    /// <summary>How many facilities one business account may have (counts all statuses).</summary>
    public int MaxFacilitiesPerBusiness { get; set; } = 3;

    /// <summary>Max images per facility.</summary>
    public int MaxImagesPerFacility { get; set; } = 10;
}
