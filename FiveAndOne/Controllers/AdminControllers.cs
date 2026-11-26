using FiveAndOne.API.Extensions;
using FiveAndOne.Application.Common.Auth;
using FiveAndOne.Application.Dtos.Bookings;
using FiveAndOne.Application.Dtos.Catalog;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Application.Dtos.Subscriptions;
using FiveAndOne.Application.Dtos.Users;
using FiveAndOne.Application.Services.Accounts;
using FiveAndOne.Application.Services.Admin;
using FiveAndOne.Application.Services.Bookings;
using FiveAndOne.Application.Services.Catalog;
using FiveAndOne.Application.Services.Community;
using FiveAndOne.Application.Services.Facilities;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Application.Services.Subscriptions;
using FiveAndOne.Application.Services.Users;
using FiveAndOne.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiveAndOne.API.Controllers;

/// <summary>
/// Platform administration (stories 47-50). Admin and SuperAdmin can operate; plans, feature flags,
/// platform settings and role changes are SuperAdmin-only. Every change is written to the audit log.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = RoleNames.AdminOrAbove)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class AdminController : ControllerBase
{
    private readonly IAdminPlatformService _platform;
    private readonly IAuditService _audit;

    public AdminController(IAdminPlatformService platform, IAuditService audit)
    {
        _platform = platform;
        _audit = audit;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardResponse>> Dashboard(CancellationToken ct) => Ok(await _platform.GetDashboardAsync(ct));

    /// <summary>Platform analytics (story 50). Kosovo local dates; default last 30 days.</summary>
    [HttpGet("analytics")]
    public async Task<ActionResult<PlatformAnalyticsResponse>> Analytics([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _platform.GetAnalyticsAsync(from, to, ct));

    /// <summary>Searchable audit log: action prefix, entity, actor, business, date range.</summary>
    [HttpGet("audit-logs")]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> AuditLogs([FromQuery] AuditLogQuery query, CancellationToken ct) =>
        Ok(await _audit.SearchAsync(query, ct));

    // ---------------- Businesses ----------------

    [HttpGet("businesses")]
    public async Task<ActionResult<PagedResponse<AdminBusinessResponse>>> Businesses([FromQuery] BusinessStatus? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _platform.GetBusinessesAsync(status, search, page, pageSize, ct));

    [HttpGet("businesses/{id:guid}")]
    public async Task<ActionResult<AdminBusinessDetailsResponse>> Business(Guid id, CancellationToken ct) => Ok(await _platform.GetBusinessAsync(id, ct));

    /// <summary>Suspend a business: hidden from discovery, no new reservations; existing reservations stay.</summary>
    [HttpPost("businesses/{id:guid}/suspend")]
    public async Task<ActionResult<AdminBusinessDetailsResponse>> SuspendBusiness(Guid id, RequiredReasonRequest body, CancellationToken ct) =>
        Ok(await _platform.SuspendBusinessAsync(User.GetUserId(), id, body.Reason!, ct));

    [HttpPost("businesses/{id:guid}/unsuspend")]
    public async Task<ActionResult<AdminBusinessDetailsResponse>> UnsuspendBusiness(Guid id, CancellationToken ct) =>
        Ok(await _platform.UnsuspendBusinessAsync(User.GetUserId(), id, ct));
}

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = RoleNames.AdminOrAbove)]
public class AdminUsersController : ControllerBase
{
    private readonly IAccountService _accounts;
    private readonly IUserService _users;

    public AdminUsersController(IAccountService accounts, IUserService users)
    {
        _accounts = accounts;
        _users = users;
    }

    /// <summary>Customers and accounts: filter by role and status, search name/username/email/phone.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<UserResponse>>> List([FromQuery] AdminUserQuery query, CancellationToken ct) =>
        Ok(await _accounts.AdminSearchAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserDetailsResponse>> Get(Guid id, CancellationToken ct) => Ok(await _accounts.AdminGetAsync(id, ct));

    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<UserResponse>> Suspend(Guid id, RequiredReasonRequest body, CancellationToken ct) =>
        Ok(await _accounts.AdminSetStatusAsync(User.GetUserId(), id, UserStatus.Suspended, body.Reason, ct));

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<UserResponse>> Activate(Guid id, ReasonRequest body, CancellationToken ct) =>
        Ok(await _accounts.AdminSetStatusAsync(User.GetUserId(), id, UserStatus.Active, body.Reason, ct));

    /// <summary>Only a SuperAdmin can hand out roles (including Admin and SuperAdmin).</summary>
    [HttpPut("{id:guid}/role")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<UserResponse>> ChangeRole(Guid id, ChangeRoleRequest request, CancellationToken ct) =>
        Ok(await _users.ChangeRoleAsync(User.GetUserId(), id, request.Role, ct));
}

[ApiController]
[Route("api/admin/facilities")]
[Authorize(Roles = RoleNames.AdminOrAbove)]
public class AdminFacilitiesController : ControllerBase
{
    private readonly IFacilityService _facilities;

    public AdminFacilitiesController(IFacilityService facilities) => _facilities = facilities;

    [HttpGet]
    public async Task<ActionResult<PagedResponse<FacilitySummaryResponse>>> List([FromQuery] FacilityStatus? status, [FromQuery] Guid? businessId,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _facilities.GetAllAsync(status, businessId, search, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FacilityResponse>> Get(Guid id, CancellationToken ct) => Ok(await _facilities.GetAnyAsync(id, ct));

    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<FacilityResponse>> Suspend(Guid id, SuspendFacilityRequest request, CancellationToken ct) =>
        Ok(await _facilities.SuspendAsync(User.GetUserId(), id, request.Reason!, ct));

    /// <summary>Lifts a suspension; the facility comes back Hidden and the owner re-publishes it.</summary>
    [HttpPost("{id:guid}/unsuspend")]
    public async Task<ActionResult<FacilityResponse>> Unsuspend(Guid id, CancellationToken ct) =>
        Ok(await _facilities.UnsuspendAsync(User.GetUserId(), id, ct));

    [HttpPost("{id:guid}/hide")]
    public async Task<ActionResult<FacilityResponse>> Hide(Guid id, ReasonRequest body, CancellationToken ct) =>
        Ok(await _facilities.AdminHideAsync(User.GetUserId(), id, body.Reason, ct));
}

[ApiController]
[Route("api/admin/reservations")]
[Authorize(Roles = RoleNames.AdminOrAbove)]
public class AdminReservationsController : ControllerBase
{
    private readonly IBusinessReservationService _reservations;

    public AdminReservationsController(IBusinessReservationService reservations) => _reservations = reservations;

    [HttpGet]
    public async Task<ActionResult<PagedResponse<BusinessReservationResponse>>> List([FromQuery] Guid? businessId, [FromQuery] BusinessReservationQuery query, CancellationToken ct) =>
        Ok(await _reservations.AdminListAsync(businessId, query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessReservationResponse>> Get(Guid id, CancellationToken ct) => Ok(await _reservations.AdminGetAsync(id, ct));

    /// <summary>Controlled, audited cancellation (reason required). Goes through the normal status rules.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<BusinessReservationResponse>> Cancel(Guid id, RequiredReasonRequest body, CancellationToken ct) =>
        Ok(await _reservations.AdminCancelAsync(User.GetUserId(), id, body.Reason!, ct));
}

[ApiController]
[Route("api/admin/catalog")]
[Authorize(Roles = RoleNames.AdminOrAbove)]
public class AdminCatalogController : ControllerBase
{
    private readonly ICatalogService _catalog;

    public AdminCatalogController(ICatalogService catalog) => _catalog = catalog;

    [HttpGet("sports")]
    public async Task<ActionResult<List<AdminSportResponse>>> Sports(CancellationToken ct) => Ok(await _catalog.GetAllSportsAsync(ct));

    [HttpPost("sports")]
    public async Task<ActionResult<AdminSportResponse>> CreateSport(SaveSportRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _catalog.SaveSportAsync(User.GetUserId(), null, request, ct));

    /// <summary>Edit a sport, incl. isActive (deactivated sports can't be added to facilities).</summary>
    [HttpPut("sports/{id:guid}")]
    public async Task<ActionResult<AdminSportResponse>> UpdateSport(Guid id, SaveSportRequest request, CancellationToken ct) =>
        Ok(await _catalog.SaveSportAsync(User.GetUserId(), id, request, ct));

    [HttpGet("amenities")]
    public async Task<ActionResult<List<AdminAmenityResponse>>> Amenities(CancellationToken ct) => Ok(await _catalog.GetAllAmenitiesAsync(ct));

    [HttpPost("amenities")]
    public async Task<ActionResult<AdminAmenityResponse>> CreateAmenity(SaveAmenityRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _catalog.SaveAmenityAsync(User.GetUserId(), null, request, ct));

    [HttpPut("amenities/{id:guid}")]
    public async Task<ActionResult<AdminAmenityResponse>> UpdateAmenity(Guid id, SaveAmenityRequest request, CancellationToken ct) =>
        Ok(await _catalog.SaveAmenityAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("amenities/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAmenity(Guid id, CancellationToken ct)
    {
        await _catalog.DeleteAmenityAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}

/// <summary>Plans, subscriptions, retention, feature flags and platform settings.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = RoleNames.AdminOrAbove)]
public class AdminConfigurationController : ControllerBase
{
    private readonly ISubscriptionService _subscriptions;
    private readonly IFeatureFlags _flags;
    private readonly IPlatformSettings _settings;

    public AdminConfigurationController(ISubscriptionService subscriptions, IFeatureFlags flags, IPlatformSettings settings)
    {
        _subscriptions = subscriptions;
        _flags = flags;
        _settings = settings;
    }

    [HttpGet("plans")]
    public async Task<ActionResult<List<PlanResponse>>> Plans(CancellationToken ct) => Ok(await _subscriptions.GetAllPlansAsync(ct));

    [HttpPost("plans")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<PlanResponse>> CreatePlan(SavePlanRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _subscriptions.SavePlanAsync(User.GetUserId(), null, request, ct));

    /// <summary>Edit, activate or deactivate a plan.</summary>
    [HttpPut("plans/{id:guid}")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<PlanResponse>> UpdatePlan(Guid id, SavePlanRequest request, CancellationToken ct) =>
        Ok(await _subscriptions.SavePlanAsync(User.GetUserId(), id, request, ct));

    [HttpGet("subscriptions")]
    public async Task<ActionResult<PagedResponse<AdminSubscriptionResponse>>> Subscriptions([FromQuery] SubscriptionStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _subscriptions.AdminListAsync(status, page, pageSize, ct));

    /// <summary>Inactive (unpaid) businesses and when their data will be anonymized (story 46).</summary>
    [HttpGet("retention")]
    public async Task<ActionResult<List<RetentionStatusResponse>>> Retention(CancellationToken ct) => Ok(await _subscriptions.AdminRetentionAsync(ct));

    [HttpGet("feature-flags")]
    public async Task<ActionResult<List<FeatureFlagResponse>>> Flags(CancellationToken ct) => Ok(await _flags.GetAllAsync(ct));

    [HttpPost("feature-flags")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<FeatureFlagResponse>> CreateFlag(CreateFeatureFlagRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _flags.CreateAsync(User.GetUserId(), request, ct));

    [HttpPost("feature-flags/{key}/enable")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<FeatureFlagResponse>> Enable(string key, CancellationToken ct) =>
        Ok(await _flags.SetEnabledAsync(User.GetUserId(), key, true, ct));

    [HttpPost("feature-flags/{key}/disable")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<FeatureFlagResponse>> Disable(string key, CancellationToken ct) =>
        Ok(await _flags.SetEnabledAsync(User.GetUserId(), key, false, ct));

    /// <summary>Booking limits, guest controls, grace days, retention years, review window.</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<List<PlatformSettingResponse>>> Settings(CancellationToken ct) => Ok(await _settings.GetAllAsync(ct));

    [HttpPut("settings/{key}")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<PlatformSettingResponse>> UpdateSetting(string key, [FromBody] UpdateSettingRequest request, CancellationToken ct) =>
        Ok(await _settings.UpdateAsync(User.GetUserId(), key, request.Value ?? string.Empty, ct));

    /// <summary>
    /// Runs a background job now instead of waiting for its schedule:
    /// subscriptions (renewals, grace, expiry), retention (anonymization), notifications (send outbox), reservation-expiry.
    /// </summary>
    [HttpPost("jobs/{name}/run")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<object>> RunJob(string name, [FromServices] INotificationService notifications,
        [FromServices] FiveAndOne.Application.Services.Payments.IReservationPaymentService payments, CancellationToken ct)
    {
        var processed = name.ToLowerInvariant() switch
        {
            "subscriptions" => await _subscriptions.ProcessRenewalsAsync(ct),
            "retention" => await _subscriptions.ProcessRetentionAsync(ct),
            "notifications" => await notifications.DispatchPendingAsync(500, ct),
            "reservation-expiry" => await payments.ExpireUnpaidAsync(ct),
            _ => throw new FiveAndOne.Application.Common.Exceptions.NotFoundException("Unknown job. Use subscriptions, retention, notifications or reservation-expiry.")
        };
        return Ok(new { job = name, processed });
    }
}

public class UpdateSettingRequest
{
    public string? Value { get; set; }
}

/// <summary>Moderation and promotions (stories 63, 65, 66).</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = RoleNames.AdminOrAbove)]
public class AdminModerationController : ControllerBase
{
    private readonly IReviewService _reviews;
    private readonly IPromotionService _promotions;

    public AdminModerationController(IReviewService reviews, IPromotionService promotions)
    {
        _reviews = reviews;
        _promotions = promotions;
    }

    [HttpGet("reports")]
    public async Task<ActionResult<PagedResponse<ReportResponse>>> Reports([FromQuery] ReportStatus? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _reviews.GetReportsAsync(status, page, pageSize, ct));

    [HttpPost("reports/{id:guid}/resolve")]
    public async Task<ActionResult<ReportResponse>> Resolve(Guid id, ResolveReportRequest request, CancellationToken ct) =>
        Ok(await _reviews.ResolveReportAsync(User.GetUserId(), id, request, ct));

    /// <summary>Publish, hide or remove a review.</summary>
    [HttpPut("reviews/{id:guid}/status")]
    public async Task<ActionResult<ReviewResponse>> Moderate(Guid id, ModerateReviewRequest request, CancellationToken ct) =>
        Ok(await _reviews.ModerateAsync(User.GetUserId(), id, request, ct));

    [HttpGet("promotions")]
    public async Task<ActionResult<List<PromotionResponse>>> Promotions(CancellationToken ct) => Ok(await _promotions.GetAllAsync(ct));

    [HttpPost("promotions")]
    public async Task<ActionResult<PromotionResponse>> CreatePromotion(SavePromotionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _promotions.SaveAsync(User.GetUserId(), null, request, ct));

    [HttpPut("promotions/{id:guid}")]
    public async Task<ActionResult<PromotionResponse>> UpdatePromotion(Guid id, SavePromotionRequest request, CancellationToken ct) =>
        Ok(await _promotions.SaveAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("promotions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePromotion(Guid id, CancellationToken ct)
    {
        await _promotions.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
