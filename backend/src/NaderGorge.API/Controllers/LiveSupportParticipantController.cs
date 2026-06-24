using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;
using MediatR;
using NaderGorge.Application.Features.LiveSupportAI.Commands;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/live-support")]
public sealed class LiveSupportParticipantController(ILiveSupportService service, ILiveSupportGuestSessionService guestSessions, IMediator mediator, ILiveSupportAIVerificationService verificationService, ILiveSupportAIRegistrationService registrationService, ILiveSupportAIHandoffService handoffService) : ControllerBase
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
    [EnableRateLimiting("live-support-ai-message")]
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
            await mediator.Send(new ConfirmLiveSupportAIActionCommand(participant, conversationId, proposalId, proposalId.ToString("N")), ct);
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
            await mediator.Send(new CancelLiveSupportAIDecisionCommand(participant, conversationId, proposalId, proposalId.ToString("N")), ct);
            return Ok(ApiResponse<object>.Ok(new { success = true, message = "Action proposal cancelled." }));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/decisions/{decisionId:guid}/confirm")]
    [EnableRateLimiting("live-support-ai-confirmation")]
    [RequestSizeLimit(16 * 1_024)]
    public async Task<IActionResult> ConfirmDecision(Guid conversationId, Guid decisionId, ParticipantDecisionRequest request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            var executionId = await mediator.Send(new ConfirmLiveSupportAIActionCommand(participant, conversationId, decisionId, request.IdempotencyKey), ct);
            return Ok(ApiResponse<object>.Ok(new { decisionId, executionId, status = "Succeeded" }));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/decisions/{decisionId:guid}/cancel")]
    [EnableRateLimiting("live-support-ai-confirmation")]
    [RequestSizeLimit(16 * 1_024)]
    public async Task<IActionResult> CancelDecision(Guid conversationId, Guid decisionId, ParticipantDecisionRequest request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            await mediator.Send(new CancelLiveSupportAIDecisionCommand(participant, conversationId, decisionId, request.IdempotencyKey), ct);
            return Ok(ApiResponse<object>.Ok(new { decisionId, status = "Cancelled" }));
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
            await handoffService.HandoffAsync(conversationId, participant, participant.StudentUserId, "PARTICIPANT_CONFIRMED", "طلب المستخدم التحويل لموظف دعم.", false, conversationId.ToString("N"), ct);
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
            var proposal = await _service.GetActivePendingActionAsync(participant, conversationId, ct);
            if (proposal is null || proposal.ActionKey != "system.handoff") return NotFound(ApiResponse<object>.Fail("لا يوجد طلب تحويل نشط.", ["HANDOFF_NOT_FOUND"]));
            await mediator.Send(new CancelLiveSupportAIDecisionCommand(participant, conversationId, proposal.Id, proposal.Id.ToString("N")), ct);
            return Ok(ApiResponse<object>.Ok(new { success = true, message = "Handoff cancelled. Returning to AI assistant." }));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/verification/lookup")]
    [EnableRateLimiting("live-support-ai-verification")]
    [RequestSizeLimit(16 * 1_024)]
    public async Task<IActionResult> VerificationLookup(Guid conversationId, LiveSupportAIVerificationLookupCommandDto request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            var sessionDto = await verificationService.StartLookupAsync(participant, conversationId, request, ct);
            return Ok(ApiResponse<LiveSupportAIVerificationStateDto>.Ok(sessionDto));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/verification/{sessionId:guid}/answer")]
    [EnableRateLimiting("live-support-ai-verification")]
    [RequestSizeLimit(16 * 1_024)]
    public async Task<IActionResult> VerificationSessionAnswer(Guid conversationId, Guid sessionId, LiveSupportAIVerificationAnswerRequest request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            var state = await verificationService.SubmitAnswerAsync(participant, conversationId, new LiveSupportAIVerificationAnswerCommandDto(sessionId, request.Answer, request.IdempotencyKey), ct);
            return Ok(ApiResponse<LiveSupportAIVerificationStateDto>.Ok(state));
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
            var active = await _service.GetActiveVerificationSessionAsync(participant, conversationId, ct);
            if (active is null) return NotFound(ApiResponse<object>.Fail("جلسة التحقق غير متاحة.", ["VERIFICATION_NOT_FOUND"]));
            var state = await verificationService.SubmitAnswerAsync(participant, conversationId,
                new LiveSupportAIVerificationAnswerCommandDto(active.SessionId, request.Answer, active.SessionId.ToString("N")), ct);
            return Ok(ApiResponse<LiveSupportAIVerificationStateDto>.Ok(state));
        }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/account-proposal/confirm")]
    public IActionResult ConfirmRegistration(Guid conversationId, LiveSupportRegisterGuestDto request)
    {
        _ = conversationId;
        _ = request;
        return StatusCode(StatusCodes.Status410Gone, ApiResponse<object>.Fail("حدّث الصفحة واستخدم نموذج التسجيل الآمن الكامل.", ["SECURE_REGISTRATION_REQUIRED"]));
    }

    [AllowAnonymous]
    [HttpPost("participant/conversations/{conversationId:guid}/ai/decisions/{decisionId:guid}/register")]
    [EnableRateLimiting("live-support-ai-registration")]
    [RequestSizeLimit(32 * 1_024)]
    public async Task<IActionResult> RegisterFromDecision(Guid conversationId, Guid decisionId, LiveSupportAISecureRegistrationRequest request, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try
        {
            var userId = await registrationService.RegisterAndLinkAsync(participant, conversationId,
                new LiveSupportAISecureRegistrationDto(decisionId, request.IdempotencyKey, request.FullName, request.PhoneNumber, request.Password,
                    request.DateOfBirth, request.Gender, request.Governorate, request.Address, request.EducationStage, request.GradeLevel,
                    request.SchoolName, request.ParentPhoneNumber), ct);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { userId, status = "CreatedAndLinked" }));
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

    [AllowAnonymous]
    [HttpGet("participant/conversations/{conversationId:guid}/ai/snapshot")]
    public async Task<IActionResult> GetAISnapshot(Guid conversationId, CancellationToken ct)
    {
        var participant = await ResolveParticipantAsync(ct);
        if (participant is null) return Unauthorized();
        try { return Ok(ApiResponse<object>.Ok(await _service.GetParticipantAISnapshotAsync(participant, conversationId, ct))); }
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
public sealed record ParticipantDecisionRequest(string IdempotencyKey);
public sealed record LiveSupportAIVerificationAnswerRequest(string Answer, string IdempotencyKey);
public sealed record LiveSupportAISecureRegistrationRequest(
    string IdempotencyKey, string FullName, string PhoneNumber, string Password, DateTime DateOfBirth, string Gender,
    string Governorate, string Address, string EducationStage, string GradeLevel, string SchoolName, string ParentPhoneNumber);
