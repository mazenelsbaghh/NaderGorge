using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.LiveSupportAI.Commands;

public sealed record CancelLiveSupportAIDecisionCommand(
    LiveSupportParticipantIdentity Participant,
    Guid ConversationId,
    Guid DecisionId,
    string IdempotencyKey) : IRequest;

public sealed class CancelLiveSupportAIDecisionCommandHandler(IAppDbContext db) : IRequestHandler<CancelLiveSupportAIDecisionCommand>
{
    public async Task Handle(CancelLiveSupportAIDecisionCommand request, CancellationToken cancellationToken)
    {
        var conversation = await db.LiveSupportConversations.SingleOrDefaultAsync(item => item.Id == request.ConversationId, cancellationToken)
            ?? throw new LiveSupportException("NOT_FOUND", "المحادثة غير موجودة.");
        if ((request.Participant.StudentUserId.HasValue && request.Participant.StudentUserId != conversation.StudentUserId) ||
            (request.Participant.GuestSessionId.HasValue && request.Participant.GuestSessionId != conversation.GuestSessionId))
            throw new LiveSupportException(LiveSupportErrorCodes.Forbidden, "لا يمكنك الوصول لهذه المحادثة.");
        var decision = await db.LiveSupportAIPendingActions.SingleOrDefaultAsync(item => item.Id == request.DecisionId && item.ConversationId == request.ConversationId, cancellationToken)
            ?? throw new LiveSupportException("NOT_FOUND", "القرار غير موجود.");
        if (decision.Status == LiveSupportAIPendingActionStatus.Cancelled) return;
        if (decision.Status != LiveSupportAIPendingActionStatus.PendingConfirmation)
            throw new LiveSupportException("DECISION_NOT_CANCELLABLE", "القرار لم يعد متاحًا للإلغاء.");
        decision.Status = LiveSupportAIPendingActionStatus.Cancelled;
        decision.CancelledAt = DateTime.UtcNow;
        decision.CompletedAt = DateTime.UtcNow;
        decision.ConfirmedByUserId = request.Participant.StudentUserId;
        decision.ConfirmedByGuestSessionId = request.Participant.GuestSessionId;
        decision.Version++;
        await db.SaveChangesAsync(cancellationToken);
    }
}
