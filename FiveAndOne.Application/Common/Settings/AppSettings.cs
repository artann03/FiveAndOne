namespace FiveAndOne.Application.Common.Settings;

/// <summary>"App" configuration section.</summary>
public class AppSettings
{
    public const string SectionName = "App";

    /// <summary>Frontend origin, used to build links in emails (guest reservation, staff invitation).</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>Public base URL of this API, used for uploaded file URLs and mock checkout pages.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5166";

    /// <summary>Languages the platform supports. The first one is the default.</summary>
    public string[] SupportedLanguages { get; set; } = { "sq", "en", "tr", "sr" };

    /// <summary>Market time zone used when no facility is involved (dashboards, limits per day).</summary>
    public string DefaultTimeZone { get; set; } = "Europe/Belgrade";
}

/// <summary>"Facilities" configuration section.</summary>
public class FacilitySettings
{
    public const string SectionName = "Facilities";

    /// <summary>Facilities a business without an active subscription may create (as drafts).</summary>
    public int MaxFacilitiesWithoutSubscription { get; set; } = 1;

    public int MaxImagesPerFacility { get; set; } = 10;

    /// <summary>Max upload size per image in bytes (5 MB).</summary>
    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;
}

/// <summary>"Payments" configuration section (mock provider switches).</summary>
public class PaymentSettings
{
    public const string SectionName = "Payments";

    /// <summary>Shared secret the payment provider signs webhooks with.</summary>
    public string WebhookSecret { get; set; } = "dev-webhook-secret";

    /// <summary>Mock only: whether automatic renewals succeed.</summary>
    public bool MockRenewalSucceeds { get; set; } = true;

    /// <summary>Minutes a reservation waiting for online payment holds its slot (post-MVP payments).</summary>
    public int ReservationPaymentHoldMinutes { get; set; } = 15;
}
