using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Services;

namespace NaderGorge.Application.Features.Exams.Commands;

public record SubmitExamCommand(Guid ExamId, Guid AttemptId, Guid UserId, List<AnswerSubmissionDto> Answers) : IRequest<ApiResponse<ExamResultDto>>;

public record AnswerSubmissionDto(Guid ExamQuestionId, Guid SelectedOptionId);

public record ExamResultDto(Guid AttemptId, decimal ScoreAchieved, decimal TotalScore, bool IsPassed, bool BlocksNextLesson, string Evaluation, bool IsTimeExpired);

public class SubmitExamCommandHandler : IRequestHandler<SubmitExamCommand, ApiResponse<ExamResultDto>>
{
    private readonly IAppDbContext _db;
    private readonly IPublisher _publisher;

    public SubmitExamCommandHandler(IAppDbContext db, IPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<ApiResponse<ExamResultDto>> Handle(SubmitExamCommand request, CancellationToken ct)
    {
        // 1. Fetch exam with its questions and correct options
        var exam = await _db.Exams
            .Include(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Question)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, ct);

        if (exam == null) return ApiResponse<ExamResultDto>.Fail("Exam not found");

        var attempt = await _db.StudentExamAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId && a.UserId == request.UserId && a.ExamId == request.ExamId, ct);
            
        if (attempt == null) return ApiResponse<ExamResultDto>.Fail("Attempt not found or invalid.");
        
        if (attempt.Answers.Any()) return ApiResponse<ExamResultDto>.Fail("This attempt has already been submitted.");

        attempt.ScoreAchieved = 0;

        // Calculate Time Expiry
        if (exam.DurationMinutes.HasValue && attempt.StartedAt.HasValue)
        {
            var timeAllowed = TimeSpan.FromMinutes(exam.DurationMinutes.Value).Add(TimeSpan.FromSeconds(60)); // 60s grace period
            var timeTaken = DateTime.UtcNow - attempt.StartedAt.Value;
            if (timeTaken > timeAllowed)
            {
                attempt.IsTimeExpired = true;
            }
        }

        decimal rawPointsEarned = 0;
        decimal rawPointsPossible = exam.ExamQuestions.Sum(q => q.Points);

        foreach (var sub in request.Answers)
        {
            var eq = exam.ExamQuestions.FirstOrDefault(x => x.Id == sub.ExamQuestionId);
            if (eq == null) continue;

            var selectedOption = eq.Question.Options.FirstOrDefault(o => o.Id == sub.SelectedOptionId);
            if (selectedOption == null) continue;

            var points = selectedOption.IsCorrect ? eq.Points : 0;
            rawPointsEarned += points;

            attempt.Answers.Add(new StudentAnswer
            {
                ExamQuestionId = eq.Id,
                SelectedOptionId = selectedOption.Id,
                IsCorrect = selectedOption.IsCorrect,
                PointsAwarded = points
            });
        }

        var scaledScore = GradingEvaluationService.CalculateScaledScore(rawPointsEarned, rawPointsPossible, exam.TotalScore);
        
        attempt.ScoreAchieved = scaledScore;
        attempt.IsPassed = scaledScore >= exam.PassingScore;
        attempt.Evaluation = GradingEvaluationService.DetermineEvaluation(scaledScore, exam.PassingScore, exam.TotalScore);

        // We already have attempt tracked, no need to add, just update relationships
        // _db.StudentExamAttempts.Update(attempt) is implicit since it's tracked
        
        // Find lesson tied to this exam to see if we block
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.ExamId == exam.Id, ct);
        
        bool blocksNextLesson = false;
        if (lesson != null)
        {
            // For Lesson Progress tracking (Phase 4 / US3)
            var progress = await _db.LessonProgresses
                .FirstOrDefaultAsync(lp => lp.UserId == request.UserId && lp.LessonId == lesson.Id, ct);

            if (progress == null)
            {
                progress = new LessonProgress
                {
                    UserId = request.UserId,
                    LessonId = lesson.Id,
                    IsCompleted = attempt.IsPassed,
                    IsManuallyUnlocked = false
                };
                _db.LessonProgresses.Add(progress);
            }
            else
            {
                // Unlocked or re-eval
                if (attempt.IsPassed) progress.IsCompleted = true;
            }

            blocksNextLesson = !attempt.IsPassed && !progress.IsManuallyUnlocked;
        }

        await _db.SaveChangesAsync(ct);

        if (attempt.IsPassed)
        {
            int basePoints = (int)exam.TotalScore > 0 ? (int)exam.TotalScore : 50;
            // Award points for passing an exam
            await _publisher.Publish(new NaderGorge.Application.Features.Gamification.Commands.AcademicTaskCompletedEvent(request.UserId, NaderGorge.Domain.Entities.Gamification.GamificationEventType.PerfectExam, basePoints), ct);
        }

        var result = new ExamResultDto(attempt.Id, attempt.ScoreAchieved, exam.TotalScore, attempt.IsPassed, blocksNextLesson, attempt.Evaluation ?? "غير مقيم", attempt.IsTimeExpired);
        return ApiResponse<ExamResultDto>.Ok(result, attempt.IsPassed ? "Exam passed!" : "Exam failed.");
    }
}
