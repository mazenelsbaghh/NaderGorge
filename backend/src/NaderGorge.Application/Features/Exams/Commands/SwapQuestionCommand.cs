using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Exams.Commands;

public record SwapQuestionCommand(Guid ExamId, Guid AttemptId, Guid QuestionId, Guid UserId) : IRequest<ApiResponse<ExamQuestionViewDto>>;

public class SwapQuestionCommandHandler : IRequestHandler<SwapQuestionCommand, ApiResponse<ExamQuestionViewDto>>
{
    private readonly IAppDbContext _db;

    public SwapQuestionCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ExamQuestionViewDto>> Handle(SwapQuestionCommand request, CancellationToken cancellationToken)
    {
        var attempt = await _db.StudentExamAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId && a.UserId == request.UserId && a.ExamId == request.ExamId, cancellationToken);

        if (attempt == null)
            return ApiResponse<ExamQuestionViewDto>.Fail("Attempt not found");

        if (attempt.Evaluation != null || attempt.IsPassed)
            return ApiResponse<ExamQuestionViewDto>.Fail("Attempt already submitted");

        var revision = attempt.DefinitionSnapshotJson is null ? null
            : AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson, "exam", attempt.ExamId);
        if (revision?.Revision?.RequiresCompletion == true && (revision.CompletionStartedAt is null
            || !revision.Revision.Answers.Any(a => a.QuestionId == request.QuestionId && a.RequiresCompletion && !a.Excluded)))
            return ApiResponse<ExamQuestionViewDto>.Fail("يمكن تبديل الأسئلة المضافة فقط بعد بدء الاستكمال.");

        var exam = await _db.Exams
            .Include(e => e.ExamQuestions.Where(q => !q.IsRetired))
            .ThenInclude(eq => eq.Question)
            .ThenInclude(q => q.Options.Where(o => !o.IsRetired))
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);

        if (exam == null)
            return ApiResponse<ExamQuestionViewDto>.Fail("Exam not found");

        var replacements = AssessmentDefinitionSnapshot.ResolveSwapCandidates(exam, attempt.DefinitionSnapshotJson);
        var originalExam = exam;
        exam = AssessmentDefinitionSnapshot.ResolveExam(exam, attempt.DefinitionSnapshotJson);

        if (exam.DurationMinutes.HasValue && attempt.StartedAt.HasValue)
        {
            var timeAllowed = TimeSpan.FromMinutes(exam.DurationMinutes.Value).Add(TimeSpan.FromSeconds(60));
            var timeTaken = DateTime.UtcNow - attempt.StartedAt.Value;
            if (timeTaken > timeAllowed)
            {
                if (attempt.Evaluation == null && revision?.Revision?.RequiresCompletion != true)
                {
                    attempt.IsTimeExpired = true;
                    attempt.ScoreAchieved = 0;
                    attempt.IsPassed = false;
                    attempt.Evaluation = "انتهى الوقت";
                    await _db.SaveChangesAsync(cancellationToken);
                }
                return ApiResponse<ExamQuestionViewDto>.Fail("لقد انتهى وقت المحاولة السابقة وتعتبر غير مجتازة.");
            }
        }

        var currentAnswer = attempt.Answers.FirstOrDefault(a => a.ExamQuestionId == request.QuestionId);
        if (currentAnswer == null)
            return ApiResponse<ExamQuestionViewDto>.Fail("Question is not part of your active attempt");

        var usedQuestionIds = attempt.Answers.Select(a => a.ExamQuestionId).ToHashSet();

        var availableExtraQuestions = replacements
            .Where(eq => !usedQuestionIds.Contains(eq.Id))
            .ToList();

        if (availableExtraQuestions.Count == 0)
        {
            return ApiResponse<ExamQuestionViewDto>.Fail("لا يوجد أسئلة إضافية متاحة للتبديل في بنك أسئلة هذا الامتحان.");
        }

        var random = new Random();
        var newExamQuestion = availableExtraQuestions[random.Next(availableExtraQuestions.Count)];

        attempt.DefinitionSnapshotJson ??= AssessmentDefinitionSnapshot.FromExam(originalExam,
            originalExam.ExamQuestions.Where(q => usedQuestionIds.Contains(q.Id))).ToJson();
        attempt.DefinitionSnapshotJson = AssessmentDefinitionSnapshot.SwapAssignedQuestion(
            attempt.DefinitionSnapshotJson, exam.Id, request.QuestionId, newExamQuestion.Id);

        // Remove old answer and add new one
        _db.StudentAnswers.Remove(currentAnswer);

        var newAnswer = new StudentAnswer
        {
            Id = Guid.NewGuid(),
            StudentExamAttemptId = attempt.Id,
            ExamQuestionId = newExamQuestion.Id,
            HintUsed = false,
            IsCorrect = false,
            PointsAwarded = 0
        };

        _db.StudentAnswers.Add(newAnswer);
        await _db.SaveChangesAsync(cancellationToken);

        // Map to DTO
        var options = newExamQuestion.Question.Options
            .OrderBy(x => random.Next()) // Shuffle options
            .Select(o => new QuestionOptionViewDto(o.Id, o.Text))
            .ToList();

        string? baseText = null;
        int? mistakeStartIndex = null;
        int? mistakeEndIndex = null;
        if (newExamQuestion.Question is FindTheMistakeQuestion ftm)
        {
            baseText = ftm.BaseText;
            mistakeStartIndex = ftm.MistakeStartIndex;
            mistakeEndIndex = ftm.MistakeEndIndex;
        }

        var dto = new ExamQuestionViewDto(
            newExamQuestion.Id,
            newExamQuestion.Question.Text,
            newExamQuestion.Question.Type.ToString(),
            newExamQuestion.Points,
            newExamQuestion.Question.HintText,
            newExamQuestion.Question.ImageUrl,
            baseText,
            mistakeStartIndex,
            mistakeEndIndex,
            options);

        return ApiResponse<ExamQuestionViewDto>.Ok(dto, "تم تبديل السؤال بنجاح.");
    }
}
