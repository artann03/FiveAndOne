using FiveAndOne.Application.Common.Auth;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Application.Services.Facilities;
using FiveAndOne.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiveAndOne.API.Controllers;

/// <summary>Platform moderation of facilities.</summary>
[ApiController]
[Route("api/admin/facilities")]
[Authorize(Roles = RoleNames.AdminOrAbove)]
public class AdminFacilitiesController : ControllerBase
{
    private readonly IFacilityService _facilityService;

    public AdminFacilitiesController(IFacilityService facilityService)
    {
        _facilityService = facilityService;
    }

    // GET /api/admin/facilities?status=Published&ownerUserId=...&page=1&pageSize=20
    [HttpGet]
    [ProducesResponseType<PagedResponse<FacilitySummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<FacilitySummaryResponse>>> GetAll(
        [FromQuery] FacilityStatus? status,
        [FromQuery] Guid? ownerUserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _facilityService.GetAllAsync(status, ownerUserId, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<FacilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FacilityResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _facilityService.GetAnyAsync(id, cancellationToken));
    }

    /// <summary>Takes a facility off the platform; the owner can't publish it until it's unsuspended.</summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType<FacilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FacilityResponse>> Suspend(Guid id, SuspendFacilityRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _facilityService.SuspendAsync(id, request.Reason!, cancellationToken));
    }

    /// <summary>Lifts a suspension. The facility comes back Hidden; the owner re-publishes it.</summary>
    [HttpPost("{id:guid}/unsuspend")]
    [ProducesResponseType<FacilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FacilityResponse>> Unsuspend(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _facilityService.UnsuspendAsync(id, cancellationToken));
    }
}
