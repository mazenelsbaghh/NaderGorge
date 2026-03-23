using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Commands;

public record SubmitExamCommand(Guid ExamId, Guid UserId, List<AnswerSubmissionDto> Answers) : IRequest<ApiResponse<ExamResultDto>>;

public record AnswerSubmissionDto(Guid ExamQuestionId, Guid SelectedOptionId);

public record ExamResultDto(Guid AttemptId, decimal ScoreAchieved, decimal TotalScore, bool IsPassed, bool BlocksNextLesson);

public class SubmitExamCommandHandler : IRequestHandler<SubmitExamCommand, ApiResponse<ExamResultDto>>
{
    private readonly IAppDbContext _db;

    public SubmitExamCommandHandler(IAppDbContext db)
    {
        _db = db;
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

        var attempt = new StudentExamAttempt
        {
            UserId = request.UserId,
            ExamId = request.ExamId,
            ScoreAchieved = 0,
            Answers = new List<StudentAnswer>()
        };

        decimal totalScoreScored = 0;

        foreach (var sub in request.Answers)
        {
            var eq = exam.ExamQuestions.FirstOrDefault(x => x.Id == sub.ExamQuestionId);
            if (eq == null) continue;

            var selectedOption = eq.Question.Options.FirstOrDefault(o => o.Id == sub.SelectedOptionId);
            if (selectedOption == null) continue;

            var points = selectedOption.IsCorrect ? eq.Points : 0;
            totalScoreScored += points;

            attempt.Answers.Add(new StudentAnswer
            {
                ExamQuestionId = eq.Id,
                SelectedOptionId = selectedOption.Id,
                IsCorrect = selectedOption.IsCorrect,
                PointsAwarded = points
            });
        }

        attempt.ScoreAchieved = totalScoreScored;
        attempt.IsPassed = totalScoreScored >= exam.PassingScore;

        _db.StudentExamAttempts.Add(attempt);
        
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

        var result = new ExamResultDto(attempt.Id, attempt.ScoreAchieved, exam.TotalScore, attempt.IsPassed, blocksNextLesson);
        return ApiResponse<ExamResultDto>.Ok(result, attempt.IsPassed ? "Exam passed!" : "Exam failed.");
    }
}
