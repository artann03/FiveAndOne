using FiveAndOne.Application.Common.Auth;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Users;
using FiveAndOne.Application.Services.Users;
using FiveAndOne.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FiveAndOne.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = RoleNames.AdminOrAbove)]
public class AdminUsersController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminUsersController(IUserService userService)
    {
        _userService = userService;
    }

    // GET /api/admin/users?page=1&pageSize=20&role=Business
    [HttpGet]
    [ProducesResponseType<PagedResponse<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] UserRole? role = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _userService.GetUsersAsync(page, pageSize, role, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        return Ok(UserResponse.FromUser(user));
    }

    // Only a SuperAdmin can hand out roles (including Admin and SuperAdmin).
    [HttpPut("{id:guid}/role")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> ChangeRole(
        Guid id,
        ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        var actingUserId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

        return Ok(await _userService.ChangeRoleAsync(actingUserId, id, request.Role, cancellationToken));
    }
}
