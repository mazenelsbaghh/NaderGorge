using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.LiveSupportAI;

public sealed partial class LiveSupportAIContextBuilder(
    IAppDbContext db,
    ILiveSupportAIKnowledgeService knowledgeService) : ILiveSupportAIContextBuilder
{
    private static readonly string[] DecisionTypes =
    [
        "reply", "propose_action", "request_verification",
        "propose_account_creation", "request_resolution", "handoff"
    ];

    public async Task<LiveSupportAIWorkerClaimDto> BuildAsync(Guid turnId, CancellationToken cancellationToken)
    {
        var turn = await db.LiveSupportAITurns.AsNoTracking().SingleAsync(item => item.Id == turnId, cancellationToken);
        var conversation = await db.LiveSupportConversations.AsNoTracking()
            .SingleAsync(item => item.Id == turn.ConversationId, cancellationToken);
        var policy = await db.LiveSupportAIPolicyVersions.AsNoTracking()
            .SingleAsync(item => item.Id == turn.PolicyVersionId, cancellationToken);
        var aiStateIsActive = await db.LiveSupportAIConversationStates.AsNoTracking()
            .AnyAsync(item =>
                item.ConversationId == conversation.Id &&
                item.PolicyVersionId == turn.PolicyVersionId &&
                item.Mode == LiveSupportAIMode.AiActive,
                cancellationToken);
        if (!conversation.AllowsAI ||
            turn.Status != LiveSupportAITurnStatus.Processing ||
            policy.Status != LiveSupportAIPolicyStatus.Published ||
            !policy.IsEnabled ||
            !aiStateIsActive)
            throw new InvalidOperationException("AI context is not available for this conversation.");

        var sourceMessage = await db.LiveSupportMessages.AsNoTracking()
            .SingleAsync(item => item.Id == turn.SourceMessageId, cancellationToken);
        var knowledge = await knowledgeService.SearchPublishedAsync(
            policy.Id,
            sourceMessage.Content,
            LiveSupportAIContractLimits.MaxKnowledgeDocuments,
            LiveSupportAIContractLimits.MaxContextCharacters / 2,
            cancellationToken);

        var transcript = await db.LiveSupportMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversation.Id)
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Take(LiveSupportAIContractLimits.MaxTranscriptMessages)
            .Select(message => new LiveSupportAIContextMessageDto(
                message.SenderType.ToString(),
                message.Content,
                message.SentAt))
            .ToListAsync(cancellationToken);
        transcript.Reverse();
        transcript = transcript
            .Select(message => message with { Content = RedactForProvider(message.Content) })
            .ToList();

        var readableKeys = DeserializeKeys(policy.ReadableDataKeysJson);
        var studentContext = await BuildStudentContextAsync(conversation.LinkedStudentUserId, readableKeys, cancellationToken);
        var actionKeys = DeserializeKeys(policy.ActionKeysJson);
        var actions = actionKeys
            .Where(LiveSupportAICatalog.Actions.ContainsKey)
            .Select(key => new LiveSupportAIAllowedActionDto(
                key,
                LiveSupportAICatalog.Actions[key].Description,
                LiveSupportAICatalog.GetArgumentsSchema(key)))
            .ToArray();

        return new LiveSupportAIWorkerClaimDto(
            "1",
            turn.Id,
            conversation.Id,
            policy.Id,
            turn.ExpectedConversationVersion,
            turn.Id.ToString("N"),
            DateTime.UtcNow.AddSeconds(10),
            policy.SystemInstructions[..Math.Min(policy.SystemInstructions.Length, 20_000)],
            knowledge,
            studentContext,
            transcript,
            actions,
            DecisionTypes);
    }

    private async Task<IReadOnlyDictionary<string, object?>> BuildStudentContextAsync(
        Guid? studentUserId,
        IReadOnlySet<string> readableKeys,
        CancellationToken cancellationToken)
    {
        var context = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!studentUserId.HasValue) return context;

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == studentUserId, cancellationToken);
        if (user is null) return context;
        var linkedStudentUserId = studentUserId.Value;

        if (readableKeys.Contains("identity.basic"))
            context["identity.basic"] = new { user.Id, user.FullName };
        if (readableKeys.Contains("identity.contact"))
            context["identity.contact"] = new { user.PhoneNumber };
        if (readableKeys.Contains("account.status"))
            context["account.status"] = new { user.IsActive, user.IsProfileComplete, user.SuspensionReason };

        var profile = await db.StudentProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == studentUserId, cancellationToken);
        if (readableKeys.Contains("education.profile"))
        {
            context["education.profile"] = profile is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>
            {
                ["StudentCode"] = profile.StudentCode,
                ["Governorate"] = profile.Governorate,
                ["SchoolName"] = profile.SchoolName,
                ["EducationStage"] = profile.EducationStage.ToString(),
                ["GradeLevel"] = profile.GradeLevel.ToString()
            };
        }

        if (readableKeys.Contains("devices.summary"))
            context["devices.summary"] = new { ActiveCount = await db.Devices.CountAsync(item => item.UserId == studentUserId && item.IsActive, cancellationToken) };
        if (readableKeys.Contains("access.grants"))
            context["access.grants"] = new { ActiveCount = await db.StudentAccessGrants.CountAsync(item => item.UserId == studentUserId && item.IsActive, cancellationToken) };
        if (readableKeys.Contains("packages.active"))
            context["packages.active"] = await BuildActivePackagesContextAsync(linkedStudentUserId, cancellationToken);
        if (readableKeys.Contains("balance.summary"))
            context["balance.summary"] = await BuildBalanceContextAsync(linkedStudentUserId, cancellationToken);
        if (readableKeys.Contains("watch.summary"))
            context["watch.summary"] = new { EventCount = await db.VideoWatchEvents.CountAsync(item => item.UserId == studentUserId, cancellationToken) };
        if (readableKeys.Contains("exams.summary"))
            context["exams.summary"] = new { AttemptCount = await db.StudentExamAttempts.CountAsync(item => item.UserId == studentUserId, cancellationToken) };
        if (readableKeys.Contains("homework.summary"))
            context["homework.summary"] = new { SubmissionCount = await db.HomeworkSubmissions.CountAsync(item => item.StudentId == studentUserId, cancellationToken) };
        if (readableKeys.Contains("requests.summary"))
            context["requests.summary"] = await BuildRequestsContextAsync(linkedStudentUserId, cancellationToken);
        if (readableKeys.Contains("gamification.summary"))
            context["gamification.summary"] = await BuildGamificationContextAsync(linkedStudentUserId, cancellationToken);
        if (readableKeys.Contains("notes.safe"))
            context["notes.safe"] = await BuildNotesContextAsync(linkedStudentUserId, cancellationToken);
        if (readableKeys.Contains("crm.safe"))
            context["crm.safe"] = await BuildCrmContextAsync(linkedStudentUserId, cancellationToken);
        if (readableKeys.Contains("audit.safe_recent"))
            context["audit.safe_recent"] = await BuildAuditContextAsync(linkedStudentUserId, cancellationToken);

        return context;
    }

    private async Task<object> BuildActivePackagesContextAsync(Guid studentUserId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var grants = await db.StudentAccessGrants.AsNoTracking()
            .Where(item => item.UserId == studentUserId && item.PackageId.HasValue && item.IsActive && (item.ExpiresAt == null || item.ExpiresAt > now))
            .OrderByDescending(item => item.GrantedAt)
            .Take(10)
            .Select(item => new { item.PackageId, item.ExpiresAt })
            .ToListAsync(cancellationToken);
        return new { ActiveCount = grants.Count, PackageIds = grants.Select(item => item.PackageId).ToArray(), EarliestExpiry = grants.Where(item => item.ExpiresAt.HasValue).Select(item => item.ExpiresAt).Min() };
    }

    private async Task<object> BuildBalanceContextAsync(Guid studentUserId, CancellationToken cancellationToken)
    {
        var balance = await db.StudentBalances.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == studentUserId, cancellationToken);
        return new { CurrentBalance = balance?.CurrentBalance ?? 0m, Version = balance?.Version ?? 0 };
    }

    private async Task<object> BuildRequestsContextAsync(Guid studentUserId, CancellationToken cancellationToken)
    {
        var requests = await db.ExtraWatchRequests.AsNoTracking()
            .Where(item => item.UserId == studentUserId)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key.ToString(), Count = group.Count() })
            .ToListAsync(cancellationToken);
        return new { TotalCount = requests.Sum(item => item.Count), ByStatus = requests.OrderBy(item => item.Status).ToArray() };
    }

    private async Task<object> BuildGamificationContextAsync(Guid studentUserId, CancellationToken cancellationToken)
    {
        var gamification = await db.StudentGamifications.AsNoTracking().SingleOrDefaultAsync(item => item.StudentId == studentUserId, cancellationToken);
        return new { TotalPoints = gamification?.TotalPoints ?? 0, LevelName = RedactForProvider(gamification?.LevelName ?? "Novice"), CurrentStreakCount = gamification?.CurrentStreakCount ?? 0 };
    }

    private async Task<object> BuildNotesContextAsync(Guid studentUserId, CancellationToken cancellationToken)
    {
        var notes = await db.StudentNotes.AsNoTracking()
            .Where(item => item.StudentId == studentUserId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(5)
            .Select(item => new { item.Content, item.IsPinned, item.CreatedAt })
            .ToListAsync(cancellationToken);
        return new { Notes = notes.Select(item => new { Content = RedactForProvider(item.Content[..Math.Min(item.Content.Length, 500)]), item.IsPinned, item.CreatedAt }).ToArray() };
    }

    private async Task<object> BuildCrmContextAsync(Guid studentUserId, CancellationToken cancellationToken)
    {
        var status = await db.CrmStudentStatuses.AsNoTracking().SingleOrDefaultAsync(item => item.StudentId == studentUserId, cancellationToken);
        var calls = await db.CrmCallLogs.AsNoTracking().CountAsync(item => item.StudentId == studentUserId, cancellationToken);
        return new { Status = status?.Status.ToString() ?? "Unassigned", Priority = status?.Priority.ToString() ?? "Medium", status?.NextFollowUpDate, CallCount = calls };
    }

    private async Task<object> BuildAuditContextAsync(Guid studentUserId, CancellationToken cancellationToken)
    {
        var audit = await db.AuditLogs.AsNoTracking()
            .Where(item => item.EntityId == studentUserId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(10)
            .Select(item => new { item.Action, item.EntityType, item.CreatedAt })
            .ToListAsync(cancellationToken);
        return new { Items = audit.Select(item => new { Action = RedactForProvider(item.Action[..Math.Min(item.Action.Length, 120)]), EntityType = RedactForProvider(item.EntityType[..Math.Min(item.EntityType.Length, 120)]), item.CreatedAt }).ToArray() };
    }

    private static HashSet<string> DeserializeKeys(string json) =>
        JsonSerializer.Deserialize<string[]>(json)?.ToHashSet(StringComparer.Ordinal) ?? [];

    internal static string RedactForProvider(string content)
    {
        if (SensitiveLabelRegex().IsMatch(content)) return "[REDACTED_SENSITIVE_CONTENT]";
        return LongDigitRegex().Replace(content, "[REDACTED_NUMBER]");
    }

    [GeneratedRegex("password|passcode|token|secret|كلمة\\s*المرور|رمز\\s*الدخول", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveLabelRegex();

    [GeneratedRegex(@"(?<!\d)\d{10,}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex LongDigitRegex();
}
