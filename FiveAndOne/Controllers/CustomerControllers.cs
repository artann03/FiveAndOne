using FiveAndOne.API.Extensions;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Bookings;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Users;
using FiveAndOne.Application.Services.Accounts;
using FiveAndOne.Application.Services.Bookings;
using FiveAndOne.Application.Services.Community;
using FiveAndOne.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiveAndOne.API.Controllers;

/// <summary>Reservations for customers and guests (stories 29-37).</summary>
[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public ReservationsController(IBookingService bookings) => _bookings = bookings;

    /// <summary>
    /// Book a slot. Signed in = customer reservation; no token = guest (send "guest" details).
    /// Send an "Idempotency-Key" header per submit attempt and the optional "X-Device-Id" header.
    /// 409 "slot_unavailable": "This time slot is no longer available. Please choose another time."
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ReservationResponse>> Create(CreateReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        // A token that failed validation must not silently turn a customer into a guest.
        if (Request.Headers.Authorization.Count > 0 && User.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedException("The access token is invalid or expired. Refresh it and try again.");
        }

        var result = await _bookings.CreateAsync(User.TryGetUserId(), request, idempotencyKey, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>The customer's reservations. scope = upcoming (default) | past | cancelled | all.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ReservationResponse>>> Mine([FromQuery] string? scope, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _bookings.GetMineAsync(User.GetUserId(), scope, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await _bookings.GetMineAsync(User.GetUserId(), id, ct));

    /// <summary>Cancel before the facility's deadline (409 "cancellation_deadline_passed" after it).</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ReservationResponse>> Cancel(Guid id, ReasonRequest body, CancellationToken ct) =>
        Ok(await _bookings.CancelAsync(User.GetUserId(), id, body.Reason, ct));

    /// <summary>"Change date/time": same facility, sport and duration; before the deadline.</summary>
    [HttpPut("{id:guid}/reschedule")]
    public async Task<ActionResult<ReservationResponse>> Reschedule(Guid id, RescheduleReservationRequest request, CancellationToken ct) =>
        Ok(await _bookings.RescheduleAsync(User.GetUserId(), id, request, ct));

    /// <summary>Guest retrieval with the secret reference from the confirmation (story 37).</summary>
    [HttpGet("guest/{token}")]
    [AllowAnonymous]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> GetGuest(string token, CancellationToken ct) =>
        Ok(await _bookings.GetGuestAsync(token, ct));

    [HttpPost("guest/{token}/cancel")]
    [AllowAnonymous]
    public async Task<ActionResult<ReservationResponse>> CancelGuest(string token, ReasonRequest body, CancellationToken ct) =>
        Ok(await _bookings.CancelGuestAsync(token, body.Reason, ct));
}

/// <summary>Risk-triggered SMS verification for guest bookings (story 61, flag "sms_verification").</summary>
[ApiController]
[Route("api/verification/phone")]
[AllowAnonymous]
public class PhoneVerificationController : ControllerBase
{
    private readonly IPhoneVerificationService _verification;

    public PhoneVerificationController(IPhoneVerificationService verification) => _verification = verification;

    [HttpPost("send")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Send(SendCodeRequest request, CancellationToken ct)
    {
        await _verification.SendCodeAsync(request.Phone!, ct);
        return NoContent();
    }

    [HttpPost("verify")]
    public async Task<ActionResult<VerificationTokenResponse>> Verify(VerifyCodeRequest request, CancellationToken ct) =>
        Ok(await _verification.VerifyAsync(request.Phone!, request.Code!, ct));
}

/// <summary>The signed-in user's own account (story 28, 52). Password change and deletion live under /api/auth.</summary>
[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accounts;

    public AccountController(IAccountService accounts) => _accounts = accounts;

    [HttpGet("profile")]
    public async Task<ActionResult<UserResponse>> GetProfile(CancellationToken ct) => Ok(await _accounts.GetProfileAsync(User.GetUserId(), ct));

    /// <summary>"Save profile".</summary>
    [HttpPut("profile")]
    public async Task<ActionResult<UserResponse>> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await _accounts.UpdateProfileAsync(User.GetUserId(), request, ct));

    /// <summary>Download everything stored about the user (data-subject access request).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct) => Ok(await _accounts.ExportAsync(User.GetUserId(), ct));
}

/// <summary>Player profiles and teammate discovery (stories 64, 65; flags "player_profiles", "teams").</summary>
[ApiController]
[Route("api/players")]
[Authorize]
public class PlayersController : ControllerBase
{
    private readonly IPlayerService _players;

    public PlayersController(IPlayerService players) => _players = players;

    [HttpGet("me")]
    public async Task<ActionResult<PlayerProfileResponse>> Me(CancellationToken ct) => Ok(await _players.GetMineAsync(User.GetUserId(), ct));

    [HttpPut("me")]
    public async Task<ActionResult<PlayerProfileResponse>> SaveMe(SavePlayerProfileRequest request, CancellationToken ct) =>
        Ok(await _players.SaveMineAsync(User.GetUserId(), request, ct));

    /// <summary>Find teammates: visible profiles by sport, city and level.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<PlayerProfileResponse>>> Search([FromQuery] Guid? sportId, [FromQuery] string? city,
        [FromQuery] SkillLevel? level, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _players.SearchAsync(User.GetUserId(), sportId, city, level, page, pageSize, ct));

    [HttpGet("{username}")]
    [AllowAnonymous]
    public async Task<ActionResult<PlayerProfileResponse>> Get(string username, CancellationToken ct) =>
        Ok(await _players.GetAsync(User.TryGetUserId(), username, ct));
}

[ApiController]
[Route("api/teams")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teams;

    public TeamsController(ITeamService teams) => _teams = teams;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResponse<TeamResponse>>> Search([FromQuery] Guid? sportId, [FromQuery] string? city, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _teams.SearchAsync(User.TryGetUserId(), sportId, city, search, page, pageSize, ct));

    [HttpGet("mine")]
    public async Task<ActionResult<List<TeamResponse>>> Mine(CancellationToken ct) => Ok(await _teams.GetMineAsync(User.GetUserId(), ct));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<TeamResponse>> Get(Guid id, CancellationToken ct) => Ok(await _teams.GetAsync(User.TryGetUserId(), id, ct));

    [HttpPost]
    public async Task<ActionResult<TeamResponse>> Create(SaveTeamRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _teams.CreateAsync(User.GetUserId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TeamResponse>> Update(Guid id, SaveTeamRequest request, CancellationToken ct) =>
        Ok(await _teams.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _teams.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    /// <summary>Captain invites a player by username.</summary>
    [HttpPost("{id:guid}/invitations")]
    public async Task<ActionResult<TeamResponse>> Invite(Guid id, InviteToTeamRequest request, CancellationToken ct) =>
        Ok(await _teams.InviteAsync(User.GetUserId(), id, request.Username!, ct));

    /// <summary>Accept (true) or decline (false) an invitation.</summary>
    [HttpPost("{id:guid}/invitations/respond")]
    public async Task<ActionResult<TeamResponse>> Respond(Guid id, [FromQuery] bool accept, CancellationToken ct) =>
        Ok(await _teams.RespondAsync(User.GetUserId(), id, accept, ct));

    [HttpPost("{id:guid}/requests")]
    public async Task<ActionResult<TeamResponse>> RequestToJoin(Guid id, CancellationToken ct) =>
        Ok(await _teams.RequestToJoinAsync(User.GetUserId(), id, ct));

    /// <summary>Captain approves (true) or rejects (false) a join request.</summary>
    [HttpPost("{id:guid}/requests/{userId:guid}")]
    public async Task<ActionResult<TeamResponse>> Decide(Guid id, Guid userId, [FromQuery] bool approve, CancellationToken ct) =>
        Ok(await _teams.DecideRequestAsync(User.GetUserId(), id, userId, approve, ct));

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<ActionResult<TeamResponse>> Remove(Guid id, Guid userId, CancellationToken ct) =>
        Ok(await _teams.RemoveMemberAsync(User.GetUserId(), id, userId, ct));

    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await _teams.LeaveAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}

/// <summary>Writing reviews and reporting content (story 63).</summary>
[ApiController]
[Route("api")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviews;

    public ReviewsController(IReviewService reviews) => _reviews = reviews;

    /// <summary>Review a completed reservation (once, within the review window).</summary>
    [HttpPost("reviews")]
    public async Task<ActionResult<ReviewResponse>> Create(CreateReviewRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _reviews.CreateAsync(User.GetUserId(), request, ct));

    [HttpDelete("reviews/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _reviews.DeleteOwnAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    /// <summary>Report a review, user or team for moderation. targetType = Review | User | Team.</summary>
    [HttpPost("reports/{targetType}/{targetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Report(ReportTargetType targetType, Guid targetId, ReportRequest request, CancellationToken ct)
    {
        await _reviews.ReportAsync(User.GetUserId(), targetType, targetId, request.Reason!, ct);
        return NoContent();
    }
}
