using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Auth;

public class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; set; }

    [Required, StringLength(128)]
    public required string Password { get; set; }
}
