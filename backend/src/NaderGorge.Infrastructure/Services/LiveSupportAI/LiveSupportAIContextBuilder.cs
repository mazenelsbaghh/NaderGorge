using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Services;
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
                JsonDocument.Parse("{}").RootElement.Clone()))
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

        if (readableKeys.Contains("identity.basic"))
            context["identity.basic"] = new { user.Id, user.FullName };
        if (readableKeys.Contains("identity.contact"))
            context["identity.contact"] = new { user.PhoneNumber };
        if (readableKeys.Contains("account.status"))
            context["account.status"] = new { user.IsActive, user.IsProfileComplete, user.SuspensionReason };

        var profile = await db.StudentProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == studentUserId, cancellationToken);
        if (profile is not null && readableKeys.Contains("education.profile"))
        {
            context["education.profile"] = new
            {
                profile.StudentCode,
                profile.Governorate,
                profile.SchoolName,
                EducationStage = profile.EducationStage.ToString(),
                GradeLevel = profile.GradeLevel.ToString()
            };
        }

        if (readableKeys.Contains("devices.summary"))
            context["devices.summary"] = new { ActiveCount = await db.Devices.CountAsync(item => item.UserId == studentUserId && item.IsActive, cancellationToken) };
        if (readableKeys.Contains("access.grants"))
            context["access.grants"] = new { ActiveCount = await db.StudentAccessGrants.CountAsync(item => item.UserId == studentUserId && item.IsActive, cancellationToken) };
        if (readableKeys.Contains("watch.summary"))
            context["watch.summary"] = new { EventCount = await db.VideoWatchEvents.CountAsync(item => item.UserId == studentUserId, cancellationToken) };
        if (readableKeys.Contains("exams.summary"))
            context["exams.summary"] = new { AttemptCount = await db.StudentExamAttempts.CountAsync(item => item.UserId == studentUserId, cancellationToken) };
        if (readableKeys.Contains("homework.summary"))
            context["homework.summary"] = new { SubmissionCount = await db.HomeworkSubmissions.CountAsync(item => item.StudentId == studentUserId, cancellationToken) };

        return context;
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
