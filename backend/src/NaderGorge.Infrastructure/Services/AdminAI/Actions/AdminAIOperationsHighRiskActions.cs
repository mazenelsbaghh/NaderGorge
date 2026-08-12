using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Application.Features.LiveSupport.Commands;
using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIArchiveChatRoomInput(Guid RoomId, bool IsArchived);
public sealed record AdminAIInterveneLiveSupportInput(Guid ConversationId, string Operation, Guid? TargetStaffUserId, string Reason);

public sealed class AdminAIArchiveChatRoomAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIArchiveChatRoomInput, ApiResponse>(mediator, preview)
{
    public override string Key => "admin.operations.internal-chat.archive";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIArchiveChatRoomInput input, Guid actorId, string operationId) => new ArchiveChatRoomCommand(input.RoomId, actorId, input.IsArchived);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse response) => IdentityOutcome.From(response, ["internal-chat"]);
}

public sealed class AdminAIInterveneLiveSupportAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIInterveneLiveSupportInput, LiveSupportConversationDto>(mediator, preview)
{
    public override string Key => "admin.operations.live-support.intervene";
    protected override IRequest<LiveSupportConversationDto> CreateCommand(AdminAIInterveneLiveSupportInput input, Guid actorId, string operationId) =>
        new AdminInterveneLiveSupportConversationCommand(actorId, input.ConversationId, input.Operation, input.TargetStaffUserId, input.Reason);
    protected override AdminAIActionOutcome ToOutcome(LiveSupportConversationDto response) =>
        AdminAIActionOutcomeFactory.Success(new { conversationId = response.Id, status = response.Status.ToString() }, 1, ["live-support"]);
}
