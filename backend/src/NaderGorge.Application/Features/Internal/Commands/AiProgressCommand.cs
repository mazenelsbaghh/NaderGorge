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

        string? teacherUserId = null;
        Guid? parsedVideoId = null;
        if (Guid.TryParse(request.JobId, out var videoId))
        {
            parsedVideoId = videoId;
            teacherUserId = await _db.LessonVideos
                .Where(v => v.Id == videoId)
                .Select(v => (string?)v.Lesson.ContentSection.Term.Package.Teacher.UserId.ToString())
                .FirstOrDefaultAsync(ct);
        }

        var adminEvent = new OutboxEvent
        {
            Type = "AiJobProgress",
            TargetGroup = "Role_Admin",
            PayloadJson = payloadJson
        };
        _db.OutboxEvents.Add(adminEvent);

        if (teacherUserId != null)
        {
            var teacherEvent = new OutboxEvent
            {
                Type = "AiJobProgress",
                TargetUserId = teacherUserId,
                PayloadJson = payloadJson
            };
            _db.OutboxEvents.Add(teacherEvent);
        }

        if (request.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
        {
            if (parsedVideoId.HasValue)
            {
                var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == parsedVideoId.Value, ct);
                if (video != null)
                {
                    video.IsProcessingAI = false;
                    video.UpdatedAt = DateTime.UtcNow;
                    _db.LessonVideos.Update(video);

                    var videoFailedEvent = new OutboxEvent
                    {
                        Type = "VideoFailed",
                        TargetGroup = $"Lesson_{video.LessonId}",
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            lessonId = video.LessonId,
                            videoId = video.Id,
                            error = request.Message
                        })
                    };
                    _db.OutboxEvents.Add(videoFailedEvent);
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

            if (teacherUserId != null)
            {
                var teacherFailedEvent = new OutboxEvent
                {
                    Type = "AiJobFailed",
                    TargetUserId = teacherUserId,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        jobId = request.JobId,
                        error = request.Message
                    })
                };
                _db.OutboxEvents.Add(teacherFailedEvent);
            }
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
