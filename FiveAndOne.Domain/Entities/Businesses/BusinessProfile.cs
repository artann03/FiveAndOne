using FiveAndOne.Domain.Entities.Identity;

namespace FiveAndOne.Domain.Entities.Businesses;

/// <summary>
/// Public-facing details of a business account (one per Business user).
/// Deliberately holds no official registration identifiers (business/fiscal numbers).
/// </summary>
public class BusinessProfile : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public User Owner { get; set; } = default!;

    public string BusinessName { get; set; } = default!;
    public string AddressLine { get; set; } = default!;
    public string City { get; set; } = default!;

    // ISO 3166-1 alpha-2, upper case (Kosovo = "XK").
    public string Country { get; set; } = "XK";

    public string Phone { get; set; } = default!;
    public string Email { get; set; } = default!;

    // Language code used for communication with the business: "sq", "en" or "sr".
    public string PreferredLanguage { get; set; } = "sq";
}
