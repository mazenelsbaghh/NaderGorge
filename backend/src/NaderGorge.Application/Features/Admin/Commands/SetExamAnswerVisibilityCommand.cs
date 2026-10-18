using System.Data;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public sealed record ExamAnswerVisibilityDto(bool ShowAnswersToStudents, int NotifiedStudents);
public sealed record SetExamAnswerVisibilityCommand(Guid ExamId, bool ShowAnswers, Guid ActorId)
    : IRequest<ApiResponse<ExamAnswerVisibilityDto>>;

public sealed class SetExamAnswerVisibilityCommandHandler(IAppDbContext db)
    : IRequestHandler<SetExamAnswerVisibilityCommand, ApiResponse<ExamAnswerVisibilityDto>>
{
    public async Task<ApiResponse<ExamAnswerVisibilityDto>> Handle(SetExamAnswerVisibilityCommand request, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Users.AnyAsync(user => user.Id == request.ActorId && user.IsActive, ct))
            return ApiResponse<ExamAnswerVisibilityDto>.Fail("غير مصرح بتعديل هذا الامتحان.", ["FORBIDDEN"]);
        if (!await new TeacherAuthorizationService(db).CanAccessExamAsync(request.ActorId, request.ExamId, ct))
            return ApiResponse<ExamAnswerVisibilityDto>.Fail("غير مصرح بتعديل هذا الامتحان.", ["FORBIDDEN"]);

        var exam = await db.Exams.SingleOrDefaultAsync(exam => exam.Id == request.ExamId, ct);
        if (exam is null) return ApiResponse<ExamAnswerVisibilityDto>.Fail("الامتحان غير موجود.", ["NOT_FOUND"]);
        if (exam.ShowAnswersToStudents == request.ShowAnswers)
            return ApiResponse<ExamAnswerVisibilityDto>.Ok(new(request.ShowAnswers, 0));

        var previous = exam.ShowAnswersToStudents;
        exam.ShowAnswersToStudents = request.ShowAnswers;
        var notified = await QueueStudentUpdates(exam, ct);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "ExamAnswerVisibilityChanged", EntityType = nameof(Exam), EntityId = exam.Id,
            PerformedByUserId = request.ActorId,
            OldValues = JsonSerializer.Serialize(new { ShowAnswersToStudents = previous }),
            NewValues = JsonSerializer.Serialize(new { exam.ShowAnswersToStudents, NotifiedStudents = notified })
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ApiResponse<ExamAnswerVisibilityDto>.Ok(new(exam.ShowAnswersToStudents, notified));
    }

    private async Task<int> QueueStudentUpdates(Exam exam, CancellationToken ct)
    {
        var actionUrl = $"/student/exams/{exam.Id}";
        var notifiedUsers = (await db.NotificationEvents.Where(n => n.ActionUrl == actionUrl
                || (n.ActionUrl != null && n.ActionUrl.StartsWith(actionUrl + "?attemptId=")))
            .Select(n => n.UserId).ToListAsync(ct)).ToHashSet();
        var submissions = await db.StudentExamAttempts.AsNoTracking()
            .Where(a => a.ExamId == exam.Id && (a.SubmittedAt != null || a.Evaluation != null
                || db.EssaySubmissions.Any(essay => essay.StudentExamAttemptId == a.Id)))
            .OrderByDescending(a => a.SubmittedAt ?? a.CreatedAt)
            .Select(a => new { a.Id, a.UserId, a.ScoreAchieved, a.IsPassed }).ToListAsync(ct);
        var notified = 0;
        foreach (var attempt in submissions.DistinctBy(a => a.UserId))
        {
            db.OutboxEvents.Add(new OutboxEvent
            {
                Type = "ExamResultReady", TargetUserId = attempt.UserId.ToString(),
                PayloadJson = JsonSerializer.Serialize(new { examId = exam.Id, attemptId = attempt.Id,
                    score = attempt.ScoreAchieved, isPassed = attempt.IsPassed })
            });
            if (!exam.ShowAnswersToStudents || !notifiedUsers.Add(attempt.UserId)) continue;
            db.NotificationEvents.Add(new NotificationEvent
            {
                UserId = attempt.UserId, ChannelType = NotificationChannelType.InApp,
                Title = "إجابات الامتحان متاحة الآن", Body = $"تم فتح إجابات امتحان «{exam.Title}». راجع إجاباتك والتصحيح من نتيجتك المحفوظة.",
                ActionUrl = $"{actionUrl}?attemptId={attempt.Id}"
            });
            notified++;
        }
        return notified;
    }
}
