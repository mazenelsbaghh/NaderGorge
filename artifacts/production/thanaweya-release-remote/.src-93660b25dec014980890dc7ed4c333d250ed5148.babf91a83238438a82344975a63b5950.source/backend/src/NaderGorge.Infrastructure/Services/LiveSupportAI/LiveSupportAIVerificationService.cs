using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.LiveSupportAI;

public sealed class LiveSupportAIVerificationService(
    IAppDbContext db,
    ILiveSupportAIDataProtector protector,
    IConfiguration configuration) : ILiveSupportAIVerificationService
{
    public async Task<LiveSupportAIVerificationStateDto> StartLookupAsync(LiveSupportParticipantIdentity participant, Guid conversationId, LiveSupportAIVerificationLookupCommandDto request, CancellationToken cancellationToken)
    {
        var conversation = await RequireParticipantAsync(participant, conversationId, cancellationToken);
        var state = await db.LiveSupportAIConversationStates.SingleOrDefaultAsync(item => item.ConversationId == conversationId, cancellationToken)
            ?? throw new LiveSupportException("AI_STATE_NOT_FOUND", "تعذر بدء التحقق.");
        var policy = await db.LiveSupportAIPolicyVersions.SingleOrDefaultAsync(item => item.Id == state.PolicyVersionId && item.IsEnabled, cancellationToken)
            ?? throw new LiveSupportException("AI_POLICY_DISABLED", "تعذر بدء التحقق.");
        var now = DateTime.UtcNow;
        foreach (var active in await db.LiveSupportAIVerificationSessions.Where(item => item.ConversationId == conversationId && (item.Status == LiveSupportAIVerificationStatus.AwaitingLookup || item.Status == LiveSupportAIVerificationStatus.Challenging)).ToListAsync(cancellationToken))
        {
            active.Status = LiveSupportAIVerificationStatus.Cancelled;
            active.CompletedAt = now;
            active.Version++;
        }

        var normalizedLookup = NormalizeLookup(request.LookupKey, request.Value);
        var candidates = request.LookupKey switch
        {
            "phone.full" => await db.Users.AsNoTracking().Where(item => item.IsActive && item.PhoneNumber == normalizedLookup).Select(item => item.Id).Take(2).ToListAsync(cancellationToken),
            "student_code.full" => await db.StudentProfiles.AsNoTracking().Where(item => item.StudentCode == normalizedLookup && item.User.IsActive).Select(item => item.UserId).Take(2).ToListAsync(cancellationToken),
            _ => throw new LiveSupportException("LOOKUP_KEY_INVALID", "بيانات البحث غير صالحة.")
        };
        var candidateId = candidates.Count == 1 ? candidates[0] : (Guid?)null;
        var questions = await db.LiveSupportAIVerificationPolicyQuestions.AsNoTracking().Where(item => item.PolicyVersionId == policy.Id)
            .OrderBy(item => item.Order).Select(item => new { item.QuestionKey, item.PromptText }).Take(5).ToListAsync(cancellationToken);
        var keys = questions.Select(item => item.QuestionKey).ToArray();
        if (keys.Length == 0) keys = ["profile.governorate"];
        var session = new LiveSupportAIVerificationSession
        {
            ConversationId = conversation.Id,
            PolicyVersionId = policy.Id,
            CandidateStudentUserId = candidateId,
            LookupKey = request.LookupKey,
            LookupValueHash = protector.ComputeKeyedDigest("verification-lookup", System.Text.Encoding.UTF8.GetBytes(normalizedLookup)),
            SelectedQuestionKeysJson = JsonSerializer.Serialize(keys),
            RequiredCorrect = Math.Min(Math.Max(policy.VerificationRequiredCorrect, 1), keys.Length),
            MaxAttempts = Math.Clamp(policy.VerificationMaxAttempts, 1, 10),
            Status = LiveSupportAIVerificationStatus.Challenging,
            ExpiresAt = now.AddMinutes(5),
            Version = 1
        };
        db.LiveSupportAIVerificationSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return new LiveSupportAIVerificationStateDto(session.Id, session.Status, questions.FirstOrDefault()?.PromptText ?? "ما هي المحافظة المسجلة بحسابك؟", 0, session.MaxAttempts);
    }

    public async Task<LiveSupportAIVerificationStateDto> SubmitAnswerAsync(LiveSupportParticipantIdentity participant, Guid conversationId, LiveSupportAIVerificationAnswerCommandDto request, CancellationToken cancellationToken)
    {
        var conversation = await RequireParticipantAsync(participant, conversationId, cancellationToken);
        var session = await db.LiveSupportAIVerificationSessions.SingleOrDefaultAsync(item => item.Id == request.SessionId && item.ConversationId == conversationId, cancellationToken)
            ?? throw new LiveSupportException("VERIFICATION_NOT_FOUND", "جلسة التحقق غير متاحة.");
        if (session.Status != LiveSupportAIVerificationStatus.Challenging)
            return ToState(session, null);
        var now = DateTime.UtcNow;
        if (session.ExpiresAt <= now)
        {
            session.Status = LiveSupportAIVerificationStatus.Failed;
            session.LockedAt = now;
            session.CompletedAt = now;
            session.Version++;
            await db.SaveChangesAsync(cancellationToken);
            throw new LiveSupportException("VERIFICATION_EXPIRED", "انتهت جلسة التحقق.");
        }
        var keys = JsonSerializer.Deserialize<string[]>(session.SelectedQuestionKeysJson) ?? [];
        if (session.CurrentQuestionIndex >= keys.Length) throw new LiveSupportException("VERIFICATION_STATE_INVALID", "تعذر إكمال التحقق.");
        var questionKey = keys[session.CurrentQuestionIndex];
        var expected = session.CandidateStudentUserId.HasValue
            ? await ExpectedAnswerAsync(session.CandidateStudentUserId.Value, questionKey, cancellationToken)
            : null;
        var correct = expected is not null && FixedNormalizedEquals(expected, request.Answer);
        session.AttemptCount++;
        session.LastAttemptAt = now;
        if (correct) { session.CorrectCount++; session.CurrentQuestionIndex++; }
        db.LiveSupportAIVerificationAttempts.Add(new LiveSupportAIVerificationAttempt
        {
            SessionId = session.Id,
            QuestionKeysJson = JsonSerializer.Serialize(new[] { questionKey }),
            OutcomeCodesJson = JsonSerializer.Serialize(new[] { correct ? "Correct" : "Incorrect" }),
            SubmittedAt = now,
            AttemptNumber = session.AttemptCount
        });

        if (session.CorrectCount >= session.RequiredCorrect && session.CandidateStudentUserId.HasValue)
        {
            var systemActorId = await ResolveSystemActorAsync(cancellationToken);
            session.Status = LiveSupportAIVerificationStatus.Verified;
            session.VerifiedAt = now;
            session.CompletedAt = now;
            conversation.LinkedStudentUserId = session.CandidateStudentUserId;
            conversation.Version++;
            var state = await db.LiveSupportAIConversationStates.SingleAsync(item => item.ConversationId == conversationId, cancellationToken);
            state.VerifiedStudentUserId = session.CandidateStudentUserId;
            state.Version++;
            db.LiveSupportStudentLinkHistories.Add(new LiveSupportStudentLinkHistory { ConversationId = conversationId, PreviousStudentUserId = null, NewStudentUserId = session.CandidateStudentUserId, ChangedByUserId = systemActorId, Reason = "AI identity verification", ChangedAt = now });
        }
        else if (session.AttemptCount >= session.MaxAttempts)
        {
            session.Status = LiveSupportAIVerificationStatus.Exhausted;
            session.LockedAt = now;
            session.CompletedAt = now;
            await QueueHumanAsync(conversation, "VERIFICATION_EXHAUSTED", cancellationToken);
        }
        session.Version++;
        await db.SaveChangesAsync(cancellationToken);
        var nextPrompt = session.Status == LiveSupportAIVerificationStatus.Challenging ? "أدخل الإجابة المسجلة بالضبط." : null;
        return ToState(session, nextPrompt);
    }

    private async Task<LiveSupportConversation> RequireParticipantAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await db.LiveSupportConversations.SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw new LiveSupportException("NOT_FOUND", "المحادثة غير موجودة.");
        if ((participant.StudentUserId.HasValue && participant.StudentUserId != conversation.StudentUserId) ||
            (participant.GuestSessionId.HasValue && participant.GuestSessionId != conversation.GuestSessionId))
            throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "لا يمكنك الوصول لهذه المحادثة.");
        return conversation;
    }

    private async Task<string?> ExpectedAnswerAsync(Guid userId, string key, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        var profile = await db.StudentProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return key switch
        {
            "profile.full_name" => user?.FullName,
            "profile.birth_date" when profile is not null => profile.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "profile.governorate" => profile?.Governorate,
            "profile.school_name" => profile?.SchoolName,
            "contact.parent_phone_last4" when profile?.ParentPhone?.Length >= 4 => profile.ParentPhone[^4..],
            _ => null
        };
    }

    private async Task QueueHumanAsync(LiveSupportConversation conversation, string reason, CancellationToken cancellationToken)
    {
        var state = await db.LiveSupportAIConversationStates.SingleAsync(item => item.ConversationId == conversation.Id, cancellationToken);
        state.Mode = LiveSupportAIMode.HumanQueued;
        state.HandoffReasonCode = reason;
        state.HandoffSafeSummary = "تعذر إكمال التحقق وتم التحويل للدعم البشري.";
        state.HandedOffAt = DateTime.UtcNow;
        state.Version++;
        if (!await db.LiveSupportQueueEntries.AnyAsync(item => item.ConversationId == conversation.Id && item.DequeuedAt == null, cancellationToken))
            db.LiveSupportQueueEntries.Add(new LiveSupportQueueEntry { ConversationId = conversation.Id, EnteredAt = DateTime.UtcNow, Sequence = DateTime.UtcNow.Ticks });
        conversation.Status = LiveSupportConversationStatus.Waiting;
        conversation.QueuedAt ??= DateTime.UtcNow;
        conversation.Version++;
    }

    private async Task<Guid> ResolveSystemActorAsync(CancellationToken cancellationToken)
    {
        if (Guid.TryParse(configuration["LiveSupportAI:SystemActorUserId"], out var configured) && await db.Users.AnyAsync(item => item.Id == configured && item.IsActive, cancellationToken)) return configured;
        var id = await db.UserRoles.Where(item => item.Role.Type == RoleType.Admin && item.User.IsActive).Select(item => item.UserId).OrderBy(item => item).FirstOrDefaultAsync(cancellationToken);
        return id == Guid.Empty ? throw new InvalidOperationException("AI_SYSTEM_ACTOR_NOT_CONFIGURED") : id;
    }

    private static string NormalizeLookup(string key, string value) => key == "phone.full"
        ? new string(value.Where(char.IsAsciiDigit).ToArray())
        : value.Trim().ToUpperInvariant();
    private static bool FixedNormalizedEquals(string expected, string actual) =>
        string.Equals(expected.Trim().Normalize().ToUpperInvariant(), actual.Trim().Normalize().ToUpperInvariant(), StringComparison.Ordinal);
    private static LiveSupportAIVerificationStateDto ToState(LiveSupportAIVerificationSession session, string? prompt) =>
        new(session.Id, session.Status, prompt, session.AttemptCount, session.MaxAttempts);
}
