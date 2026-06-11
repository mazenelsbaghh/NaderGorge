using MediatR;
using Microsoft.EntityFrameworkCore;
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

        if (request.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
        {
            if (Guid.TryParse(request.JobId, out var videoId))
            {
                var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == videoId, ct);
                if (video != null)
                {
                    video.IsProcessingAI = false;
                    video.UpdatedAt = DateTime.UtcNow;
                    _db.LessonVideos.Update(video);
                }
            }

            var failedEvent = new OutboxEvent
            {
                Type = "AiJobFailed",
                TargetGroup = "Role_Admin",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    jobId = request.JobId,
                    error = request.Message
                })
            };
            _db.OutboxEvents.Add(failedEvent);
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
