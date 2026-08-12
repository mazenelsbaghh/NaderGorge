using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Commands;
using NaderGorge.Application.Features.AdminAI.Queries;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/ai-agent")]
[Authorize(Roles = "Admin")]
public sealed class AdminAIAgentController(IAdminAIConversationService conversations, IAdminAITurnOrchestrator turns, AdminAIProposalCommands proposals, IAdminAISecureInputService secureInputs, AdminAIAuditQueries audit, AdminAICapabilityBaselineQueries baselines, IConfiguration configuration) : ControllerBase
{
    [HttpGet("conversations")]
    public Task<IActionResult> List([FromQuery] AdminAIConversationStatus? status, [FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        SafeAsync(async () => Ok(await conversations.ListAsync(User.RequireUserId(), status, cursor, pageSize, ct)));

    [HttpPost("conversations")]
    public Task<IActionResult> Create(CreateAdminAIConversationRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct) =>
        SafeAsync(async () => StatusCode(201, await conversations.CreateAsync(User.RequireUserId(), request.Title, idempotencyKey, ct)));

    [HttpPatch("conversations/{conversationId:guid}")]
    public Task<IActionResult> Rename(Guid conversationId, RenameAdminAIConversationRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct) =>
        SafeAsync(async () => Ok(await conversations.RenameAsync(User.RequireUserId(), conversationId, request.Title, request.ExpectedVersion, idempotencyKey, ct)));

    [HttpPost("conversations/{conversationId:guid}/archive")]
    public Task<IActionResult> Archive(Guid conversationId, AdminAIExpectedVersionRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct) =>
        SafeAsync(async () => Ok(await conversations.SetArchivedAsync(User.RequireUserId(), conversationId, true, request.ExpectedVersion, idempotencyKey, ct)));

    [HttpPost("conversations/{conversationId:guid}/restore")]
    public Task<IActionResult> Restore(Guid conversationId, AdminAIExpectedVersionRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct) =>
        SafeAsync(async () => Ok(await conversations.SetArchivedAsync(User.RequireUserId(), conversationId, false, request.ExpectedVersion, idempotencyKey, ct)));

    [HttpGet("conversations/{conversationId:guid}/snapshot")]
    public Task<IActionResult> Snapshot(Guid conversationId, [FromQuery] long? beforeSequence, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        SafeAsync(async () => Ok(await conversations.SnapshotAsync(User.RequireUserId(), conversationId, beforeSequence, pageSize, ct)));

    [HttpPost("conversations/{conversationId:guid}/turns")]
    [EnableRateLimiting("admin-ai-turn")]
    public Task<IActionResult> Queue(Guid conversationId, SendAdminAIMessageRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct) =>
        SafeAsync(async () => Accepted(await turns.QueueAsync(User.RequireUserId(), conversationId, request.Content, request.ExpectedConversationVersion, idempotencyKey, ct)));

    [HttpPost("conversations/{conversationId:guid}/turns/{turnId:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid conversationId, Guid turnId, CancelAdminAITurnRequest request, CancellationToken ct) =>
        SafeAsync(async () => Ok(await turns.CancelAsync(User.RequireUserId(), conversationId, turnId, request.ExpectedVersion, ct)));

    [HttpGet("proposals/{proposalId:guid}")]
    public async Task<IActionResult> Proposal(Guid proposalId, CancellationToken ct)
    {
        if (!Enabled) return Disabled();
        try { return Ok(await proposals.GetAsync(User.RequireUserId(), proposalId, ct)); }
        catch (UnauthorizedAccessException) { return Forbidden(); }
        catch (KeyNotFoundException) { return NotFoundProposal(); }
        catch (ArgumentException) { return BadRequest(Error(AdminAIErrorCodes.InvalidRequest)); }
        catch (InvalidOperationException exception) { return ProposalConflict(exception); }
    }

    [HttpPost("proposals/{proposalId:guid}/confirm")]
    [EnableRateLimiting("admin-ai-confirmation")]
    public async Task<IActionResult> Confirm(Guid proposalId, ConfirmAdminAIProposalRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct)
    {
        if (!Enabled) return Disabled();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) return BadRequest(Error(AdminAIErrorCodes.InvalidRequest));
        try { return Ok(await proposals.ConfirmAsync(User.RequireUserId(), proposalId, request.ExpectedVersion, request.TypedPhrase, idempotencyKey, ct)); }
        catch (KeyNotFoundException) { return NotFoundProposal(); }
        catch (UnauthorizedAccessException exception)
        {
            return exception.Message.Contains("confirmation", StringComparison.OrdinalIgnoreCase)
                ? StatusCode(StatusCodes.Status422UnprocessableEntity, Error(AdminAIErrorCodes.InvalidConfirmation))
                : Forbidden();
        }
        catch (NotSupportedException) { return Conflict(Error(AdminAIErrorCodes.CapabilityUnavailable)); }
        catch (ArgumentException) { return BadRequest(Error(AdminAIErrorCodes.InvalidRequest)); }
        catch (DbUpdateConcurrencyException) { return Conflict(Error(AdminAIErrorCodes.StaleState)); }
        catch (InvalidOperationException exception) { return ProposalConflict(exception); }
    }

    [HttpPost("proposals/{proposalId:guid}/cancel")]
    public async Task<IActionResult> CancelProposal(Guid proposalId, AdminAIExpectedVersionRequest request, CancellationToken ct)
    {
        if (!Enabled) return Disabled();
        try { return Ok(await proposals.CancelAsync(User.RequireUserId(), proposalId, request.ExpectedVersion, ct)); }
        catch (UnauthorizedAccessException) { return Forbidden(); }
        catch (KeyNotFoundException) { return NotFoundProposal(); }
        catch (ArgumentException) { return BadRequest(Error(AdminAIErrorCodes.InvalidRequest)); }
        catch (DbUpdateConcurrencyException) { return Conflict(Error(AdminAIErrorCodes.StaleState)); }
        catch (InvalidOperationException exception) { return ProposalConflict(exception); }
    }

    [HttpPost("proposals/{proposalId:guid}/secure-input-grants")]
    [EnableRateLimiting("admin-ai-secure-input")]
    public Task<IActionResult> IssueSecureInput(Guid proposalId, IssueAdminAISecureInputRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct) =>
        SafeAsync(async () => string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200
            ? BadRequest(Error(AdminAIErrorCodes.InvalidRequest))
            : StatusCode(201, await secureInputs.IssueAsync(User.RequireUserId(), proposalId, request.InputKind, request.ExpectedProposalVersion, ct)));

    [HttpPost("secure-input-grants/{grantId:guid}/submit")]
    [EnableRateLimiting("admin-ai-secure-input")]
    [RequestSizeLimit(65_536)]
    [HttpLogging(HttpLoggingFields.None)]
    public async Task<IActionResult> SubmitSecureInput(Guid grantId, SubmitAdminAISecureInputRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct)
    {
        if (!Enabled) return Disabled();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) return BadRequest(Error(AdminAIErrorCodes.InvalidRequest));
        try { return Ok(await secureInputs.SubmitAsync(User.RequireUserId(), grantId, request.Token, request.Kind, System.Text.Encoding.UTF8.GetBytes(request.Value), ct)); }
        catch (AdminAISecureInputGoneException) { return StatusCode(StatusCodes.Status410Gone, new AdminAIError(AdminAIErrorCodes.Expired, "انتهت صلاحية الإدخال الآمن.")); }
        catch (UnauthorizedAccessException) { return Forbidden(); }
        catch (KeyNotFoundException) { return NotFound(Error(AdminAIErrorCodes.CapabilityUnavailable)); }
        catch (ArgumentException) { return BadRequest(Error(AdminAIErrorCodes.InvalidRequest)); }
        catch (InvalidOperationException exception) { return ProposalConflict(exception); }
    }

    [HttpGet("action-evidence")]
    public Task<IActionResult> ActionEvidence([FromQuery] string? cursor, [FromQuery] int pageSize = 50, [FromQuery] string? capabilityKey = null, [FromQuery] Guid? actorAdminUserId = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default) =>
        SafeAsync(async () => Ok(await audit.ListAsync(User.RequireUserId(), cursor, pageSize, capabilityKey, actorAdminUserId, from, to, ct)));

    [HttpGet("capability-baseline")]
    public async Task<IActionResult> CapabilityBaseline(CancellationToken ct)
    {
        if (!Enabled) return Disabled();
        try { return Ok(new { success = true, data = await baselines.ActiveAsync(User.RequireUserId(), ct) }); }
        catch (KeyNotFoundException) { return StatusCode(503, new AdminAIError(AdminAIErrorCodes.CapabilityUnavailable, "خط أساس قدرات الوكيل غير متاح.")); }
        catch (UnauthorizedAccessException) { return Forbidden(); }
        catch (ArgumentException) { return BadRequest(Error(AdminAIErrorCodes.InvalidRequest)); }
    }

    private bool Enabled => configuration.GetValue("AdminAI:Enabled", false);
    private async Task<IActionResult> SafeAsync(Func<Task<IActionResult>> action)
    {
        if (!Enabled) return Disabled();
        try { return await action(); }
        catch (UnauthorizedAccessException) { return Forbidden(); }
        catch (KeyNotFoundException) { return NotFound(Error(AdminAIErrorCodes.CapabilityUnavailable)); }
        catch (ArgumentException) { return BadRequest(Error(AdminAIErrorCodes.InvalidRequest)); }
        catch (DbUpdateConcurrencyException) { return Conflict(Error(AdminAIErrorCodes.StaleState)); }
        catch (InvalidOperationException exception)
        {
            return Conflict(Error(exception.Message.Contains("Idempotency", StringComparison.OrdinalIgnoreCase)
                ? AdminAIErrorCodes.IdempotencyConflict
                : AdminAIErrorCodes.StaleState));
        }
    }
    private ObjectResult Disabled() => StatusCode(503, new AdminAIError(AdminAIErrorCodes.FeatureDisabled, "وكيل الإدارة غير متاح حاليًا."));
    private ObjectResult Forbidden() => StatusCode(StatusCodes.Status403Forbidden, Error(AdminAIErrorCodes.AccessDenied));
    private NotFoundObjectResult NotFoundProposal() => NotFound(Error(AdminAIErrorCodes.CapabilityUnavailable));
    private ConflictObjectResult ProposalConflict(InvalidOperationException exception) => Conflict(Error(
        exception.Message.Contains("Idempotency", StringComparison.OrdinalIgnoreCase) ? AdminAIErrorCodes.IdempotencyConflict
        : exception.Message.Contains("expired", StringComparison.OrdinalIgnoreCase) ? AdminAIErrorCodes.Expired
        : AdminAIErrorCodes.StaleState));
    private static AdminAIError Error(string code) => new(code, "تعذر إكمال الطلب بأمان.");
}
