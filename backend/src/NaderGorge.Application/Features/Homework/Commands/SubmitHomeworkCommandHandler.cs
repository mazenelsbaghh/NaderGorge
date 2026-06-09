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

    public SubmitHomeworkCommandHandler(IAppDbContext dbContext, IPublisher publisher)
    {
        _dbContext = dbContext;
        _publisher = publisher;
    }

    public async Task<ApiResponse<bool>> Handle(SubmitHomeworkCommand request, CancellationToken cancellationToken)
    {
        var homework = await _dbContext.Homeworks
            .Include(h => h.Questions)
            .FirstOrDefaultAsync(h => h.Id == request.HomeworkId, cancellationToken);

        if (homework == null)
            return ApiResponse<bool>.Fail("Homework not found");

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

        await _dbContext.SaveChangesAsync(cancellationToken);

        // At this point, Domain Events or background job enqueuing to BullMQ should ideally be triggered
        // for AI auto-grading (Task T019 for AI pipeline). For now, it stays PendingReview.

        await _publisher.Publish(new NaderGorge.Application.Features.Gamification.Commands.AcademicTaskCompletedEvent(request.StudentId, NaderGorge.Domain.Entities.Gamification.GamificationEventType.HomeworkSubmittedOnTime, 20), cancellationToken);

        return ApiResponse<bool>.Ok(true, "Homework submitted successfully.");
    }
}
