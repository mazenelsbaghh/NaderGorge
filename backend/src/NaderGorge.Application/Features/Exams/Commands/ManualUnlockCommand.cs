using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Commands;

public record ManualUnlockCommand(Guid LessonId, Guid StudentId, Guid AdminId) : IRequest<ApiResponse>;

public class ManualUnlockCommandHandler : IRequestHandler<ManualUnlockCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public ManualUnlockCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(ManualUnlockCommand request, CancellationToken ct)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == request.LessonId, ct);
        if (lesson == null) return ApiResponse.Fail("Lesson not found");

        var progress = await _db.LessonProgresses
            .FirstOrDefaultAsync(lp => lp.UserId == request.StudentId && lp.LessonId == request.LessonId, ct);

        if (progress == null)
        {
            progress = new LessonProgress
            {
                UserId = request.StudentId,
                LessonId = request.LessonId,
                IsCompleted = false,
                IsManuallyUnlocked = true
            };
            _db.LessonProgresses.Add(progress);
        }
        else
        {
            progress.IsManuallyUnlocked = true;
        }

        // Add Audit Log
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "ManualUnlock",
            EntityType = "Lesson",
            EntityId = request.LessonId,
            PerformedByUserId = request.AdminId,
            NewValues = $"Manually unlocked lesson {request.LessonId} for student {request.StudentId}",
            IpAddress = "System"
        });

        var unlockEvent = new OutboxEvent
        {
            Type = "LessonManuallyUnlocked",
            TargetUserId = request.StudentId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lessonId = request.LessonId,
                studentId = request.StudentId
            })
        };
        _db.OutboxEvents.Add(unlockEvent);

        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok("Lesson successfully unlocked for student.");
    }
}
