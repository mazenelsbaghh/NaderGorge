using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/v1/internal/admin-ai")]
[EnableRateLimiting("admin-ai-internal")]
[RequestSizeLimit(131_072)]
public sealed class AdminAIInternalController(
    IConfiguration configuration,
    IAppDbContext db,
    IAdminAICapabilityRegistry capabilities,
    IAdminAIAccessGate access,
    IAdminAIReadExecutor reads,
    IAdminAITurnCompletionService completion) : ControllerBase
{
    private const int MaxReadsPerBatch = 4;

    [HttpGet("readiness")]
    public IActionResult Ready()
    {
        if (!Authorized()) return Unauthorized(SafeError(AdminAIErrorCodes.AccessDenied));
        return !Enabled || capabilities.All.Count == 0
        ? StatusCode(503, SafeError(AdminAIErrorCodes.FeatureDisabled))
        : Ok(new { ready = true, schemaVersion = "1", baselineHash = capabilities.BaselineHash });
    }

    [HttpPost("turns/{turnId:guid}/claim")]
    [RequestSizeLimit(1_024)]
    public async Task<IActionResult> Claim(Guid turnId, [FromBody] AdminAIInternalClaimRequest request, CancellationToken ct)
    {
        if (Guard() is { } guarded) return guarded;
        if (!ValidVersion(request.SchemaVersion) || !Bounded(request.WorkerInstanceId, 100)) return BadRequest(SafeError(AdminAIErrorCodes.InvalidRequest));
        var turn = await db.AdminAITurns.Include(x => x.Conversation).Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == turnId, ct);
        if (turn is null) return NotFound(SafeError(AdminAIErrorCodes.TurnNotFound));
        if (turn.Status.IsTerminal() || turn.CancellationRequestedAt is not null) return Conflict(SafeError(AdminAIErrorCodes.TurnNotClaimable));
        try { await access.RequireCurrentAdminAsync(turn.ActorAdminUserId, checked((int)turn.ExpectedSecurityVersion), ct); }
        catch { return StatusCode(403, SafeError(AdminAIErrorCodes.AccessRevoked)); }
        if (turn.Status != AdminAITurnStatus.Queued && turn.Status != AdminAITurnStatus.Planning) return Conflict(SafeError(AdminAIErrorCodes.TurnLeaseConflict));
        var baseline = await db.AdminAICapabilityBaselines.AsNoTracking().SingleOrDefaultAsync(x => x.Id == turn.CapabilityBaselineId && x.Status == AdminAICapabilityBaselineStatus.Active, ct);
        var policy = await db.AdminAISensitiveDataPolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == turn.SensitiveDataPolicyVersionId && x.Status == AdminAISensitiveDataPolicyStatus.Active, ct);
        if (baseline is null) return Conflict(SafeError(AdminAIErrorCodes.BaselineChanged));
        if (policy is null) return Conflict(SafeError(AdminAIErrorCodes.SensitivePolicyChanged));
        var step = turn.Steps.OrderByDescending(x => x.StepNumber).FirstOrDefault();
        if (step is null || step.StepNumber is < 1 or > 3) return Conflict(SafeError(AdminAIErrorCodes.StepVersionConflict));
        var now = DateTime.UtcNow;
        var deadline = turn.QueuedAt.AddSeconds(Math.Clamp(configuration.GetValue("AdminAI:TurnDeadlineSeconds", 120), 10, 120));
        if (deadline <= now) return StatusCode(410, SafeError(AdminAIErrorCodes.TurnLeaseExpired));
        var leaseExpiry = Min(deadline, now.AddSeconds(Math.Clamp(configuration.GetValue("AdminAI:LeaseSeconds", 60), 10, 60)));
        if (step.CallbackStatus == "Claimed" && step.NextCallbackAttemptAt > now && step.Provider != request.WorkerInstanceId)
            return Conflict(SafeError(AdminAIErrorCodes.TurnLeaseConflict));
        turn.Status = AdminAITurnStatus.Planning; turn.StartedAt ??= now; turn.CurrentStepNumber = step.StepNumber; turn.Version++;
        step.Status = AdminAITurnStepStatus.Claimed; step.StartedAt ??= now; step.ExpectedTurnVersion = turn.Version; step.CallbackStatus = "Claimed"; step.Provider = request.WorkerInstanceId; step.NextCallbackAttemptAt = leaseExpiry; step.Version++;
        var leaseToken = IssueLease(turn.Id, step.StepNumber, turn.Version, leaseExpiry);
        step.CanonicalDecisionHash = HashToken(leaseToken);
        await db.SaveChangesAsync(ct);
        var messages = await db.AdminAIMessages.AsNoTracking().Where(x => x.ConversationId == turn.ConversationId).OrderByDescending(x => x.Sequence).Take(50).OrderBy(x => x.Sequence).Select(x => new { role = x.Role == AdminAIMessageRole.Admin ? "user" : "model", content = x.Content, createdAt = x.CreatedAt }).ToListAsync(ct);
        return Ok(new
        {
            schemaVersion = "1", turnId = turn.Id, turn.ConversationId, turn.ActorAdminUserId,
            stepNumber = step.StepNumber, expectedTurnVersion = turn.Version,
            turn.ExpectedConversationVersion, turn.ExpectedSecurityVersion,
            capabilityBaseline = new { baseline.Id, baseline.Version, manifestHash = baseline.ManifestHash },
            sensitiveDataPolicy = new { policy.Id, policy.Version, policyHash = policy.PolicyHash },
            leaseToken, leaseExpiresAt = leaseExpiry, callbackIdempotencyKey = $"turn-{turn.Id:N}", deadlineAt = deadline,
            systemInstructions = configuration["AdminAI:SystemInstructions"] ?? "استخدم الأدوات المعلنة فقط ولا تطلب أو تعرض أسرارًا.",
            messages,
            readTools = capabilities.All.Where(x => x.Kind == "read").Select(x => new { key = x.Key, descriptionAr = ReadToolDescription(x.Key), parametersJsonSchema = JsonSerializer.Deserialize<JsonElement>(x.InputSchema), maxResultRecords = x.MaxRows, x.TimeoutMs }),
            actionTools = capabilities.All.Where(x => x.Kind != "read").Select(x => new { key = x.Key, descriptionAr = $"إجراء إداري مقترح: {x.Key}", parametersJsonSchema = JsonSerializer.Deserialize<JsonElement>(x.InputSchema), confirmationType = x.Confirmation }),
            budgets = new { maxModelSteps = 3, maxReadCalls = 6, maxReadCallsPerStep = 4, remainingReadCalls = 6 - turn.ReadInvocationCount, maxRedactedContextBytes = 65536, remainingRedactedContextBytes = 65536 - turn.RedactedContextBytes }
        });
    }

    [HttpPost("turns/{turnId:guid}/lease/renew")]
    [RequestSizeLimit(2_048)]
    public async Task<IActionResult> Renew(Guid turnId, [FromBody] AdminAIInternalLeaseRenewRequest request, CancellationToken ct)
    {
        if (Guard() is { } guarded) return guarded;
        if (!ValidVersion(request.SchemaVersion) || !Bounded(request.WorkerInstanceId, 100) || !Bounded(request.LeaseToken, 500)) return BadRequest(SafeError(AdminAIErrorCodes.InvalidRequest));
        var turn = await db.AdminAITurns.Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == turnId, ct);
        if (turn is null) return NotFound(SafeError(AdminAIErrorCodes.TurnNotFound));
        if (turn.Version != request.ExpectedTurnVersion) return Conflict(SafeError(AdminAIErrorCodes.StepVersionConflict));
        var step = turn.Steps.SingleOrDefault(x => x.StepNumber == turn.CurrentStepNumber);
        if (step is null || step.Provider != request.WorkerInstanceId || !ValidateLease(request.LeaseToken, turn.Id, step.StepNumber, turn.Version, step)) return Conflict(SafeError(AdminAIErrorCodes.TurnLeaseExpired));
        if (turn.CancellationRequestedAt is not null || turn.Status.IsTerminal()) return Conflict(SafeError(AdminAIErrorCodes.TurnCancelled));
        try { await access.RequireCurrentAdminAsync(turn.ActorAdminUserId, checked((int)turn.ExpectedSecurityVersion), ct); } catch { return StatusCode(403, SafeError(AdminAIErrorCodes.AccessRevoked)); }
        var expiry = DateTime.UtcNow.AddSeconds(Math.Clamp(configuration.GetValue("AdminAI:LeaseSeconds", 60), 10, 60));
        var leaseToken = IssueLease(turn.Id, step.StepNumber, turn.Version, expiry);
        step.CanonicalDecisionHash = HashToken(leaseToken); step.NextCallbackAttemptAt = expiry; step.Version++;
        await db.SaveChangesAsync(ct);
        return Ok(new { schemaVersion = "1", turnId, turnVersion = turn.Version, leaseToken, leaseExpiresAt = expiry });
    }

    [HttpPost("turns/{turnId:guid}/steps/{stepNumber:int}/reads")]
    [RequestSizeLimit(65_536)]
    public async Task<IActionResult> ReadBatch(Guid turnId, int stepNumber, [FromBody] AdminAIInternalReadRequest request, CancellationToken ct)
    {
        if (Guard() is { } guarded) return guarded;
        if (!ValidVersion(request.SchemaVersion) || request.Calls.Count is < 1 or > MaxReadsPerBatch || !Bounded(request.BatchIdempotencyKey, 200)) return BadRequest(SafeError(AdminAIErrorCodes.ReadArgumentsInvalid));
        var turn = await db.AdminAITurns.Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == turnId, ct);
        if (turn is null) return NotFound(SafeError(AdminAIErrorCodes.TurnNotFound));
        var step = turn.Steps.SingleOrDefault(x => x.StepNumber == stepNumber);
        if (step is null || turn.Version != request.ExpectedTurnVersion) return Conflict(SafeError(AdminAIErrorCodes.StepVersionConflict));
        if (!ValidateLease(request.LeaseToken, turnId, stepNumber, turn.Version, step)) return Conflict(SafeError(AdminAIErrorCodes.TurnLeaseExpired));
        var baseline = await db.AdminAICapabilityBaselines.AsNoTracking().SingleOrDefaultAsync(x => x.Id == turn.CapabilityBaselineId && x.Status == AdminAICapabilityBaselineStatus.Active, ct);
        var policy = await db.AdminAISensitiveDataPolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == turn.SensitiveDataPolicyVersionId && x.Status == AdminAISensitiveDataPolicyStatus.Active, ct);
        if (baseline?.Version != request.ExpectedBaselineVersion) return Conflict(SafeError(AdminAIErrorCodes.BaselineChanged));
        if (policy?.Version != request.ExpectedSensitivePolicyVersion) return Conflict(SafeError(AdminAIErrorCodes.SensitivePolicyChanged));
        if (turn.ReadInvocationCount + request.Calls.Count > 6) return UnprocessableEntity(SafeError(AdminAIErrorCodes.ReadBudgetExceeded));
        try { await access.RequireCurrentAdminAsync(turn.ActorAdminUserId, checked((int)turn.ExpectedSecurityVersion), ct); } catch { return StatusCode(403, SafeError(AdminAIErrorCodes.AccessRevoked)); }
        var results = new List<object>(request.Calls.Count);
        for (var callIndex = 0; callIndex < request.Calls.Count; callIndex++)
        {
            var call = request.Calls[callIndex];
            if (!Bounded(call.CallId, 200) || !capabilities.TryGet(call.CapabilityKey, out var definition) || definition.Kind != "read") return UnprocessableEntity(SafeError(AdminAIErrorCodes.ReadCapabilityNotAllowed));
            try
            {
                var result = await reads.ExecuteAsync(turn.ActorAdminUserId, new AdminAIReadCall(
                    call.CapabilityKey, definition.Version, call.Arguments, turn.Id, step.Id,
                    turn.ReadInvocationCount + callIndex + 1, HttpContext.TraceIdentifier), ct);
                results.Add(new { call.CallId, call.CapabilityKey, status = "Succeeded", data = result, safeErrorCode = (string?)null });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { results.Add(new { call.CallId, call.CapabilityKey, status = "Failed", data = (object?)null, safeErrorCode = AdminAIErrorCodes.ReadTimeout }); }
            catch { results.Add(new { call.CallId, call.CapabilityKey, status = "Rejected", data = (object?)null, safeErrorCode = AdminAIErrorCodes.ReadArgumentsInvalid }); }
        }
        turn.ReadInvocationCount += request.Calls.Count; turn.Status = AdminAITurnStatus.Retrieving; turn.Version++;
        step.Status = AdminAITurnStepStatus.ReadsCompleted; step.ToolCallsRequested += request.Calls.Count; step.Version++;
        await db.SaveChangesAsync(ct);
        var expiry = DateTime.UtcNow.AddSeconds(Math.Clamp(configuration.GetValue("AdminAI:LeaseSeconds", 60), 10, 60));
        var renewedToken = IssueLease(turnId, stepNumber, turn.Version, expiry);
        step.CanonicalDecisionHash = HashToken(renewedToken); step.NextCallbackAttemptAt = expiry;
        await db.SaveChangesAsync(ct);
        return Ok(new { schemaVersion = "1", turnId, stepNumber, turnVersion = turn.Version, leaseToken = renewedToken, leaseExpiresAt = expiry, remainingBudgets = new { readCalls = 6 - turn.ReadInvocationCount, redactedContextBytes = 65536 - turn.RedactedContextBytes }, results });
    }

    [HttpPost("turns/{turnId:guid}/complete")]
    [RequestSizeLimit(262_144)]
    public async Task<IActionResult> Complete(Guid turnId, [FromBody] AdminAIInternalCompleteRequest request, CancellationToken ct)
    {
        if (Guard() is { } guarded) return guarded;
        if (!ValidVersion(request.SchemaVersion) || !Bounded(request.DecisionHash, 64) || request.LatencyMs < 0) return BadRequest(SafeError(AdminAIErrorCodes.DecisionSchemaInvalid));
        var turn = await db.AdminAITurns.Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == turnId, ct);
        if (turn is null) return NotFound(SafeError(AdminAIErrorCodes.TurnNotFound));
        var step = turn.Steps.SingleOrDefault(x => x.StepNumber == request.ExpectedStepNumber);
        if (step is null || turn.Version != request.ExpectedTurnVersion || !ValidateLease(request.LeaseToken, turnId, request.ExpectedStepNumber, turn.Version, step)) return Conflict(SafeError(AdminAIErrorCodes.TurnLeaseExpired));
        if (turn.CancellationRequestedAt is not null || turn.Status.IsTerminal()) return Conflict(SafeError(AdminAIErrorCodes.CallbackDiscarded));
        try
        {
            var result = await completion.CompleteAsync(turnId, request, ct);
            return Ok(new { schemaVersion = "1", turnId, status = result.Status.ToString(), turnVersion = result.TurnVersion, proposalIds = result.ProposalIds, result.Replayed, result.Discarded });
        }
        catch (InvalidOperationException exception) when (AdminAIErrorCodes.All.Contains(exception.Message)) { return Conflict(SafeError(exception.Message)); }
    }

    [HttpPost("turns/{turnId:guid}/fail")]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Fail(Guid turnId, [FromBody] AdminAIInternalFailRequest request, CancellationToken ct)
    {
        if (Guard() is { } guarded) return guarded;
        if (!ValidVersion(request.SchemaVersion) || !Bounded(request.CallbackIdempotencyKey, 200) || request.LatencyMs < 0) return BadRequest(SafeError(AdminAIErrorCodes.InvalidRequest));
        var turn = await db.AdminAITurns.Include(x => x.Steps).SingleOrDefaultAsync(x => x.Id == turnId, ct);
        if (turn is null) return NotFound(SafeError(AdminAIErrorCodes.TurnNotFound));
        var step = turn.Steps.OrderByDescending(x => x.StepNumber).FirstOrDefault();
        if (step is null || !ValidateLease(request.LeaseToken, turnId, step.StepNumber, turn.Version, step)) return Conflict(SafeError(AdminAIErrorCodes.TurnLeaseExpired));
        if (turn.Status.IsTerminal()) return Ok(new { schemaVersion = "1", turnId, status = turn.Status.ToString(), turnVersion = turn.Version });
        var cancelled = turn.CancellationRequestedAt is not null || request.FailureCode == AdminAIInternalFailureCode.CANCELLED;
        turn.Status = cancelled ? AdminAITurnStatus.Cancelled : AdminAITurnStatus.Failed;
        turn.FailureCode = request.FailureCode.ToString(); turn.CompletedAt = DateTime.UtcNow; turn.Version++;
        step.Status = cancelled ? AdminAITurnStepStatus.Cancelled : AdminAITurnStepStatus.Failed; step.FailureCode = request.FailureCode.ToString(); step.CompletedAt = turn.CompletedAt; step.CallbackStatus = "Delivered"; step.CallbackAttemptCount++; step.Version++;
        await db.SaveChangesAsync(ct);
        return Ok(new { schemaVersion = "1", turnId, status = turn.Status.ToString(), turnVersion = turn.Version });
    }

    private IActionResult? Guard()
    {
        if (!Authorized()) return Unauthorized(SafeError(AdminAIErrorCodes.AccessDenied));
        return !Enabled || capabilities.All.Count == 0 ? StatusCode(503, SafeError(AdminAIErrorCodes.FeatureDisabled)) : null;
    }

    private bool Enabled => configuration.GetValue("AdminAI:Enabled", false);
    private bool Authorized()
    {
        var expected = configuration["AdminAI:CallbackSecret"];
        var supplied = Request.Headers["X-Internal-Token"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }
    private static bool Bounded(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
    private static bool ValidVersion(string value) => value == "1";
    private static DateTime Min(DateTime left, DateTime right) => left <= right ? left : right;
    private string IssueLease(Guid turnId, int stepNumber, long version, DateTime expiresAt)
    {
        var body = $"{turnId:N}.{stepNumber}.{version}.{new DateTimeOffset(expiresAt).ToUnixTimeSeconds()}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(configuration["AdminAI:CallbackSecret"]!));
        return $"{body}.{Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant()}";
    }
    private bool ValidateLease(string token, Guid turnId, int stepNumber, long version, NaderGorge.Domain.Entities.AdminAI.AdminAITurnStep step)
    {
        var parts = token.Split('.');
        if (parts.Length != 5 || parts[0] != turnId.ToString("N") || parts[1] != stepNumber.ToString() || parts[2] != version.ToString() || !long.TryParse(parts[3], out var expiry) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiry) return false;
        var body = string.Join('.', parts.Take(4));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(configuration["AdminAI:CallbackSecret"]!));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        return step.CallbackStatus == "Claimed" && step.NextCallbackAttemptAt > DateTime.UtcNow &&
            FixedEquals(step.CanonicalDecisionHash, HashToken(token)) && FixedEquals(parts[4], expected);
    }
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right) => left is not null && right is not null && left.Length == right.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static AdminAIError SafeError(string code) => new(code, "تعذر إكمال الطلب بأمان.");

    private static string ReadToolDescription(string key) => key switch
    {
        "identity.users.summary" => "إحصاءات المستخدمين والطلاب، ومنها العدد الإجمالي للطلاب.",
        "content.summary" => "ملخص المواد والأقسام والدروس والفيديوهات التعليمية.",
        "assessment.summary" => "ملخص الاختبارات والمحاولات والنتائج.",
        "codes.summary" => "ملخص الأكواد والباقات المشتركة.",
        "community.summary" => "ملخص المجتمع والتعليقات والمراجعة.",
        "forms-settings.summary" => "ملخص النماذج وإعدادات المنصة الآمنة.",
        "hr-people.summary" => "ملخص الموظفين والهيكل البشري دون بيانات حساسة.",
        "hr-operations.summary" => "ملخص الحضور والإجازات والعمليات البشرية.",
        "hr-lifecycle.summary" => "ملخص التوظيف والعقود ودورة الموظف.",
        "legacy-finance.summary" => "ملخص المالية القديمة وحسابات المدرسين.",
        "platform-finance.summary" => "ملخص المركز المالي العام والخزينة والمصروفات.",
        "teacher-finance.summary" => "ملخص اتفاقيات وتسويات مالية المدرسين.",
        "sales.summary" => "ملخص المبيعات والكوبونات والطلبات.",
        "wallet-recharge.summary" => "ملخص المحافظ وعمليات الشحن.",
        "teacher.summary" => "ملخص المدرسين وموادهم.",
        "operations.summary" => "ملخص المهام والعمليات الداخلية.",
        "live-support.summary" => "ملخص الدعم المباشر وإدارته.",
        "reporting.summary" => "ملخص التقارير والسجلات الآمنة ومؤشرات المنصة.",
        _ => "ملخص إداري آمن ومحدود."
    };
}
