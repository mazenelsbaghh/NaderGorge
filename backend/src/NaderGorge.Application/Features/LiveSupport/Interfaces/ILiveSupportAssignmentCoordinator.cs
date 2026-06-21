using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public interface ILiveSupportAssignmentCoordinator
{
    Task<LiveSupportConversationDto> TransferAsync(Guid actorUserId, bool isAdmin, Guid conversationId, Guid? targetStaffUserId, string reason, CancellationToken ct);
    Task ReleaseStaffAssignmentsAsync(Guid staffUserId, LiveSupportAssignmentEndReason reason, CancellationToken ct);
}
