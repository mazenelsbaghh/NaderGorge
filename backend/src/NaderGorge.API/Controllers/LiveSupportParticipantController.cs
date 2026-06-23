using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/live-support")]
public sealed class LiveSupportParticipantController(ILiveSupportService service, ILiveSupportGuestSessionService guestSessions) : ControllerBase
{
    private const string GuestCookie = "massar_support_guest";
    private readonly ILiveSupportService _service = service;

    [AllowAnonymous]
    [HttpGet("availability")]
    public async Task<IActionResult> Availability(CancellationToken ct) => Ok(ApiResponse<LiveSupportAvailabilityDto>.Ok(await _service.GetAvailabilityAsync(ct)));

    [AllowAnonymous]
    [HttpPost("guest/session")]
    [EnableRateLimiting("live-support-public")]
    public async Task<IActionResult> CreateGuestSession(CreateGuestSessionRequest request, CancellationToken ct)
    {
        try
        {
            var session = await guestSessions.IssueAsync(request.DisplayName, request.PhoneNumber, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", Request.Headers.UserAgent.ToString(), ct);
            Response.Cookies.Append(GuestCookie, session.CookieToken, new CookieOptions { HttpOnly = true, Secure = !HttpContext.Request.Host.Host.Contains("localhost", StringComparison.OrdinalIgnoreCase), SameSite = SameSiteMode.Lax, Expires = session.ExpiresAt, IsEssential = true, Path = "/" });
            return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { session.Id, session.DisplayName, session.ExpiresAt }));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpDelete("guest/session")]
    public async Task<IActionResult> RevokeGuestSession(CancellationToken ct)
    {
        await guestSessions.RevokeAsync(Request.Cookies[GuestCookie], ct);
        Response.Cookies.Delete(GuestCookie, new CookieOptions { Path = "/" });
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("participant/conversations")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        return participant is null ? Unauthorized() : Ok(ApiResponse<IReadOnlyList<LiveSupportConversationDto>>.Ok(await _service.ListParticipantConversationsAsync(participant, ct)));
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations")]
    [EnableRateLimiting("live-support-public")]
    public async Task<IActionResult> Create(CreateConversationRequest request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try { return StatusCode(StatusCodes.Status201Created, ApiResponse<LiveSupportConversationDto>.Ok(await _service.CreateConversationAsync(participant, request.Subject, request.PreviousConversationId, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpGet("participant/conversations/{conversationId:guid}")]
    public async Task<IActionResult> Get(Guid conversationId, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        var item = await _service.GetParticipantConversationAsync(participant, conversationId, ct);
        return item is null ? NotFound() : Ok(ApiResponse<LiveSupportConversationDto>.Ok(item));
    }

    [AllowAnonymous]
    [HttpGet("participant/conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> Messages(Guid conversationId, [FromQuery] int pageSize = 50, [FromQuery] string? cursor = null, [FromQuery] long? afterSequence = null, CancellationToken ct = default)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try { return Ok(ApiResponse<LiveSupportMessagePageDto>.Ok(await _service.GetParticipantMessagePageAsync(participant, conversationId, pageSize, cursor, afterSequence, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/attachments")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [EnableRateLimiting("live-support-public")]
    public async Task<IActionResult> Upload(Guid conversationId, IFormFile file, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            await using var stream = file.OpenReadStream();
            return StatusCode(201, ApiResponse<LiveSupportAttachmentDto>.Ok(await _service.SaveParticipantAttachmentAsync(participant, conversationId, stream, file.FileName, file.ContentType, file.Length, ct)));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpGet("participant/conversations/{conversationId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid conversationId, Guid attachmentId, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            var item = await _service.OpenParticipantAttachmentAsync(participant, conversationId, attachmentId, ct);
            return File(item.Content, item.ContentType, item.FileName, enableRangeProcessing: true);
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/messages")]
    [EnableRateLimiting("live-support-public")]
    public async Task<IActionResult> Send(Guid conversationId, SendMessageRequest request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            var result = request.AttachmentId.HasValue
                ? await _service.SendParticipantAttachmentMessageAsync(participant, conversationId, request.ClientMessageId, request.AttachmentId.Value, request.Content, request.Type, ct)
                : await _service.SendParticipantMessageAsync(participant, conversationId, request.ClientMessageId, request.Content ?? string.Empty, request.Type, ct);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<LiveSupportSendResultDto>.Ok(result));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/abandon")]
    public async Task<IActionResult> Abandon(Guid conversationId, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try { return Ok(ApiResponse<LiveSupportConversationDto>.Ok(await _service.AbandonAsync(participant, conversationId, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/rating")]
    public async Task<IActionResult> Rating(Guid conversationId, SubmitRatingRequest request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try { await _service.SubmitRatingAsync(participant, conversationId, request.Stars, request.Comment, ct); return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok("تم تسجيل تقييمك.")); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/actions/{proposalId:guid}/confirm")]
    public async Task<IActionResult> ConfirmAction(Guid conversationId, Guid proposalId, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            await _service.ConfirmPendingActionAsync(participant, conversationId, proposalId, ct);
            return Ok(ApiResponse<object>.Ok(new { success = true, message = "Action executed successfully." }));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/actions/{proposalId:guid}/cancel")]
    public async Task<IActionResult> CancelAction(Guid conversationId, Guid proposalId, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            await _service.CancelPendingActionAsync(participant, conversationId, proposalId, ct);
            return Ok(ApiResponse<object>.Ok(new { success = true, message = "Action proposal cancelled." }));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/handoff/confirm")]
    public async Task<IActionResult> ConfirmHandoff(Guid conversationId, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            await _service.ConfirmHandoffAsync(participant, conversationId, ct);
            return Ok(ApiResponse<object>.Ok(new { success = true, message = "Conversation transferred to human support." }));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/handoff/cancel")]
    public async Task<IActionResult> CancelHandoff(Guid conversationId, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            await _service.CancelHandoffAsync(participant, conversationId, ct);
            return Ok(ApiResponse<object>.Ok(new { success = true, message = "Handoff cancelled. Returning to AI assistant." }));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/verification/lookup")]
    public async Task<IActionResult> VerificationLookup(Guid conversationId, LiveSupportLookupRequestDto request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            var sessionDto = await _service.StartVerificationLookupAsync(participant, conversationId, request, ct);
            return Ok(ApiResponse<LiveSupportAIVerificationSessionDto>.Ok(sessionDto));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/verification/answer")]
    public async Task<IActionResult> VerificationAnswer(Guid conversationId, LiveSupportAnswerChallengeDto request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            var sessionDto = await _service.SubmitVerificationChallengeAsync(participant, conversationId, request, ct);
            return Ok(ApiResponse<LiveSupportAIVerificationSessionDto>.Ok(sessionDto));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/account-proposal/confirm")]
    public async Task<IActionResult> ConfirmRegistration(Guid conversationId, LiveSupportRegisterGuestDto request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            await _service.ConfirmRegistrationProposalAsync(participant, conversationId, request, ct);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { success = true, message = "Account created and linked successfully." }));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpGet("participant/conversations/{conversationId:guid}/ai/pending-action")]
    public async Task<IActionResult> GetActivePendingAction(Guid conversationId, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            var action = await _service.GetActivePendingActionAsync(participant, conversationId, ct);
            return Ok(ApiResponse<LiveSupportAIPendingActionDto?>.Ok(action));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpGet("participant/conversations/{conversationId:guid}/ai/verification/session")]
    public async Task<IActionResult> GetActiveVerificationSession(Guid conversationId, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            var session = await _service.GetActiveVerificationSessionAsync(participant, conversationId, ct);
            return Ok(ApiResponse<LiveSupportAIVerificationSessionDto?>.Ok(session));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    private async Task<LiveSupportParticipantIdentity?> ResolveParticipantAsync(CancellationToken ct)
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Student") && Guid.TryParse(idValue, out var userId))
            return new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, userId, null);
        return await guestSessions.ValidateAsync(Request.Cookies[GuestCookie], ct);
    }

    private IActionResult Error(LiveSupportException ex)
    {
        var status = ex.Code switch
        {
            LiveSupportErrorCodes.SupportUnavailable => StatusCodes.Status423Locked,
            LiveSupportErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
            "VALIDATION_ERROR" => StatusCodes.Status400BadRequest,
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status409Conflict
        };
        return StatusCode(status, ApiResponse<object>.Fail(ex.Message, [ex.Code]));
    }
}

public sealed record CreateGuestSessionRequest(string DisplayName, string PhoneNumber);
public sealed record CreateConversationRequest(string? Subject, Guid? PreviousConversationId);
public sealed record SendMessageRequest(string ClientMessageId, string? Content, LiveSupportMessageType Type = LiveSupportMessageType.Text, Guid? AttachmentId = null);
public sealed record SubmitRatingRequest(int Stars, string? Comment);
