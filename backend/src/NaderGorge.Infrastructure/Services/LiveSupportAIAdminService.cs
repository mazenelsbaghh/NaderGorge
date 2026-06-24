using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services.LiveSupportAI;

namespace NaderGorge.Infrastructure.Services;

public sealed class LiveSupportAIAdminService(
    IAppDbContext db,
    ILiveSupportAIKnowledgeService knowledge,
    ILiveSupportAIWorkerPreviewClient? previewClient = null) : ILiveSupportAIAdminService
{
    public LiveSupportAICatalogsDto GetCatalogs() => LiveSupportAICatalog.Snapshot();

    public async Task<LiveSupportAIConfigDto> GetConfigAsync(CancellationToken ct)
    {
        var draft = await db.LiveSupportAIPolicyVersions.AsNoTracking().Where(x => x.Status == LiveSupportAIPolicyStatus.Draft)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        var published = await db.LiveSupportAIPolicyVersions.AsNoTracking().Where(x => x.Status == LiveSupportAIPolicyStatus.Published)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        return new(ToDtoOrNull(draft), ToDtoOrNull(published), GetCatalogs());
    }

    public async Task<LiveSupportAIPolicyDto> SaveDraftAsync(Guid adminUserId, SaveLiveSupportAIDraftRequest request, CancellationToken ct)
    {
        Validate(request);
        var draft = await db.LiveSupportAIPolicyVersions.Where(x => x.Status == LiveSupportAIPolicyStatus.Draft)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        if (draft is not null && request.ExpectedVersion != draft.Version)
            throw new LiveSupportAIAdminException("VERSION_CONFLICT", "تم تعديل المسودة في جلسة أخرى.");

        if (draft is null)
        {
            var nextVersion = (await db.LiveSupportAIPolicyVersions.MaxAsync(x => (long?)x.VersionNumber, ct) ?? 0) + 1;
            draft = new LiveSupportAIPolicyVersion { VersionNumber = nextVersion, Status = LiveSupportAIPolicyStatus.Draft, CreatedByUserId = adminUserId };
            db.LiveSupportAIPolicyVersions.Add(draft);
        }

        Apply(draft, request);
        draft.Version++;
        await db.SaveChangesAsync(ct);
        return ToDto(draft);
    }

    public async Task<LiveSupportAIPolicyDto> PublishAsync(Guid adminUserId, long expectedVersion, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var draft = await db.LiveSupportAIPolicyVersions.SingleOrDefaultAsync(x => x.Status == LiveSupportAIPolicyStatus.Draft, ct)
            ?? throw new LiveSupportAIAdminException("DRAFT_NOT_FOUND", "لا توجد مسودة للنشر.");
        if (draft.Version != expectedVersion)
            throw new LiveSupportAIAdminException("VERSION_CONFLICT", "المسودة قديمة، أعد تحميلها.");
        Validate(ToRequest(draft));

        var current = await db.LiveSupportAIPolicyVersions.SingleOrDefaultAsync(x => x.Status == LiveSupportAIPolicyStatus.Published, ct);
        if (current is not null)
        {
            current.Status = LiveSupportAIPolicyStatus.Superseded;
            current.IsEnabled = false;
            current.Version++;
            await db.SaveChangesAsync(ct);
        }

        draft.Status = LiveSupportAIPolicyStatus.Published;
        draft.IsEnabled = true;
        draft.PublishedByUserId = adminUserId;
        draft.PublishedAt = DateTime.UtcNow;
        draft.Version++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(draft);
    }

    public async Task DisableAsync(Guid adminUserId, long expectedVersion, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var published = await db.LiveSupportAIPolicyVersions.SingleOrDefaultAsync(x => x.Status == LiveSupportAIPolicyStatus.Published, ct)
            ?? throw new LiveSupportAIAdminException("PUBLISHED_POLICY_NOT_FOUND", "لا توجد سياسة منشورة لإيقافها.");
        if (published.Version != expectedVersion)
            throw new LiveSupportAIAdminException("VERSION_CONFLICT", "تغيرت السياسة المنشورة، أعد تحميل الصفحة.");
        if (!published.IsEnabled)
            throw new LiveSupportAIAdminException("POLICY_ALREADY_DISABLED", "المساعد متوقف بالفعل.");
        published.IsEnabled = false;
        published.Version++;
        var now = DateTime.UtcNow;
        var activeStates = await db.LiveSupportAIConversationStates.Where(item => item.Mode == LiveSupportAIMode.AiActive).ToListAsync(ct);
        foreach (var state in activeStates)
        {
            state.DisableRequestedAt = now;
            state.Version++;
        }
        db.AuditLogs.Add(new NaderGorge.Domain.Entities.AuditLog
        {
            Action = "DisableAILiveSupport",
            EntityType = "LiveSupportAIPolicyVersion",
            EntityId = published.Id,
            PerformedByUserId = adminUserId,
            NewValues = JsonSerializer.Serialize(new { status = "RecoveryScheduled" }),
            IpAddress = "AdminAI"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<LiveSupportAIPolicyDto> EnableAsync(Guid adminUserId, long expectedVersion, CancellationToken ct)
    {
        _ = adminUserId;
        var published = await db.LiveSupportAIPolicyVersions.SingleOrDefaultAsync(x => x.Status == LiveSupportAIPolicyStatus.Published, ct);
        if (published is null)
            throw new LiveSupportAIAdminException("PUBLISHED_POLICY_NOT_FOUND", "لا توجد سياسة منشورة لتفعيلها.");
        if (published.Version != expectedVersion)
            throw new LiveSupportAIAdminException("VERSION_CONFLICT", "تغيرت السياسة المنشورة، أعد تحميل الصفحة.");
        if (published.IsEnabled)
            return ToDto(published);

        published.IsEnabled = true;
        published.Version++;
        await db.SaveChangesAsync(ct);
        return ToDto(published);
    }

    public async Task<LiveSupportAIStatsDto> GetStatsAsync(string period, CancellationToken ct)
    {
        DateTime? threshold = period switch
        {
            "last-24h" => DateTime.UtcNow.AddDays(-1),
            "last-7d" => DateTime.UtcNow.AddDays(-7),
            "last-30d" => DateTime.UtcNow.AddDays(-30),
            "all" => null,
            _ => throw new LiveSupportAIAdminException("INVALID_PERIOD", "الفترة المطلوبة غير مدعومة.")
        };

        var activeQuery = db.LiveSupportAIConversationStates.AsNoTracking();
        var resolvedQuery = db.LiveSupportAIConversationStates.AsNoTracking();
        var handoffsQuery = db.LiveSupportAIConversationStates.AsNoTracking();
        var messagesQuery = db.LiveSupportMessages.AsNoTracking();
        var actionsQuery = db.LiveSupportAIPendingActions.AsNoTracking();

        int activeConversations = await activeQuery.CountAsync(x => x.Mode == LiveSupportAIMode.AiActive, ct);

        int resolvedIssues;
        if (threshold.HasValue)
            resolvedIssues = await resolvedQuery.CountAsync(x => x.Mode == LiveSupportAIMode.AiResolved && x.ResolvedAt >= threshold.Value, ct);
        else
            resolvedIssues = await resolvedQuery.CountAsync(x => x.Mode == LiveSupportAIMode.AiResolved, ct);

        int handoffs;
        if (threshold.HasValue)
            handoffs = await handoffsQuery.CountAsync(x => x.HandedOffAt != null && x.HandedOffAt >= threshold.Value, ct);
        else
            handoffs = await handoffsQuery.CountAsync(x => x.HandedOffAt != null, ct);

        int totalMessagesSent;
        if (threshold.HasValue)
            totalMessagesSent = await messagesQuery.CountAsync(x => x.SenderType == LiveSupportSenderType.AI && x.SentAt >= threshold.Value, ct);
        else
            totalMessagesSent = await messagesQuery.CountAsync(x => x.SenderType == LiveSupportSenderType.AI, ct);

        int successfulActions;
        if (threshold.HasValue)
            successfulActions = await actionsQuery.CountAsync(x => x.Status == LiveSupportAIPendingActionStatus.Succeeded && x.CompletedAt >= threshold.Value, ct);
        else
            successfulActions = await actionsQuery.CountAsync(x => x.Status == LiveSupportAIPendingActionStatus.Succeeded, ct);

        return new LiveSupportAIStatsDto(activeConversations, resolvedIssues, handoffs, totalMessagesSent, successfulActions);
    }

    public async Task<IReadOnlyList<LiveSupportAdminConversationDto>> GetActiveConversationsAsync(CancellationToken ct)
    {
        var activeStates = await db.LiveSupportAIConversationStates
            .AsNoTracking()
            .Where(x => x.Mode == LiveSupportAIMode.AiActive)
            .OrderByDescending(x => x.LastParticipantActivityAt)
            .ToListAsync(ct);

        var conversationIds = activeStates.Select(x => x.ConversationId).ToList();

        var conversations = await db.LiveSupportConversations
            .AsNoTracking()
            .Where(c => conversationIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var userIds = conversations.Values.Where(c => c.StudentUserId.HasValue).Select(c => c.StudentUserId!.Value).Distinct().ToList();
        var userNames = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var guestSessionIds = conversations.Values.Where(c => c.GuestSessionId.HasValue).Select(c => c.GuestSessionId!.Value).Distinct().ToList();
        var guestNames = await db.LiveSupportGuestSessions.AsNoTracking().Where(g => guestSessionIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.DisplayName, ct);

        var latestTurns = await db.LiveSupportAITurns
            .AsNoTracking()
            .Where(t => conversationIds.Contains(t.ConversationId))
            .GroupBy(t => t.ConversationId)
            .Select(g => g.OrderByDescending(t => t.QueuedAt).FirstOrDefault())
            .ToDictionaryAsync(t => t!.ConversationId, ct);

        var result = new List<LiveSupportAdminConversationDto>();
        foreach (var state in activeStates)
        {
            if (!conversations.TryGetValue(state.ConversationId, out var c)) continue;
            var participantName = c.ParticipantType == LiveSupportParticipantType.Student
                ? (c.StudentUserId.HasValue && userNames.TryGetValue(c.StudentUserId.Value, out var name) ? name : "غير معروف")
                : (c.GuestSessionId.HasValue && guestNames.TryGetValue(c.GuestSessionId.Value, out var gname) ? gname : "زائر");

            latestTurns.TryGetValue(state.ConversationId, out var turn);

            result.Add(new LiveSupportAdminConversationDto(
                c.Id,
                participantName,
                c.ParticipantType,
                c.Status,
                null,
                c.CreatedAt,
                c.AssignedAt,
                c.FirstStaffResponseAt,
                c.ClosedAt,
                c.AssignedAt.HasValue ? (c.AssignedAt.Value - c.CreatedAt).TotalSeconds : null,
                c.ClosedAt.HasValue && c.AssignedAt.HasValue ? (c.ClosedAt.Value - c.AssignedAt.Value).TotalSeconds : null,
                c.Subject,
                turn?.Status.ToString(),
                turn?.FailureCode
            ));
        }

        return result;
    }

    public async Task<LiveSupportAIPreviewResultDto> PreviewAsync(LiveSupportAIPreviewRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > LiveSupportAIContractLimits.MaxMessageLength)
            throw new LiveSupportAIAdminException("PREVIEW_MESSAGE_INVALID", "رسالة المعاينة غير صالحة.");
        var policy = request.PolicyVersionId.HasValue
            ? await db.LiveSupportAIPolicyVersions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.PolicyVersionId, ct)
            : await db.LiveSupportAIPolicyVersions.AsNoTracking().Where(item => item.Status == LiveSupportAIPolicyStatus.Published).OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(ct);
        if (policy is null) throw new LiveSupportAIAdminException("POLICY_NOT_FOUND", "لا توجد سياسة للمعاينة.");
        if (previewClient is null)
            throw new LiveSupportAIAdminException("AI_PREVIEW_UNAVAILABLE", "خدمة معاينة المساعد غير متاحة.");
        var documents = await knowledge.SearchPublishedAsync(policy.Id, request.Message, LiveSupportAIContractLimits.MaxKnowledgeDocuments, LiveSupportAIContractLimits.MaxContextCharacters, ct);
        var allowedDecisionTypes = new[] { "reply", "propose_action", "request_verification", "propose_account_creation", "request_resolution", "handoff" };
        var allowedActionKeys = Parse(policy.ActionKeysJson);
        using var emptySchema = JsonDocument.Parse("{}");
        var actions = allowedActionKeys.Where(LiveSupportAICatalog.Actions.ContainsKey).Select(key =>
            new LiveSupportAIAllowedActionDto(key, LiveSupportAICatalog.Actions[key].Description, emptySchema.RootElement.Clone())).ToArray();
        var context = new LiveSupportAIWorkerClaimDto(
            "1", Guid.NewGuid(), Guid.NewGuid(), policy.Id, 0, $"preview:{Guid.NewGuid():N}", DateTime.UtcNow.AddSeconds(30),
            policy.SystemInstructions, documents, new Dictionary<string, object?> { ["previewMode"] = true },
            [new LiveSupportAIContextMessageDto("Guest", request.Message.Trim(), DateTime.UtcNow)], actions, allowedDecisionTypes);
        LiveSupportAIWorkerPreviewResultDto preview;
        try { preview = await previewClient.PreviewAsync(context, ct); }
        catch (InvalidOperationException exception) { throw new LiveSupportAIAdminException(exception.Message, "تعذر إكمال معاينة المساعد."); }
        LiveSupportAITurnOrchestrator.ValidateDecision(preview.Decision, preview.DecisionHash);
        if (preview.Decision.Type == "propose_action")
        {
            var actionKey = preview.Decision.Action!.Value.GetProperty("key").GetString();
            if (actionKey is null || !allowedActionKeys.Contains(actionKey, StringComparer.Ordinal))
                throw new LiveSupportAIAdminException("AI_PREVIEW_ACTION_NOT_ALLOWED", "اقترحت المعاينة إجراءً غير مسموح به.");
        }
        return new LiveSupportAIPreviewResultDto(policy.Id, true, documents.Count, allowedDecisionTypes,
            "DRY_RUN_DECISION_VALIDATED", preview.Decision, preview.DecisionHash, preview.Provider, preview.Model, preview.LatencyMs);
    }

    public async Task<LiveSupportAIEvidencePageDto> GetEvidenceAsync(string period, string? cursor, int pageSize, CancellationToken ct)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var threshold = period switch
        {
            "last-24h" => DateTime.UtcNow.AddDays(-1), "last-7d" => DateTime.UtcNow.AddDays(-7),
            "last-30d" => DateTime.UtcNow.AddDays(-30), "all" => DateTime.MinValue,
            _ => throw new LiveSupportAIAdminException("INVALID_PERIOD", "الفترة المطلوبة غير مدعومة.")
        };
        DateTime? before = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            try { before = new DateTime(long.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor))), DateTimeKind.Utc); }
            catch { throw new LiveSupportAIAdminException("INVALID_CURSOR", "مؤشر الصفحة غير صالح."); }
        }
        var query = db.LiveSupportAITurns.AsNoTracking().Where(item => item.QueuedAt >= threshold);
        if (before.HasValue) query = query.Where(item => item.QueuedAt < before.Value);
        var turns = await query.OrderByDescending(item => item.QueuedAt).ThenByDescending(item => item.Id).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = turns.Count > pageSize;
        if (hasMore) turns.RemoveAt(turns.Count - 1);
        var items = turns.Select(item => new LiveSupportAIEvidenceItemDto(item.Id, item.ConversationId, item.QueuedAt, item.Status.ToString(), item.DecisionType?.ToString(), item.FailureCode, item.Provider, item.Model, item.CallbackAttemptCount)).ToList();
        var next = hasMore && turns.Count > 0 ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(turns[^1].QueuedAt.Ticks.ToString())) : null;
        return new LiveSupportAIEvidencePageDto(items, next);
    }


    private static void Validate(SaveLiveSupportAIDraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SystemInstructions) || request.SystemInstructions.Length > 20_000)
            throw new LiveSupportAIAdminException("INVALID_INSTRUCTIONS", "تعليمات الـAI مطلوبة وبحد أقصى 20000 حرف.");
        ValidateKeys(request.ReadableDataKeys, LiveSupportAICatalog.ReadableData, "readable data");
        ValidateKeys(request.ActionKeys, LiveSupportAICatalog.Actions, "actions");
        ValidateKeys(request.LookupKeys, LiveSupportAICatalog.LookupKeys, "lookup");
        ValidateKeys(request.VerificationQuestionKeys, LiveSupportAICatalog.VerificationQuestions, "verification");
        if (request.VerificationRequiredCorrect < 1 || request.VerificationRequiredCorrect > request.VerificationQuestionKeys.Count)
            throw new LiveSupportAIAdminException("INVALID_VERIFICATION", "عدد إجابات التحقق المطلوبة غير صالح.");
        if (request.VerificationMaxAttempts is < 1 or > 10 || request.PendingActionExpirySeconds is < 30 or > 900 ||
            request.InactivityMinutes is < 5 or > 1440 || request.InactivityWarningGraceSeconds is < 30 or > 600)
            throw new LiveSupportAIAdminException("INVALID_LIMITS", "حدود المحاولات أو المدد غير صالحة.");
    }

    private static void ValidateKeys(IReadOnlyList<string> keys, IReadOnlyDictionary<string, LiveSupportAICatalogItemDto> catalog, string name)
    {
        if (keys.Count > 100 || keys.Distinct(StringComparer.Ordinal).Count() != keys.Count || keys.Any(key => !catalog.ContainsKey(key)))
            throw new LiveSupportAIAdminException("INVALID_CATALOG_KEY", $"توجد قيمة غير مسموحة في {name}.");
    }

    private static void Apply(LiveSupportAIPolicyVersion entity, SaveLiveSupportAIDraftRequest request)
    {
        entity.SystemInstructions = request.SystemInstructions.Trim();
        entity.ReadableDataKeysJson = JsonSerializer.Serialize(request.ReadableDataKeys);
        entity.ActionKeysJson = JsonSerializer.Serialize(request.ActionKeys);
        entity.LookupKeysJson = JsonSerializer.Serialize(request.LookupKeys);
        entity.VerificationQuestionKeysJson = JsonSerializer.Serialize(request.VerificationQuestionKeys);
        entity.VerificationRequiredCorrect = request.VerificationRequiredCorrect;
        entity.VerificationMaxAttempts = request.VerificationMaxAttempts;
        entity.PendingActionExpirySeconds = request.PendingActionExpirySeconds;
        entity.InactivityMinutes = request.InactivityMinutes;
        entity.InactivityWarningGraceSeconds = request.InactivityWarningGraceSeconds;
    }

    private static SaveLiveSupportAIDraftRequest ToRequest(LiveSupportAIPolicyVersion entity) => new(entity.SystemInstructions,
        Parse(entity.ReadableDataKeysJson), Parse(entity.ActionKeysJson), Parse(entity.LookupKeysJson), Parse(entity.VerificationQuestionKeysJson),
        entity.VerificationRequiredCorrect, entity.VerificationMaxAttempts, entity.PendingActionExpirySeconds, entity.InactivityMinutes,
        entity.InactivityWarningGraceSeconds, entity.Version);

    private static LiveSupportAIPolicyDto? ToDtoOrNull(LiveSupportAIPolicyVersion? entity) => entity is null ? null : ToDto(entity);
    private static LiveSupportAIPolicyDto ToDto(LiveSupportAIPolicyVersion entity) => new(entity.Id, entity.VersionNumber, entity.Status.ToString(), entity.IsEnabled,
        entity.SystemInstructions, Parse(entity.ReadableDataKeysJson), Parse(entity.ActionKeysJson), Parse(entity.LookupKeysJson), Parse(entity.VerificationQuestionKeysJson),
        entity.VerificationRequiredCorrect, entity.VerificationMaxAttempts, entity.PendingActionExpirySeconds, entity.InactivityMinutes,
        entity.InactivityWarningGraceSeconds, entity.Version, entity.PublishedAt);
    private static string[] Parse(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
}
