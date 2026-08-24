using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Internal.Commands;

public record AiProgressCommand(string JobId, int Progress, string Status, string Message) : IRequest<ApiResponse>;

public sealed record AiProgressPublicUpdate(
    string JobId,
    int Progress,
    string Status,
    string Message,
    string? FailureCode,
    bool Retryable);

/// <summary>
/// Converts the internal worker callback into a public staff-facing contract.
/// Worker messages are diagnostic input and must never be copied into SignalR events.
/// </summary>
public static class AiProgressPublicContract
{
    public const string AnalysisFailureMessage =
        "تعذر إكمال تحليل الفيديو. تحقّق من رابط الفيديو وصلاحية الوصول، ثم أعد المحاولة.";

    public const string MindmapFailureMessage =
        "تعذر إكمال توليد الخرائط الذهنية. أعد المحاولة بعد قليل.";

    public static AiProgressPublicUpdate Create(string jobId, int progress, string? status)
    {
        var publicStatus = PublicStatus(progress, status);
        var publicProgress = publicStatus == "failed" ? 0 : Math.Clamp(progress, 0, 100);
        var isMindmap = jobId.Contains("_mindmap", StringComparison.OrdinalIgnoreCase);

        if (publicStatus == "failed")
        {
            return FailedUpdate(jobId, isMindmap);
        }

        return new AiProgressPublicUpdate(
            jobId,
            publicProgress,
            publicStatus,
            ProgressMessage(isMindmap, publicProgress, publicStatus),
            FailureCode: null,
            Retryable: false);
    }

    private static string PublicStatus(int progress, string? status)
    {
        var normalizedStatus = status?.Trim().ToLowerInvariant();
        if (normalizedStatus == "failed" || progress < 0) return "failed";
        if (normalizedStatus == "completed" || progress >= 100) return "completed";
        return normalizedStatus == "waiting" ? "waiting" : "active";
    }

    private static AiProgressPublicUpdate FailedUpdate(string jobId, bool isMindmap)
    {
        return new AiProgressPublicUpdate(
            jobId,
            Progress: 0,
            Status: "failed",
            Message: isMindmap ? MindmapFailureMessage : AnalysisFailureMessage,
            FailureCode: isMindmap ? "AI_MINDMAP_GENERATION_FAILED" : "AI_VIDEO_ANALYSIS_FAILED",
            Retryable: true);
    }

    private static string ProgressMessage(bool isMindmap, int progress, string status)
    {
        if (status == "waiting")
        {
            return "جاري التحضير ووضع المهمة في قائمة الانتظار...";
        }

        return isMindmap
            ? MindmapProgressMessage(progress, status)
            : AnalysisProgressMessage(progress, status);
    }

    private static string AnalysisProgressMessage(int progress, string status)
    {
        if (status == "completed" || progress >= 100) return "اكتملت معالجة الفيديو بنجاح.";
        if (progress < 20) return "جاري تجهيز الفيديو للتحليل...";
        if (progress < 60) return "جاري تحويل صوت المحاضرة إلى نص مكتوب...";
        if (progress < 85) return "جاري تقسيم المحاضرة وكتابة الملخصات...";
        if (progress < 95) return "جاري بناء الفصول وتجهيز الترجمة...";
        return "جاري حفظ نتائج التحليل...";
    }

    private static string MindmapProgressMessage(int progress, string status)
    {
        if (status == "completed" || progress >= 100) return "اكتمل توليد الخرائط الذهنية بنجاح.";
        if (progress < 20) return "جاري تحضير الصور والبيانات اللازمة...";
        if (progress < 95) return "جاري توليد الخرائط الذهنية للفصول...";
        return "جاري حفظ الخرائط في لوحة التحكم...";
    }
}

public class AiProgressCommandHandler : IRequestHandler<AiProgressCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public AiProgressCommandHandler(IAppDbContext db)
    {
        _db = db;
    }
    public async Task<ApiResponse> Handle(AiProgressCommand request, CancellationToken ct)
    {
        var publicUpdate = AiProgressPublicContract.Create(request.JobId, request.Progress, request.Status);
        var failure = publicUpdate.FailureCode == null
            ? null
            : new
            {
                code = publicUpdate.FailureCode,
                message = publicUpdate.Message,
                retryable = publicUpdate.Retryable
            };
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            jobId = publicUpdate.JobId,
            progress = publicUpdate.Progress,
            status = publicUpdate.Status,
            message = publicUpdate.Message,
            failure
        });

        string? teacherUserId = null;
        Guid? parsedVideoId = null;
        var mindmapSuffixIndex = request.JobId.IndexOf("_mindmap", StringComparison.OrdinalIgnoreCase);
        var isMindmapJob = mindmapSuffixIndex >= 0;
        var videoIdPart = isMindmapJob ? request.JobId[..mindmapSuffixIndex] : request.JobId;
        if (Guid.TryParse(videoIdPart, out var videoId))
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

        if (publicUpdate.Status == "failed")
        {
            if (parsedVideoId.HasValue)
            {
                var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == parsedVideoId.Value, ct);
                if (video != null)
                {
                    if (isMindmapJob)
                    {
                        video.IsProcessingMindmaps = false;
                    }
                    else
                    {
                        video.IsProcessingAI = false;
                    }
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
                            error = publicUpdate.Message,
                            failure
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
                    jobId = publicUpdate.JobId,
                    error = publicUpdate.Message,
                    failure
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
                        jobId = publicUpdate.JobId,
                        error = publicUpdate.Message,
                        failure
                    })
                };
                _db.OutboxEvents.Add(teacherFailedEvent);
            }
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
