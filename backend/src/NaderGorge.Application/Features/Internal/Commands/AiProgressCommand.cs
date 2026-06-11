using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record AiProgressCommand(string JobId, int Progress, string Status, string Message) : IRequest<ApiResponse>;

public class AiProgressCommandHandler : IRequestHandler<AiProgressCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public AiProgressCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(AiProgressCommand request, CancellationToken ct)
    {
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            jobId = request.JobId,
            progress = request.Progress,
            status = request.Status,
            message = request.Message
        });

        var adminEvent = new OutboxEvent
        {
            Type = "AiJobProgress",
            TargetGroup = "Role_Admin",
            PayloadJson = payloadJson
        };
        _db.OutboxEvents.Add(adminEvent);

        var teacherEvent = new OutboxEvent
        {
            Type = "AiJobProgress",
            TargetGroup = "Role_Teacher",
            PayloadJson = payloadJson
        };
        _db.OutboxEvents.Add(teacherEvent);

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
