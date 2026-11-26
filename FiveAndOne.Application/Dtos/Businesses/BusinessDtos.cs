using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Businesses;

public class BusinessProfileResponse
{
    public Guid? Id { get; set; }
    public string? BusinessName { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = "XK";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string PreferredLanguage { get; set; } = "sq";
    public BusinessStatus? Status { get; set; }

    /// <summary>Whether an official registration number is on file. The number itself is never returned.</summary>
    public bool HasRegistrationNumber { get; set; }

    public int CancellationCutoffHours { get; set; } = 24;
    public string? CancellationPolicyNote { get; set; }

    /// <summary>False until the business profile exists.</summary>
    public bool IsComplete { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public static BusinessProfileResponse FromEntity(Business b) => new()
    {
        Id = b.Id,
        BusinessName = b.BusinessName,
        AddressLine = b.AddressLine,
        City = b.City,
        Country = b.Country,
        Phone = b.Phone,
        Email = b.Email,
        PreferredLanguage = b.PreferredLanguage,
        Status = b.Status,
        HasRegistrationNumber = !string.IsNullOrEmpty(b.RegistrationNumber),
        CancellationCutoffHours = b.CancellationCutoffHours,
        CancellationPolicyNote = b.CancellationPolicyNote,
        IsComplete = true,
        UpdatedAt = b.UpdatedAt ?? b.CreatedAt
    };
}

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
    [RegularExpression("^(?i)(sq|en|tr|sr)$", ErrorMessage = "Preferred language must be one of: sq, en, tr, sr.")]
    public string? PreferredLanguage { get; set; }
}

/// <summary>"Create business account" form (story 06): creates the owner account and the business in one step.</summary>
public class RegisterBusinessRequest : UpdateBusinessProfileRequest
{
    [Required(ErrorMessage = "Official business registration number is required.")]
    [StringLength(30, ErrorMessage = "Registration number can be at most 30 characters.")]
    public string? RegistrationNumber { get; set; }

    [Required(ErrorMessage = "Owner first name is required.")]
    [StringLength(100, ErrorMessage = "First name can be at most 100 characters.")]
    public string? OwnerFirstName { get; set; }

    [StringLength(100, ErrorMessage = "Last name can be at most 100 characters.")]
    public string? OwnerLastName { get; set; }

    /// <summary>Optional; generated from the email when empty.</summary>
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be 3-50 characters.")]
    [RegularExpression("^[a-zA-Z0-9_.]+$", ErrorMessage = "Username can only contain letters, numbers, '_' and '.'.")]
    public string? Username { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be 8-128 characters.")]
    public string? Password { get; set; }
}

/// <summary>Creating an additional business for an existing owner (story 58).</summary>
public class CreateBusinessRequest : UpdateBusinessProfileRequest
{
    [StringLength(30, ErrorMessage = "Registration number can be at most 30 characters.")]
    public string? RegistrationNumber { get; set; }
}

public class CancellationPolicyRequest
{
    [Range(0, 720, ErrorMessage = "Cancellation deadline must be between 0 and 720 hours before the start.")]
    public int CancellationCutoffHours { get; set; } = 24;

    [StringLength(1000, ErrorMessage = "Policy note can be at most 1000 characters.")]
    public string? CancellationPolicyNote { get; set; }
}

/// <summary>A business the current user can act on (for the business switcher).</summary>
public class MyBusinessResponse
{
    public Guid Id { get; set; }
    public string BusinessName { get; set; } = default!;
    public BusinessStatus Status { get; set; }
    public BusinessMemberRole Role { get; set; }
    public BusinessPermissions Permissions { get; set; }
    public string City { get; set; } = default!;
}
