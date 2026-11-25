using FiveAndOne.Application.Dtos.Catalog;
using FiveAndOne.Application.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiveAndOne.API.Controllers;

/// <summary>Lookup lists used by forms and filters.</summary>
[ApiController]
[AllowAnonymous]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("api/sports")]
    [ProducesResponseType<List<SportResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SportResponse>>> GetSports(CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.GetSportsAsync(cancellationToken));
    }

    [HttpGet("api/amenities")]
    [ProducesResponseType<List<AmenityResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AmenityResponse>>> GetAmenities(CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.GetAmenitiesAsync(cancellationToken));
    }
}
