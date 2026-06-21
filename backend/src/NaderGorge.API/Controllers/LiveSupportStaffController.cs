using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/live-support/staff")]
[Authorize(Roles = "Admin,Assistant,AssistantReviewer,Staff")]
public sealed class LiveSupportStaffController(ILiveSupportService service, ILiveSupportActionService actions) : ControllerBase
{
    private readonly ILiveSupportService _service = service;
    private readonly ILiveSupportActionService _actions = actions;

    [HttpGet("bootstrap")]
    public async Task<IActionResult> Bootstrap(CancellationToken ct)
    {
        try { return Ok(ApiResponse<LiveSupportStaffBootstrapDto>.Ok(await _service.GetStaffBootstrapAsync(UserId(), User.IsInRole("Admin"), ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> Send(Guid conversationId, SendMessageRequest request, CancellationToken ct)
    {
        try { return StatusCode(StatusCodes.Status201Created, ApiResponse<LiveSupportSendResultDto>.Ok(await _service.SendStaffMessageAsync(UserId(), User.IsInRole("Admin"), conversationId, request.ClientMessageId, request.Content ?? string.Empty, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> Messages(Guid conversationId, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        try { return Ok(ApiResponse<IReadOnlyList<LiveSupportMessageDto>>.Ok(await _service.GetStaffMessagesAsync(UserId(), User.IsInRole("Admin"), conversationId, pageSize, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [HttpPost("conversations/{conversationId:guid}/close")]
    public async Task<IActionResult> Close(Guid conversationId, CloseConversationRequest request, CancellationToken ct)
    {
        try { return Ok(ApiResponse<LiveSupportConversationDto>.Ok(await _service.CloseAsync(UserId(), User.IsInRole("Admin"), conversationId, request.Reason, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [HttpPost("conversations/{conversationId:guid}/transfer")]
    public async Task<IActionResult> Transfer(Guid conversationId, TransferConversationRequest request, CancellationToken ct)
    {
        try { return Ok(ApiResponse<LiveSupportConversationDto>.Ok(await _service.TransferAsync(UserId(), User.IsInRole("Admin"), conversationId, request.TargetStaffUserId, request.Reason, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [HttpGet("conversations/{conversationId:guid}/students/search")]
    public async Task<IActionResult> SearchStudents(Guid conversationId, [FromQuery] string query, CancellationToken ct)
    {
        try { return Ok(ApiResponse<IReadOnlyList<LiveSupportStudentSearchDto>>.Ok(await _service.SearchStudentsAsync(UserId(), User.IsInRole("Admin"), conversationId, query, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [HttpPut("conversations/{conversationId:guid}/student-link")]
    public async Task<IActionResult> LinkStudent(Guid conversationId, ChangeStudentLinkRequest request, CancellationToken ct)
    {
        try { return Ok(ApiResponse<LiveSupportConversationDto>.Ok(await _service.ChangeStudentLinkAsync(UserId(), User.IsInRole("Admin"), conversationId, request.StudentUserId, request.Reason, request.ExpectedVersion, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [HttpGet("conversations/{conversationId:guid}/student-context")]
    public async Task<IActionResult> StudentContext(Guid conversationId, CancellationToken ct)
    {
        try { return Ok(ApiResponse<LiveSupportStudentContextDto>.Ok(await _service.GetStudentContextAsync(UserId(), User.IsInRole("Admin"), conversationId, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [HttpGet("conversations/{conversationId:guid}/actions")]
    public async Task<IActionResult> ActionCatalog(Guid conversationId, CancellationToken ct)
    {
        try { return Ok(ApiResponse<IReadOnlyList<LiveSupportActionDefinitionDto>>.Ok(await _actions.GetCatalogAsync(UserId(), User.IsInRole("Admin"), conversationId, ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    [HttpPost("conversations/{conversationId:guid}/actions/{actionKey}")]
    [EnableRateLimiting("live-support-action")]
    public async Task<IActionResult> ExecuteAction(Guid conversationId, string actionKey, ExecuteLiveSupportActionRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct)
    {
        try { return Ok(ApiResponse<LiveSupportActionResultDto>.Ok(await _actions.ExecuteAsync(new LiveSupportActionRequest(UserId(), User.IsInRole("Admin"), conversationId, actionKey, idempotencyKey, request.ConfirmationVersion, request.Payload), ct))); }
        catch (LiveSupportException ex) { return Error(ex); }
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private IActionResult Error(LiveSupportException ex) => StatusCode(ex.Code == LiveSupportErrorCodes.Forbidden ? 403 : ex.Code == "NOT_FOUND" ? 404 : 409, ApiResponse<object>.Fail(ex.Message, [ex.Code]));
}

public sealed record CloseConversationRequest(string Reason);
public sealed record TransferConversationRequest(Guid? TargetStaffUserId, string Reason);
public sealed record ChangeStudentLinkRequest(Guid? StudentUserId, string Reason, long ExpectedVersion);
public sealed record ExecuteLiveSupportActionRequest(string ConfirmationVersion, System.Text.Json.JsonElement Payload);
