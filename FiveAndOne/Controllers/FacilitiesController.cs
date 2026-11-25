using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Application.Services.Facilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiveAndOne.API.Controllers;

/// <summary>Public facility browsing. Only Published facilities are ever returned.</summary>
[ApiController]
[Route("api/facilities")]
[AllowAnonymous]
public class FacilitiesController : ControllerBase
{
    private readonly IFacilityService _facilityService;

    public FacilitiesController(IFacilityService facilityService)
    {
        _facilityService = facilityService;
    }

    // GET /api/facilities?city=Prishtina&sportId=...&search=arena&page=1&pageSize=20
    [HttpGet]
    [ProducesResponseType<PagedResponse<PublicFacilityResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PublicFacilityResponse>>> Search(
        [FromQuery] string? city,
        [FromQuery] Guid? sportId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _facilityService.SearchPublishedAsync(city, sportId, search, page, pageSize, cancellationToken));
    }

    /// <summary>By id or slug, e.g. /api/facilities/futsal-pitch-1-3f9a1c</summary>
    [HttpGet("{idOrSlug}")]
    [ProducesResponseType<PublicFacilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicFacilityResponse>> Get(string idOrSlug, CancellationToken cancellationToken)
    {
        return Ok(await _facilityService.GetPublishedAsync(idOrSlug, cancellationToken));
    }
}
