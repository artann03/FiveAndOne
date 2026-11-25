using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Businesses;

/// <summary>All fields are required; each invalid field is reported separately in the 400 response.</summary>
public class UpdateBusinessProfileRequest
{
    [Required(ErrorMessage = "Business name is required.")]
    [StringLength(150, ErrorMessage = "Business name can be at most 150 characters.")]
    public string? BusinessName { get; set; }

    [Required(ErrorMessage = "Address is required.")]
    [StringLength(250, ErrorMessage = "Address can be at most 250 characters.")]
    public string? AddressLine { get; set; }

    [Required(ErrorMessage = "City is required.")]
    [StringLength(100, ErrorMessage = "City can be at most 100 characters.")]
    public string? City { get; set; }

    [Required(ErrorMessage = "Country is required.")]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Country must be a 2-letter ISO code, e.g. XK for Kosovo.")]
    public string? Country { get; set; }

    [Required(ErrorMessage = "Phone is required.")]
    [RegularExpression(@"^\+?[0-9][0-9 ()\-]{5,28}$", ErrorMessage = "Phone must be a valid phone number, e.g. +383 44 123 456.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256, ErrorMessage = "Email can be at most 256 characters.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Preferred language is required.")]
    [RegularExpression("^(?i)(sq|en|sr)$", ErrorMessage = "Preferred language must be one of: sq, en, sr.")]
    public string? PreferredLanguage { get; set; }
}
