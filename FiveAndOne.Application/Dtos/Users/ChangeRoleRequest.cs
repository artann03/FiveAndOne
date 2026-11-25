using FiveAndOne.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Users;

public class ChangeRoleRequest
{
    [Required]
    public required UserRole Role { get; set; }
}
