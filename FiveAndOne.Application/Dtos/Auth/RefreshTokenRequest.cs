using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Auth;

public class RefreshTokenRequest
{
    [Required]
    public required string RefreshToken { get; set; }
}
