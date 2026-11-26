using FiveAndOne.API.Extensions;
using FiveAndOne.Application.Dtos.Bookings;
using FiveAndOne.Application.Dtos.Businesses;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Subscriptions;
using FiveAndOne.Application.Services.Bookings;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Application.Services.Community;
using FiveAndOne.Application.Services.Dashboard;
using FiveAndOne.Application.Services.Subscriptions;
using FiveAndOne.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiveAndOne.API.Controllers;

/// <summary>Business profile, switcher, dashboard and analytics (stories 07, 39, 41, 58).</summary>
[ApiController]
[Route("api/business")]
[Authorize]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class BusinessController : ControllerBase
{
    private readonly IBusinessService _businesses;
    private readonly IBusinessDashboardService _dashboard;
    private readonly IReviewService _reviews;

    public BusinessController(IBusinessService businesses, IBusinessDashboardService dashboard, IReviewService reviews)
    {
        _businesses = businesses;
        _dashboard = dashboard;
        _reviews = reviews;
    }

    /// <summary>Businesses the user can act on (business switcher). Pass the chosen id as X-Business-Id.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<MyBusinessResponse>>> Mine(CancellationToken ct) =>
        Ok(await _businesses.GetMyBusinessesAsync(User.GetUserId(), ct));

    /// <summary>Create an additional business (feature flag "multiple_businesses" for a 2nd one).</summary>
    [HttpPost]
    [ProducesResponseType<BusinessProfileResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BusinessProfileResponse>> Create(CreateBusinessRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _businesses.CreateAdditionalBusinessAsync(User.GetUserId(), request, ct));

    /// <summary>"Business profile" page. The registration number is never returned.</summary>
    [HttpGet("profile")]
    public async Task<ActionResult<BusinessProfileResponse>> GetProfile(CancellationToken ct) =>
        Ok(await _businesses.GetProfileAsync(User.GetUserId(), Request.GetBusinessId(), ct));

    /// <summary>"Save changes" on the business profile.</summary>
    [HttpPut("profile")]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusinessProfileResponse>> UpdateProfile(UpdateBusinessProfileRequest request, CancellationToken ct) =>
        Ok(await _businesses.UpdateProfileAsync(User.GetUserId(), Request.GetBusinessId(), request, ct));

    /// <summary>Business-wide default cancellation deadline and policy text (story 33).</summary>
    [HttpPut("cancellation-policy")]
    public async Task<ActionResult<BusinessProfileResponse>> UpdateCancellationPolicy(CancellationPolicyRequest request, CancellationToken ct) =>
        Ok(await _businesses.UpdateCancellationPolicyAsync(User.GetUserId(), Request.GetBusinessId(), request, ct));

    /// <summary>Dashboard overview (story 39).</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardOverviewResponse>> Dashboard(CancellationToken ct) =>
        Ok(await _dashboard.GetOverviewAsync(User.GetUserId(), Request.GetBusinessId(), ct));

    /// <summary>Basic analytics (story 41). Dates are Kosovo local days; default last 30 days.</summary>
    [HttpGet("analytics")]
    public async Task<ActionResult<AnalyticsResponse>> Analytics([FromQuery] AnalyticsQuery query, CancellationToken ct) =>
        Ok(await _dashboard.GetAnalyticsAsync(User.GetUserId(), Request.GetBusinessId(), query, ct));

    /// <summary>Public reply to a review of one of the business's facilities (story 63, flag "reviews").</summary>
    [HttpPut("reviews/{reviewId:guid}/reply")]
    public async Task<ActionResult<ReviewResponse>> ReplyToReview(Guid reviewId, [FromBody] ReasonRequest body, CancellationToken ct) =>
        Ok(await _reviews.ReplyAsync(User.GetUserId(), Request.GetBusinessId(), reviewId, body.Reason ?? string.Empty, ct));
}

/// <summary>Reservations from the business side (stories 20, 35, 36, 40).</summary>
[ApiController]
[Route("api/business/reservations")]
[Authorize]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class BusinessReservationsController : ControllerBase
{
    private readonly IBusinessReservationService _reservations;

    public BusinessReservationsController(IBusinessReservationService reservations) => _reservations = reservations;

    private Guid UserId => User.GetUserId();
    private Guid? BusinessId => Request.GetBusinessId();

    /// <summary>List/calendar with filters: facility, sport, date range, status, source (SportX/External), search; scope = upcoming | past | all.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<BusinessReservationResponse>>> List([FromQuery] BusinessReservationQuery query, CancellationToken ct) =>
        Ok(await _reservations.ListAsync(UserId, BusinessId, query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessReservationResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await _reservations.GetAsync(UserId, BusinessId, id, ct));

    /// <summary>"Add external reservation" (phone, WhatsApp, social media, walk-in...). Conflicts → 409 with details.</summary>
    [HttpPost("external")]
    [ProducesResponseType<BusinessReservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BusinessReservationResponse>> CreateExternal(ExternalReservationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _reservations.CreateExternalAsync(UserId, BusinessId, request, ct));

    [HttpPut("{id:guid}/external")]
    public async Task<ActionResult<BusinessReservationResponse>> UpdateExternal(Guid id, ExternalReservationRequest request, CancellationToken ct) =>
        Ok(await _reservations.UpdateExternalAsync(UserId, BusinessId, id, request, ct));

    /// <summary>"Cancel reservation" (by the business). A reason is required; the customer keeps a record.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<BusinessReservationResponse>> Cancel(Guid id, RequiredReasonRequest body, CancellationToken ct) =>
        Ok(await _reservations.CancelAsync(UserId, BusinessId, id, body.Reason!, ct));

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<BusinessReservationResponse>> Complete(Guid id, CancellationToken ct) =>
        Ok(await _reservations.SetOutcomeAsync(UserId, BusinessId, id, BookingStatus.Completed, ct));

    [HttpPost("{id:guid}/no-show")]
    public async Task<ActionResult<BusinessReservationResponse>> NoShow(Guid id, CancellationToken ct) =>
        Ok(await _reservations.SetOutcomeAsync(UserId, BusinessId, id, BookingStatus.NoShow, ct));

    /// <summary>Move a reservation to another free time (any time; duration can change).</summary>
    [HttpPut("{id:guid}/reschedule")]
    public async Task<ActionResult<BusinessReservationResponse>> Reschedule(Guid id, RescheduleReservationRequest request, CancellationToken ct) =>
        Ok(await _reservations.RescheduleAsync(UserId, BusinessId, id, request, ct));

    [HttpPut("{id:guid}/notes")]
    public async Task<ActionResult<BusinessReservationResponse>> Notes(Guid id, ReservationNotesRequest request, CancellationToken ct) =>
        Ok(await _reservations.UpdateNotesAsync(UserId, BusinessId, id, request.InternalNotes, ct));
}

/// <summary>The business's subscription (stories 43-46). Activation happens only through the payment webhook.</summary>
[ApiController]
[Route("api/business/subscription")]
[Authorize]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class BusinessSubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptions;

    public BusinessSubscriptionController(ISubscriptionService subscriptions) => _subscriptions = subscriptions;

    /// <summary>Status, plan, billing date, payment status and payment history.</summary>
    [HttpGet]
    public async Task<ActionResult<SubscriptionResponse>> Get(CancellationToken ct) =>
        Ok(await _subscriptions.GetMineAsync(User.GetUserId(), Request.GetBusinessId(), ct));

    /// <summary>Start checkout for a plan. Redirect the user to checkoutUrl; poll GET until status is Active.</summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(SubscribeRequest request, CancellationToken ct) =>
        Ok(await _subscriptions.SubscribeAsync(User.GetUserId(), Request.GetBusinessId(), request.PlanId!.Value, ct));

    /// <summary>"Pay now" for an unpaid renewal (grace period) or to reactivate after expiry.</summary>
    [HttpPost("pay")]
    public async Task<ActionResult<CheckoutResponse>> Pay(CancellationToken ct) =>
        Ok(await _subscriptions.PayOutstandingAsync(User.GetUserId(), Request.GetBusinessId(), ct));

    /// <summary>Cancel at the end of the current period (no refund, access until then).</summary>
    [HttpPost("cancel")]
    public async Task<ActionResult<SubscriptionResponse>> Cancel(CancellationToken ct) =>
        Ok(await _subscriptions.SetCancelAtPeriodEndAsync(User.GetUserId(), Request.GetBusinessId(), true, ct));

    [HttpPost("resume")]
    public async Task<ActionResult<SubscriptionResponse>> Resume(CancellationToken ct) =>
        Ok(await _subscriptions.SetCancelAtPeriodEndAsync(User.GetUserId(), Request.GetBusinessId(), false, ct));
}

/// <summary>Staff with permissions (story 57, flag "staff_invitations") and venues (story 59, flag "venues").</summary>
[ApiController]
[Authorize]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class BusinessTeamController : ControllerBase
{
    private readonly IStaffService _staff;
    private readonly IVenueService _venues;

    public BusinessTeamController(IStaffService staff, IVenueService venues)
    {
        _staff = staff;
        _venues = venues;
    }

    [HttpGet("api/business/staff")]
    public async Task<ActionResult<List<StaffMemberResponse>>> Members(CancellationToken ct) =>
        Ok(await _staff.GetMembersAsync(User.GetUserId(), Request.GetBusinessId(), ct));

    /// <summary>Change a staff member's permissions or deactivate their access.</summary>
    [HttpPut("api/business/staff/{userId:guid}")]
    public async Task<ActionResult<StaffMemberResponse>> UpdateMember(Guid userId, UpdateStaffRequest request, CancellationToken ct) =>
        Ok(await _staff.UpdateMemberAsync(User.GetUserId(), Request.GetBusinessId(), userId, request, ct));

    [HttpGet("api/business/staff/invitations")]
    public async Task<ActionResult<List<InvitationResponse>>> Invitations(CancellationToken ct) =>
        Ok(await _staff.GetInvitationsAsync(User.GetUserId(), Request.GetBusinessId(), ct));

    [HttpPost("api/business/staff/invitations")]
    public async Task<ActionResult<InvitationResponse>> Invite(InviteStaffRequest request, CancellationToken ct) =>
        Ok(await _staff.InviteAsync(User.GetUserId(), Request.GetBusinessId(), request, ct));

    [HttpPost("api/business/staff/invitations/{id:guid}/resend")]
    public async Task<ActionResult<InvitationResponse>> Resend(Guid id, CancellationToken ct) =>
        Ok(await _staff.ResendAsync(User.GetUserId(), Request.GetBusinessId(), id, ct));

    [HttpDelete("api/business/staff/invitations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _staff.RevokeAsync(User.GetUserId(), Request.GetBusinessId(), id, ct);
        return NoContent();
    }

    /// <summary>The invited person accepts (logged in with the invited email address).</summary>
    [HttpPost("api/invitations/accept")]
    public async Task<ActionResult<MyBusinessResponseLite>> Accept(AcceptInvitationRequest request, CancellationToken ct) =>
        Ok(await _staff.AcceptAsync(User.GetUserId(), request.Token!, ct));

    [HttpGet("api/business/venues")]
    public async Task<ActionResult<List<VenueResponse>>> Venues(CancellationToken ct) =>
        Ok(await _venues.GetAllAsync(User.GetUserId(), Request.GetBusinessId(), ct));

    [HttpPost("api/business/venues")]
    public async Task<ActionResult<VenueResponse>> CreateVenue(SaveVenueRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _venues.SaveAsync(User.GetUserId(), Request.GetBusinessId(), null, request, ct));

    [HttpPut("api/business/venues/{id:guid}")]
    public async Task<ActionResult<VenueResponse>> UpdateVenue(Guid id, SaveVenueRequest request, CancellationToken ct) =>
        Ok(await _venues.SaveAsync(User.GetUserId(), Request.GetBusinessId(), id, request, ct));

    [HttpDelete("api/business/venues/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteVenue(Guid id, CancellationToken ct)
    {
        await _venues.DeleteAsync(User.GetUserId(), Request.GetBusinessId(), id, ct);
        return NoContent();
    }
}
