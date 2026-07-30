using MediatR;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.Application.Features.LiveSupport.Commands;

public sealed record AdminInterveneLiveSupportConversationCommand(Guid AdminUserId, Guid ConversationId, string Operation, Guid? TargetStaffUserId, string Reason) : IRequest<LiveSupportConversationDto>;

public sealed class AdminInterveneLiveSupportConversationCommandHandler(ILiveSupportService service) : IRequestHandler<AdminInterveneLiveSupportConversationCommand, LiveSupportConversationDto>
{
    public Task<LiveSupportConversationDto> Handle(AdminInterveneLiveSupportConversationCommand request, CancellationToken ct) =>
        service.AdminInterveneAsync(request.AdminUserId, request.ConversationId, request.Operation, request.TargetStaffUserId, request.Reason, ct);
}
