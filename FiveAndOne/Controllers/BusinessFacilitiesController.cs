using FiveAndOne.API.Extensions;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Availability;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Application.Services.Availability;
using FiveAndOne.Application.Services.Facilities;
using FiveAndOne.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiveAndOne.API.Controllers;

/// <summary>
/// The business's facilities and their configuration (stories 08-19). Every action is scoped to the
/// caller's business (owner or staff with permission); another business's facility id returns 404.
/// Send X-Business-Id only if the user manages more than one business.
/// </summary>
[ApiController]
[Route("api/business/facilities")]
[Authorize]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class BusinessFacilitiesController : ControllerBase
{
    private readonly IFacilityService _facilities;
    private readonly IFacilityConfigService _config;
    private readonly IFacilityImageService _images;
    private readonly IAvailabilityService _availability;

    public BusinessFacilitiesController(IFacilityService facilities, IFacilityConfigService config, IFacilityImageService images, IAvailabilityService availability)
    {
        _facilities = facilities;
        _config = config;
        _images = images;
        _availability = availability;
    }

    private Guid UserId => User.GetUserId();
    private Guid? BusinessId => Request.GetBusinessId();

    // ---------------- Facilities (stories 08, 12) ----------------

    /// <summary>Facility count vs. plan limit, so the UI can explain/disable "Add facility" and "Publish".</summary>
    [HttpGet("usage")]
    public async Task<ActionResult<FacilityUsageResponse>> GetUsage(CancellationToken ct) =>
        Ok(await _facilities.GetUsageAsync(UserId, BusinessId, ct));

    [HttpGet]
    public async Task<ActionResult<List<FacilitySummaryResponse>>> GetAll([FromQuery] FacilityStatus? status, CancellationToken ct) =>
        Ok(await _facilities.GetMyFacilitiesAsync(UserId, BusinessId, status, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FacilityResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await _facilities.GetMyFacilityAsync(UserId, BusinessId, id, ct));

    /// <summary>"Add facility" / "Save draft": creates a new facility in Draft.</summary>
    [HttpPost]
    [ProducesResponseType<FacilityResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FacilityResponse>> Create(SaveFacilityRequest request, CancellationToken ct)
    {
        var facility = await _facilities.CreateAsync(UserId, BusinessId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = facility.Id }, facility);
    }

    /// <summary>Edit / "Save draft". Status is unchanged. imageUrls = null keeps the current images.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FacilityResponse>> Update(Guid id, SaveFacilityRequest request, CancellationToken ct) =>
        Ok(await _facilities.UpdateAsync(UserId, BusinessId, id, request, ct));

    /// <summary>"Publish facility": requires complete info (incl. hours and prices) and an active subscription.</summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<FacilityResponse>> Publish(Guid id, CancellationToken ct) =>
        Ok(await _facilities.PublishAsync(UserId, BusinessId, id, ct));

    /// <summary>"Hide facility": stops new discovery and new reservations. Existing reservations stay.</summary>
    [HttpPost("{id:guid}/hide")]
    public async Task<ActionResult<FacilityResponse>> Hide(Guid id, CancellationToken ct) =>
        Ok(await _facilities.HideAsync(UserId, BusinessId, id, ct));

    /// <summary>Deletes a facility that has never been booked. Booked facilities can only be hidden.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _facilities.DeleteAsync(UserId, BusinessId, id, ct);
        return NoContent();
    }

    // ---------------- Configuration (stories 09, 13-16) ----------------

    /// <summary>Everything on the configuration screens in one call: rules, sports, hours, prices, upcoming exceptions.</summary>
    [HttpGet("{id:guid}/configuration")]
    public async Task<ActionResult<FacilityConfigurationResponse>> GetConfiguration(Guid id, CancellationToken ct) =>
        Ok(await _config.GetConfigurationAsync(UserId, BusinessId, id, ct));

    /// <summary>"Save sports": sports offered and their own durations/buffer (story 09).</summary>
    [HttpPut("{id:guid}/sports")]
    public async Task<ActionResult<FacilityResponse>> SaveSports(Guid id, SaveFacilitySportsRequest request, CancellationToken ct) =>
        Ok(await _config.SaveSportsAsync(UserId, BusinessId, id, request, ct));

    /// <summary>Durations, buffer, slot interval, booking window and cancellation deadline (stories 16, 33).</summary>
    [HttpPut("{id:guid}/rules")]
    public async Task<ActionResult<FacilityResponse>> SaveRules(Guid id, FacilityRulesRequest request, CancellationToken ct) =>
        Ok(await _config.SaveRulesAsync(UserId, BusinessId, id, request, ct));

    /// <summary>Weekly hours (story 14). sportId = hours that apply only to that sport.</summary>
    [HttpGet("{id:guid}/hours")]
    public async Task<ActionResult<WeeklyHoursResponse>> GetHours(Guid id, [FromQuery] Guid? sportId, CancellationToken ct) =>
        Ok(await _config.GetHoursAsync(UserId, BusinessId, id, sportId, ct));

    /// <summary>"Save hours": replaces the weekly schedule (facility or one sport).</summary>
    [HttpPut("{id:guid}/hours")]
    public async Task<ActionResult<WeeklyHoursResponse>> SaveHours(Guid id, WeeklyHoursRequest request, CancellationToken ct) =>
        Ok(await _config.SaveHoursAsync(UserId, BusinessId, id, request, ct));

    /// <summary>Removes a sport's own hours, so it follows the facility hours again.</summary>
    [HttpDelete("{id:guid}/hours/{sportId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSportHours(Guid id, Guid sportId, CancellationToken ct)
    {
        await _config.DeleteSportHoursAsync(UserId, BusinessId, id, sportId, ct);
        return NoContent();
    }

    /// <summary>Exceptional hours and temporary closures (story 15).</summary>
    [HttpGet("{id:guid}/exceptions")]
    public async Task<ActionResult<List<ScheduleExceptionResponse>>> GetExceptions(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _config.GetExceptionsAsync(UserId, BusinessId, id, from, to, ct));

    /// <summary>
    /// "Add exception" / "Close facility". If reservations conflict, returns 409 "reservation_conflicts" with
    /// details.conflicts; resend with conflictResolution = CancelReservations or KeepReservations.
    /// </summary>
    [HttpPost("{id:guid}/exceptions")]
    [ProducesResponseType<ScheduleExceptionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScheduleExceptionResponse>> AddException(Guid id, ScheduleExceptionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _config.AddExceptionAsync(UserId, BusinessId, id, request, ct));

    [HttpDelete("{id:guid}/exceptions/{exceptionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteException(Guid id, Guid exceptionId, CancellationToken ct)
    {
        await _config.DeleteExceptionAsync(UserId, BusinessId, id, exceptionId, ct);
        return NoContent();
    }

    /// <summary>Price rules (story 13).</summary>
    [HttpGet("{id:guid}/pricing")]
    public async Task<ActionResult<List<PriceRuleDto>>> GetPricing(Guid id, CancellationToken ct) =>
        Ok(await _config.GetPricingAsync(UserId, BusinessId, id, ct));

    /// <summary>"Save pricing": replaces all price rules. Existing reservations keep their price.</summary>
    [HttpPut("{id:guid}/pricing")]
    public async Task<ActionResult<List<PriceRuleDto>>> SavePricing(Guid id, SavePricingRequest request, CancellationToken ct) =>
        Ok(await _config.SavePricingAsync(UserId, BusinessId, id, request, ct));

    /// <summary>Price preview for one sport/date/time/duration.</summary>
    [HttpGet("{id:guid}/pricing/preview")]
    public async Task<ActionResult<PricePreviewResponse>> PreviewPrice(Guid id, [FromQuery] Guid sportId, [FromQuery] DateOnly date,
        [FromQuery] TimeOnly startTime, [FromQuery] int durationMinutes, CancellationToken ct) =>
        Ok(await _config.PreviewPriceAsync(UserId, BusinessId, id, sportId, date, startTime, durationMinutes, ct));

    /// <summary>The slot grid the business's customers see, for checking the configuration.</summary>
    [HttpGet("{id:guid}/availability")]
    public async Task<ActionResult<DaySlotsResponse>> GetAvailability(Guid id, [FromQuery] Guid sportId, [FromQuery] DateOnly date,
        [FromQuery] int? durationMinutes, CancellationToken ct) =>
        Ok(await _availability.GetBusinessDayAsync(UserId, BusinessId, id, sportId, date, durationMinutes, ct));

    // ---------------- Manual blocks (story 19) ----------------

    [HttpGet("{id:guid}/blocks")]
    public async Task<ActionResult<List<BlockResponse>>> GetBlocks(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _config.GetBlocksAsync(UserId, BusinessId, id, from, to, ct));

    /// <summary>All blocks of the business (calendar), optionally for one facility.</summary>
    [HttpGet("~/api/business/blocks")]
    public async Task<ActionResult<List<BlockResponse>>> GetAllBlocks([FromQuery] Guid? facilityId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _config.GetBlocksAsync(UserId, BusinessId, facilityId, from, to, ct));

    /// <summary>
    /// "Block time". Conflicting reservations → 409 "reservation_conflicts" (details.conflicts);
    /// resend with conflictResolution. Overlapping another block → 409 "block_overlap".
    /// </summary>
    [HttpPost("{id:guid}/blocks")]
    [ProducesResponseType<BlockResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BlockResponse>> AddBlock(Guid id, CreateBlockRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _config.AddBlockAsync(UserId, BusinessId, id, request, ct));

    [HttpDelete("{id:guid}/blocks/{blockId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteBlock(Guid id, Guid blockId, CancellationToken ct)
    {
        await _config.DeleteBlockAsync(UserId, BusinessId, id, blockId, ct);
        return NoContent();
    }

    // ---------------- Images (story 10) ----------------

    /// <summary>Upload one or more images (multipart/form-data, field "files"). JPG/PNG/WebP, max 5 MB each.</summary>
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<ActionResult<List<FacilityImageResponse>>> UploadImages(Guid id, [FromForm] List<IFormFile> files, CancellationToken ct)
    {
        if (files is null || files.Count == 0)
        {
            throw RequestValidationException.For("files", "Choose at least one image.");
        }

        var uploads = files.Select(f => new UploadedImage(f.OpenReadStream(), f.FileName, f.ContentType, f.Length)).ToList();
        try
        {
            return Ok(await _images.UploadAsync(UserId, BusinessId, id, uploads, ct));
        }
        finally
        {
            foreach (var upload in uploads) await upload.Content.DisposeAsync();
        }
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<ActionResult<List<FacilityImageResponse>>> DeleteImage(Guid id, Guid imageId, CancellationToken ct) =>
        Ok(await _images.DeleteAsync(UserId, BusinessId, id, imageId, ct));

    /// <summary>New order of all image ids; the first becomes the cover.</summary>
    [HttpPut("{id:guid}/images/order")]
    public async Task<ActionResult<List<FacilityImageResponse>>> ReorderImages(Guid id, [FromBody] List<Guid> imageIds, CancellationToken ct) =>
        Ok(await _images.ReorderAsync(UserId, BusinessId, id, imageIds, ct));
}
