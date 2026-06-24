using System.Data;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.LiveSupportAI.Commands;

public sealed record ConfirmLiveSupportAIActionCommand(
    LiveSupportParticipantIdentity Participant,
    Guid ConversationId,
    Guid DecisionId,
    string IdempotencyKey) : IRequest<Guid>;

public sealed class ConfirmLiveSupportAIActionCommandHandler(
    IAppDbContext db,
    ILiveSupportAIDataProtector protector,
    ILiveSupportAIActionExecutor executor) : IRequestHandler<ConfirmLiveSupportAIActionCommand, Guid>
{
    public async Task<Guid> Handle(ConfirmLiveSupportAIActionCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var conversation = await db.LiveSupportConversations.SingleOrDefaultAsync(item => item.Id == request.ConversationId, cancellationToken)
            ?? throw new LiveSupportException("NOT_FOUND", "المحادثة غير موجودة.");
        Authorize(request.Participant, conversation.StudentUserId, conversation.GuestSessionId);
        var decision = await db.LiveSupportAIPendingActions.SingleOrDefaultAsync(item => item.Id == request.DecisionId && item.ConversationId == request.ConversationId, cancellationToken)
            ?? throw new LiveSupportException("NOT_FOUND", "قرار التأكيد غير موجود.");
        var idempotencyDigest = protector.ComputeKeyedDigest("participant-confirmation", Encoding.UTF8.GetBytes(request.IdempotencyKey));
        if (!string.IsNullOrEmpty(decision.ConfirmationNonceHash) && decision.ConfirmationNonceHash != idempotencyDigest)
            throw new LiveSupportException("IDEMPOTENCY_PAYLOAD_CONFLICT", "مفتاح التأكيد مستخدم لطلب مختلف.");
        if (decision.Status == LiveSupportAIPendingActionStatus.Succeeded && decision.ActionExecutionId.HasValue)
            return decision.ActionExecutionId.Value;
        if (decision.Status != LiveSupportAIPendingActionStatus.PendingConfirmation)
            throw new LiveSupportException("DECISION_NOT_CONFIRMABLE", "القرار لم يعد متاحًا للتأكيد.");
        if (decision.DecisionKind != LiveSupportAIPendingDecisionKind.Action || !decision.StudentUserId.HasValue || decision.StudentUserId != conversation.LinkedStudentUserId)
            throw new LiveSupportException("ACTION_TARGET_MISMATCH", "هدف الإجراء لم يعد مطابقًا.");
        if (decision.StateFingerprint != $"{conversation.Id:N}:{conversation.Version}")
        {
            decision.Status = LiveSupportAIPendingActionStatus.Invalidated;
            decision.CompletedAt = DateTime.UtcNow;
            decision.Version++;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new LiveSupportException("ACTION_STATE_CHANGED", "تغيرت حالة المحادثة منذ إنشاء الإجراء.");
        }
        if (decision.ExpiresAt <= DateTime.UtcNow)
        {
            decision.Status = LiveSupportAIPendingActionStatus.Expired;
            decision.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new LiveSupportException("CONFIRMATION_EXPIRED", "انتهت صلاحية التأكيد.");
        }
        var policy = await db.LiveSupportAIPolicyVersions.SingleOrDefaultAsync(item => item.Id == decision.PolicyVersionId, cancellationToken);
        var allowed = policy is null ? [] : JsonSerializer.Deserialize<string[]>(policy.ActionKeysJson) ?? [];
        if (policy is null || !policy.IsEnabled || !allowed.Contains(decision.ActionKey, StringComparer.Ordinal))
            throw new LiveSupportException("ACTION_REVOKED", "الإجراء لم يعد مسموحًا.");
        decision.ConfirmationNonceHash = idempotencyDigest;
        var plaintext = protector.Unprotect(decision.EncryptedPayload ?? throw new LiveSupportException("ACTION_PAYLOAD_MISSING", "بيانات الإجراء غير متاحة."));
        if (protector.ComputeKeyedDigest("pending-decision", plaintext) != decision.PayloadHash)
            throw new LiveSupportException("ACTION_PAYLOAD_INVALID", "تعذر التحقق من بيانات الإجراء.");
        using var payloadDocument = JsonDocument.Parse(plaintext);
        var arguments = payloadDocument.RootElement.GetProperty("arguments").Deserialize<Dictionary<string, object?>>() ?? [];
        decision.Status = LiveSupportAIPendingActionStatus.Executing;
        decision.ConfirmedAt = DateTime.UtcNow;
        decision.ConfirmedByUserId = request.Participant.StudentUserId;
        decision.ConfirmedByGuestSessionId = request.Participant.GuestSessionId;
        decision.Version++;
        await db.SaveChangesAsync(cancellationToken);
        var executionId = await executor.ExecuteAsync(conversation.Id, decision.StudentUserId.Value, decision.Id, decision.ActionKey, arguments, request.IdempotencyKey, cancellationToken);
        decision.ActionExecutionId = executionId;
        decision.Status = LiveSupportAIPendingActionStatus.Succeeded;
        decision.CompletedAt = DateTime.UtcNow;
        decision.Version++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return executionId;
    }

    private static void Authorize(LiveSupportParticipantIdentity participant, Guid? studentId, Guid? guestId)
    {
        if ((participant.StudentUserId.HasValue && participant.StudentUserId != studentId) ||
            (participant.GuestSessionId.HasValue && participant.GuestSessionId != guestId) ||
            (!participant.StudentUserId.HasValue && !participant.GuestSessionId.HasValue))
            throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "لا يمكنك الوصول لهذه المحادثة.");
    }
}
