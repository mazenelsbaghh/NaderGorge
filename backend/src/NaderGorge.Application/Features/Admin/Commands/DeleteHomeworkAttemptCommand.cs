using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record DeleteHomeworkAttemptCommand(Guid HomeworkId, Guid SubmissionId, Guid ActorId) : IRequest<ApiResponse<bool>>;
public class DeleteHomeworkAttemptCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    : IRequestHandler<DeleteHomeworkAttemptCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteHomeworkAttemptCommand request, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ClearTrackedChanges();
            await using var transaction = await db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, retryCt);
            var target = new AssessmentTarget(AssessmentKind.Homework, request.HomeworkId, request.SubmissionId, request.ActorId);
            if (!await AssessmentAccess.Allowed(db, auth, target, retryCt)) return ApiResponse<bool>.Fail("غير مصرح بحذف هذه المحاولة.");
            var submission = await db.HomeworkSubmissions.Include(s => s.Answers)
                .SingleOrDefaultAsync(s => s.Id == request.SubmissionId && s.HomeworkId == request.HomeworkId, retryCt);
            if (submission == null) return ApiResponse<bool>.Fail("المحاولة غير موجودة أو تم حذفها.");
            db.AuditLogs.Add(new AuditLog
            {
                Action = "HomeworkAttemptDeleted", EntityType = "HomeworkSubmission", EntityId = submission.Id,
                PerformedByUserId = request.ActorId,
                OldValues = System.Text.Json.JsonSerializer.Serialize(new { submission.HomeworkId, submission.StudentId, submission.OverallScore, answerCount = submission.Answers.Count })
            });
            db.HomeworkAnswers.RemoveRange(submission.Answers);
            db.HomeworkSubmissions.Remove(submission);
            await db.SaveChangesAsync(retryCt);
            await transaction.CommitAsync(retryCt);
            return ApiResponse<bool>.Ok(true);
        }, ct);
}
