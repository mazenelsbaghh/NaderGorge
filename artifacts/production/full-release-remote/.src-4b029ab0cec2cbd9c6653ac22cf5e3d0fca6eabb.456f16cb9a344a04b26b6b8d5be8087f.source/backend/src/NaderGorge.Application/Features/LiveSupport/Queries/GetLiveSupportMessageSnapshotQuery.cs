using MediatR;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.Application.Features.LiveSupport.Queries;

public sealed record GetLiveSupportMessageSnapshotQuery(LiveSupportParticipantIdentity Participant, Guid ConversationId, int PageSize, string? Cursor, long? AfterSequence) : IRequest<LiveSupportMessagePageDto>;

public sealed class GetLiveSupportMessageSnapshotQueryHandler(ILiveSupportService service) : IRequestHandler<GetLiveSupportMessageSnapshotQuery, LiveSupportMessagePageDto>
{
    public Task<LiveSupportMessagePageDto> Handle(GetLiveSupportMessageSnapshotQuery request, CancellationToken ct) =>
        service.GetParticipantMessagePageAsync(request.Participant, request.ConversationId, request.PageSize, request.Cursor, request.AfterSequence, ct);
}
