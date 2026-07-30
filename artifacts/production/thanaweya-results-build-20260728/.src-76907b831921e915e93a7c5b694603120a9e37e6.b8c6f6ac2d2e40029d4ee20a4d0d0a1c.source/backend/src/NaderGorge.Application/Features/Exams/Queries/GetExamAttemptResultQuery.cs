using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Queries;

public record GetExamAttemptResultQuery(Guid AttemptId, Guid UserId) : IRequest<ApiResponse<ExamResultDto>>;

public class GetExamAttemptResultQueryHandler : IRequestHandler<GetExamAttemptResultQuery, ApiResponse<ExamResultDto>>
{
    private readonly IAppDbContext _db;

    public GetExamAttemptResultQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ExamResultDto>> Handle(GetExamAttemptResultQuery request, CancellationToken ct)
    {
        var attempt = await _db.StudentExamAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId && a.UserId == request.UserId, ct);

        if (attempt == null)
        {
            return ApiResponse<ExamResultDto>.Fail("Attempt not found", new List<string> { "NOT_FOUND" });
        }

        var hasSubmission = attempt.Evaluation != null
            || await _db.EssaySubmissions.AnyAsync(e => e.StudentExamAttemptId == attempt.Id, ct)
            || await _db.StudentAnswers.AnyAsync(
                a => a.StudentExamAttemptId == attempt.Id &&
                     (a.SelectedOptionId != null || !string.IsNullOrWhiteSpace(a.SubmittedText)),
                ct);

        if (!hasSubmission)
        {
            return ApiResponse<ExamResultDto>.Fail("Attempt has not been submitted yet.");
        }

        var exam = await _db.Exams
            .AsNoTracking()
            .Include(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Question)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(e => e.Id == attempt.ExamId, ct);

        if (exam == null)
        {
            return ApiResponse<ExamResultDto>.Fail("Exam not found");
        }

        var lesson = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.ContentSection)
            .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(l => l.ExamId == exam.Id, ct);

        var progress = lesson == null
            ? null
            : await _db.LessonProgresses
                .AsNoTracking()
                .FirstOrDefaultAsync(lp => lp.UserId == request.UserId && lp.LessonId == lesson.Id, ct);

        var answers = await _db.StudentAnswers
            .AsNoTracking()
            .Include(a => a.SelectedOption)
            .Where(a => a.StudentExamAttemptId == attempt.Id)
            .ToListAsync(ct);

        var essays = await _db.EssaySubmissions
            .AsNoTracking()
            .Where(e => e.StudentExamAttemptId == attempt.Id)
            .ToListAsync(ct);

        var resultState = DetermineResultState(essays);
        var result = ExamResultBuilder.Build(
            exam,
            attempt,
            blocksNextLesson: !attempt.IsPassed && !(progress?.IsManuallyUnlocked ?? false),
            lesson?.Id,
            lesson?.ContentSection?.Term?.PackageId,
            BuildQuestionReviewSnapshots(answers, essays, exam),
            revealCorrectAnswers: true,
            resultState);

        return ApiResponse<ExamResultDto>.Ok(result);
    }

    private static Dictionary<Guid, QuestionReviewSnapshot> BuildQuestionReviewSnapshots(
        IEnumerable<StudentAnswer> answers,
        IEnumerable<EssaySubmission> essays,
        Exam exam)
    {
        var snapshots = ExamResultBuilder.BuildQuestionReviewSnapshots(answers);
        var questionIdToExamQuestionId = exam.ExamQuestions.ToDictionary(eq => eq.Question.Id, eq => eq.Id);

        foreach (var essay in essays)
        {
            if (!questionIdToExamQuestionId.TryGetValue(essay.QuestionId, out var examQuestionId))
            {
                continue;
            }

            var isTeacherGraded = essay.Status == EssaySubmissionStatus.TeacherGraded;
            snapshots[examQuestionId] = new QuestionReviewSnapshot(
                essay.AnswerText,
                !string.IsNullOrWhiteSpace(essay.AnswerText) || !string.IsNullOrWhiteSpace(essay.AudioUrl),
                isTeacherGraded,
                isTeacherGraded ? essay.TeacherFinalScore ?? 0 : 0,
                essay.AudioUrl);
        }

        return snapshots;
    }

    private static string DetermineResultState(IEnumerable<EssaySubmission> essays)
    {
        var essayList = essays.ToList();
        if (essayList.Count == 0)
        {
            return "Completed";
        }

        if (essayList.Any(e => e.Status == EssaySubmissionStatus.WaitAI))
        {
            return "Pending";
        }

        if (essayList.Any(e => e.Status != EssaySubmissionStatus.TeacherGraded))
        {
            return "PartiallyGraded";
        }

        return "Completed";
    }
}
