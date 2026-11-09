using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Homework;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using SubmissionStatus = NaderGorge.Domain.Entities.Homework.SubmissionStatus;

namespace NaderGorge.Application.Features.Assessments;

public enum AssessmentEntryKind { Exam, Homework }

public sealed record AssessmentQuestionCount(int Minimum, int Maximum);

public sealed record AssessmentEntrySummary(
    int Total,
    AssessmentQuestionCount Essay,
    AssessmentQuestionCount MultipleChoice,
    AssessmentQuestionCount FindTheMistake);

public sealed record GetAssessmentEntrySummaryQuery(Guid AssessmentId, Guid UserId, AssessmentEntryKind Kind)
    : IRequest<ApiResponse<AssessmentEntrySummary>>;

public sealed class GetAssessmentEntrySummaryQueryHandler(
    IAppDbContext db,
    IAccessCheckService access,
    IContentArchiveAccessService archiveAccess)
    : IRequestHandler<GetAssessmentEntrySummaryQuery, ApiResponse<AssessmentEntrySummary>>
{
    public async Task<ApiResponse<AssessmentEntrySummary>> Handle(GetAssessmentEntrySummaryQuery request, CancellationToken ct)
    {
        var summary = request.Kind == AssessmentEntryKind.Exam
            ? await GetExamSummary(request.AssessmentId, request.UserId, ct)
            : await GetHomeworkSummary(request.AssessmentId, request.UserId, ct);
        return summary is null
            ? ApiResponse<AssessmentEntrySummary>.Fail("ملخص الأسئلة غير متاح لهذا الحساب.")
            : ApiResponse<AssessmentEntrySummary>.Ok(summary);
    }

    private async Task<AssessmentEntrySummary?> GetExamSummary(Guid examId, Guid userId, CancellationToken ct)
    {
        var exam = await db.Exams.Where(exam => exam.Id == examId && exam.IsActive)
            .Select(exam => new ExamQuestionDraw(exam.IsRandomized, exam.DisplayQuestionCount, exam.TotalScore, exam.EssayGradingMode))
            .SingleOrDefaultAsync(ct);
        if (exam is null || !await access.HasAccessToExamAsync(userId, examId, ct))
            return null;

        var current = await db.StudentExamAttempts
            .Where(attempt => attempt.UserId == userId && attempt.ExamId == examId && !attempt.IsPassed && attempt.Evaluation == null)
            .OrderByDescending(attempt => attempt.CreatedAt).ThenByDescending(attempt => attempt.Id)
            .Select(attempt => new { attempt.Id, attempt.DefinitionSnapshotJson })
            .FirstOrDefaultAsync(ct);
        if (current?.DefinitionSnapshotJson is { } snapshot)
            return FromSnapshot(AssessmentDefinitionSnapshot.Read(snapshot, "exam", examId));

        var questions = db.ExamQuestions.Where(question => question.ExamId == examId && !question.IsRetired);
        if (current is not null)
        {
            var assigned = await questions
                .Where(question => db.StudentAnswers.Any(answer =>
                    answer.StudentExamAttemptId == current.Id && answer.ExamQuestionId == question.Id))
                .Select(question => (int)question.Question.Type).ToListAsync(ct);
            return Summarize(assigned, assigned.Count);
        }

        var configuredCount = await exam.ReadMultipleChoiceCountAsync(db, examId, ct);
        if (configuredCount is not null)
            return await SummarizeFixedMix(questions, exam, configuredCount, ct);

        var ordered = questions.OrderBy(question => question.Order).ThenBy(question => question.Id)
            .Select(question => (int)question.Question.Type);
        var limit = exam.DisplayQuestionCount.GetValueOrDefault();
        if (limit > 0 && !exam.IsRandomized)
            return await SummarizeAll(ordered.Take(limit), ct);

        var types = await ordered.ToListAsync(ct);
        return Summarize(types, limit > 0 ? Math.Min(limit, types.Count) : types.Count);
    }

    private static async Task<AssessmentEntrySummary?> SummarizeFixedMix(
        IQueryable<ExamQuestion> questions, ExamQuestionDraw draw, string configuredCount, CancellationToken ct)
    {
        var pool = await questions.Select(question => new ExamDrawQuestion(question.Question.Type, question.Points)).ToListAsync(ct);
        if (draw.ResolveMultipleChoiceCount(configuredCount, pool) is not { } choices)
            return null;

        var essays = pool.Count(question => question.Type == QuestionType.Essay);
        return new(choices + essays, new(essays, essays), new(choices, choices), new(0, 0));
    }

    private async Task<AssessmentEntrySummary?> GetHomeworkSummary(Guid homeworkId, Guid userId, CancellationToken ct)
    {
        var homework = await db.Homeworks.ReadyForStudents().Where(homework => homework.Id == homeworkId)
            .Select(homework => new { homework.LessonId, homework.SingleAttemptOnly })
            .SingleOrDefaultAsync(ct);
        if (homework is null
            || !await access.HasAccessToLessonAsync(userId, homework.LessonId, ct)
            || !await archiveAccess.CanViewAsync(userId, ContentArchiveTargetType.Homework, homeworkId, ct))
            return null;

        var current = await db.HomeworkSubmissions
            .Where(submission => submission.HomeworkId == homeworkId && submission.StudentId == userId)
            .Select(submission => new { submission.DefinitionSnapshotJson, submission.Status, submission.OverallScore })
            .FirstOrDefaultAsync(ct);
        if (current?.DefinitionSnapshotJson is { } snapshot)
        {
            var definition = AssessmentDefinitionSnapshot.Read(snapshot, "homework", homeworkId);
            if (definition.Revision?.RequiresCompletion == true
                || current.Status != SubmissionStatus.Graded
                || current.OverallScore >= (definition.PassingScore ?? 0)
                || homework.SingleAttemptOnly)
                return FromSnapshot(definition);
        }

        return await SummarizeAll(db.HomeworkQuestions
            .Where(question => question.HomeworkId == homeworkId && !question.IsRetired)
            .Select(question => (int)question.QuestionType), ct);
    }

    private static AssessmentEntrySummary FromSnapshot(AssessmentDefinitionSnapshot definition)
    {
        var questions = definition.Questions.AsEnumerable();
        if (definition.Revision?.RequiresCompletion == true)
        {
            var required = definition.Revision.Answers.Where(answer => answer.RequiresCompletion && !answer.Excluded)
                .Select(answer => answer.QuestionId).ToHashSet();
            questions = questions.Where(question => required.Contains(question.Id));
        }

        var types = questions.Select(question => question.Type).ToList();
        return Summarize(types, types.Count);
    }

    private static async Task<AssessmentEntrySummary> SummarizeAll(IQueryable<int> query, CancellationToken ct)
    {
        var types = await query.ToListAsync(ct);
        return Summarize(types, types.Count);
    }

    private static AssessmentEntrySummary Summarize(List<int> types, int total)
    {
        AssessmentQuestionCount Count(QuestionType type)
        {
            var available = types.Count(value => value == (int)type);
            // A random draw can include as few as the slots the other types cannot fill.
            return new(Math.Max(0, total - (types.Count - available)), Math.Min(total, available));
        }

        return new(total, Count(QuestionType.Essay), Count(QuestionType.MCQ), Count(QuestionType.FindTheMistake));
    }
}
