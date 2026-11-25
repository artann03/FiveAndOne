using FiveAndOne.API.Extensions;
using FiveAndOne.Application.Common.Auth;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Application.Services.Facilities;
using FiveAndOne.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiveAndOne.API.Controllers;

/// <summary>
/// A business owner's own facilities. Every action is scoped to the logged-in owner;
/// someone else's facility id returns 404.
/// </summary>
[ApiController]
[Route("api/business/facilities")]
[Authorize(Roles = RoleNames.Business)]
public class BusinessFacilitiesController : ControllerBase
{
    private readonly IFacilityService _facilityService;

    public BusinessFacilitiesController(IFacilityService facilityService)
    {
        _facilityService = facilityService;
    }

    /// <summary>Facility count vs. plan limit, so the UI can explain/disable "Add facility".</summary>
    [HttpGet("usage")]
    [ProducesResponseType<FacilityUsageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FacilityUsageResponse>> GetUsage(CancellationToken cancellationToken)
    {
        return Ok(await _facilityService.GetUsageAsync(User.GetUserId(), cancellationToken));
    }

    [HttpGet]
    [ProducesResponseType<List<FacilitySummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FacilitySummaryResponse>>> GetAll(
        [FromQuery] FacilityStatus? status,
        CancellationToken cancellationToken)
    {
        return Ok(await _facilityService.GetMyFacilitiesAsync(User.GetUserId(), status, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<FacilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FacilityResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _facilityService.GetMyFacilityAsync(User.GetUserId(), id, cancellationToken));
    }

    /// <summary>"Add facility" / "Save draft": creates a new facility in Draft.</summary>
    [HttpPost]
    [ProducesResponseType<FacilityResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FacilityResponse>> Create(SaveFacilityRequest request, CancellationToken cancellationToken)
    {
        var facility = await _facilityService.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = facility.Id }, facility);
    }

    /// <summary>Edit / "Save draft" on an existing facility. Status is unchanged.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<FacilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FacilityResponse>> Update(Guid id, SaveFacilityRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _facilityService.UpdateAsync(User.GetUserId(), id, request, cancellationToken));
    }

    /// <summary>"Publish facility": makes it public if required info is complete and the plan allows it.</summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType<FacilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FacilityResponse>> Publish(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _facilityService.PublishAsync(User.GetUserId(), id, cancellationToken));
    }

    /// <summary>"Hide facility": takes a published facility off the public listing.</summary>
    [HttpPost("{id:guid}/hide")]
    [ProducesResponseType<FacilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FacilityResponse>> Hide(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _facilityService.HideAsync(User.GetUserId(), id, cancellationToken));
    }

    /// <summary>Deletes a facility that has never been booked. Booked facilities can only be hidden.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _facilityService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
