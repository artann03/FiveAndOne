using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Auth;

public class ChangePasswordRequest
{
    [Required, StringLength(128)]
    public required string CurrentPassword { get; set; }

    // Same rules as at registration.
    [Required, StringLength(128, MinimumLength = 8)]
    public required string NewPassword { get; set; }
}

public class DeactivateAccountRequest
{
    // Asked again so a stolen/forgotten-open session can't delete the account.
    [Required, StringLength(128)]
    public required string Password { get; set; }
}
