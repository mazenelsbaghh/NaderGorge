using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Exams.Commands;

public record UseFiftyFiftyCommand(Guid ExamId, Guid AttemptId, Guid QuestionId, Guid UserId) : IRequest<ApiResponse<List<Guid>>>;

public class UseFiftyFiftyCommandHandler : IRequestHandler<UseFiftyFiftyCommand, ApiResponse<List<Guid>>>
{
    private readonly IAppDbContext _db;

    public UseFiftyFiftyCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<Guid>>> Handle(UseFiftyFiftyCommand request, CancellationToken cancellationToken)
    {
        var attempt = await _db.StudentExamAttempts
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId && a.UserId == request.UserId && a.ExamId == request.ExamId, cancellationToken);

        if (attempt == null)
            return ApiResponse<List<Guid>>.Fail("Attempt not found");

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam == null)
            return ApiResponse<List<Guid>>.Fail("Exam not found");

        if (exam.DurationMinutes.HasValue && attempt.StartedAt.HasValue)
        {
            var timeAllowed = TimeSpan.FromMinutes(exam.DurationMinutes.Value).Add(TimeSpan.FromSeconds(60));
            var timeTaken = DateTime.UtcNow - attempt.StartedAt.Value;
            if (timeTaken > timeAllowed)
            {
                if (attempt.Evaluation == null)
                {
                    attempt.IsTimeExpired = true;
                    attempt.ScoreAchieved = 0;
                    attempt.IsPassed = false;
                    attempt.Evaluation = "انتهى الوقت";
                    await _db.SaveChangesAsync(cancellationToken);
                }
                return ApiResponse<List<Guid>>.Fail("لقد انتهى وقت المحاولة السابقة وتعتبر غير مجتازة.");
            }
        }

        var examQuestion = await _db.ExamQuestions
            .Include(eq => eq.Question)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(eq => eq.Id == request.QuestionId && eq.ExamId == request.ExamId, cancellationToken);
            
        if (examQuestion == null)
            return ApiResponse<List<Guid>>.Fail("Question not found");
            
        if (examQuestion.Question.Type != NaderGorge.Domain.Entities.QuestionType.MCQ)
            return ApiResponse<List<Guid>>.Fail("Fifty fifty can only be used on Multiple Choice questions");

        var answer = await _db.FindStudentAnswerAsync(request.AttemptId, request.QuestionId, cancellationToken);
        if (answer == null)
        {
            answer = new NaderGorge.Domain.Entities.StudentAnswer
            {
                Id = Guid.NewGuid(),
                StudentExamAttemptId = attempt.Id,
                ExamQuestionId = examQuestion.Id,
                HintUsed = true,
                IsCorrect = false,
                PointsAwarded = 0
            };

            _db.StudentAnswers.Add(answer);
        }
        else
        {
            answer.HintUsed = true;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var wrongOptions = examQuestion.Question.Options
            .Where(o => !o.IsCorrect)
            .OrderBy(x => Guid.NewGuid()) // shuffle
            .Take(Math.Max(1, examQuestion.Question.Options.Count / 2)) // Take half the wrong options or at least 1 (often 2)
            .Select(o => o.Id)
            .ToList();

        // Ensure we take maximum 2 options for 4-option questions
        return ApiResponse<List<Guid>>.Ok(wrongOptions.Take(2).ToList(), "Success");
    }
}
