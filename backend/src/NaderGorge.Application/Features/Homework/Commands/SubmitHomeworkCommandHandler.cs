using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Homework.Commands;

public class SubmitHomeworkCommandHandler : IRequestHandler<SubmitHomeworkCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _dbContext;
    private readonly IPublisher _publisher;
    private readonly IAccessCheckService _access;

    public SubmitHomeworkCommandHandler(IAppDbContext dbContext, IPublisher publisher, IAccessCheckService access)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _access = access;
    }

    public async Task<ApiResponse<bool>> Handle(SubmitHomeworkCommand request, CancellationToken cancellationToken)
    {
        var homework = await _dbContext.Homeworks
            .Include(h => h.Questions)
            .FirstOrDefaultAsync(h => h.Id == request.HomeworkId, cancellationToken);

        if (homework == null)
            return ApiResponse<bool>.Fail("Homework not found");

        var hasAccess = await _access.HasAccessToLessonAsync(request.StudentId, homework.LessonId, cancellationToken);
        if (!hasAccess)
        {
            return ApiResponse<bool>.Fail("You do not have access to this homework's lesson.");
        }

        // Check if a submission already exists
        var submission = await _dbContext.HomeworkSubmissions
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.HomeworkId == request.HomeworkId && s.StudentId == request.StudentId, cancellationToken);

        if (submission != null && submission.Status != SubmissionStatus.InProgress)
        {
            return ApiResponse<bool>.Fail("Homework already submitted.");
        }

        if (submission == null)
        {
            submission = new HomeworkSubmission
            {
                HomeworkId = request.HomeworkId,
                StudentId = request.StudentId,
                StartedAt = DateTime.UtcNow
            };
            _dbContext.HomeworkSubmissions.Add(submission);
        }

        // Process answers.

        // Remove existing answers to replace them.
        submission.Answers.Clear();

        foreach (var answerInput in request.Answers)
        {
            if (homework.Questions.Any(q => q.Id == answerInput.QuestionId))
            {
                submission.Answers.Add(new HomeworkAnswer
                {
                    QuestionId = answerInput.QuestionId,
                    ProvidedAnswer = answerInput.ProvidedAnswer
                });
            }
        }

        submission.Status = SubmissionStatus.PendingReview;
        submission.SubmittedAt = DateTime.UtcNow;

        var outboxEvent = new NaderGorge.Domain.Entities.OutboxEvent
        {
            Type = "HomeworkSubmitted",
            TargetUserId = request.StudentId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                homeworkId = request.HomeworkId,
                submissionId = submission.Id,
                studentId = request.StudentId
            })
        };
        _dbContext.OutboxEvents.Add(outboxEvent);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("IX_HomeworkSubmissions_HomeworkId_StudentId") ||
                msg.Contains("unique constraint") ||
                msg.Contains("UNIQUE constraint") ||
                msg.Contains("duplicate key"))
            {
                var existing = await _dbContext.HomeworkSubmissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.HomeworkId == request.HomeworkId && s.StudentId == request.StudentId, cancellationToken);
                if (existing != null)
                {
                    return ApiResponse<bool>.Ok(true, "Homework already submitted.");
                }
            }
            throw;
        }

        // At this point, Domain Events or background job enqueuing to BullMQ should ideally be triggered
        // for AI auto-grading (Task T019 for AI pipeline). For now, it stays PendingReview.

        await _publisher.Publish(new NaderGorge.Application.Features.Gamification.Commands.AcademicTaskCompletedEvent(request.StudentId, NaderGorge.Domain.Entities.Gamification.GamificationEventType.HomeworkSubmittedOnTime, 20), cancellationToken);

        return ApiResponse<bool>.Ok(true, "Homework submitted successfully.");
    }
}
