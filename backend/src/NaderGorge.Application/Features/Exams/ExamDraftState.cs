using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams;

public sealed record ExamDraftScope(Guid ExamId, Guid AttemptId, Guid UserId, Guid? RevisionId);
public sealed record ExamDraftDto(long Version, Guid? LastMutationId, DateTime? SavedAt,
    Dictionary<Guid, string> Answers, bool IsClosed);
public sealed record SaveExamDraftRequest(long ExpectedVersion, Guid MutationId,
    Dictionary<Guid, string> Answers, Guid? RevisionId = null);

internal sealed record ExamDraftState(StudentExamAttempt Attempt, AssessmentDefinitionSnapshot Definition)
{
    public Guid RevisionId => Definition.Revision?.RequiresCompletion == true
        ? Definition.RevisionId ?? Guid.Empty : Guid.Empty;
    public bool IsClosed => Attempt.Evaluation != null || Attempt.IsPassed
        || (Definition.Revision?.RequiresCompletion != true && (Attempt.SubmittedAt != null
            || Attempt.Answers.Any(a => a.SelectedOptionId != null || !string.IsNullOrWhiteSpace(a.SubmittedText))));
    public IEnumerable<AssessmentQuestionSnapshot> EditableQuestions => Definition.Revision?.RequiresCompletion == true
        ? Definition.Questions.Where(q => Definition.Revision.Answers.Any(a =>
            a.QuestionId == q.Id && a.RequiresCompletion && !a.Excluded)) : Definition.Questions;

    public static async Task<ExamDraftState?> Read(IAppDbContext db, ExamDraftScope scope, CancellationToken ct)
    {
        var attempt = await db.StudentExamAttempts.Include(a => a.Answers).SingleOrDefaultAsync(a =>
            a.Id == scope.AttemptId && a.ExamId == scope.ExamId && a.UserId == scope.UserId, ct);
        if (attempt is null) return null;
        if (attempt.DefinitionSnapshotJson is not null)
            return new(attempt, AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson, "exam", scope.ExamId));
        var exam = await db.Exams.Include(e => e.ExamQuestions).ThenInclude(q => q.Question)
            .ThenInclude(q => q.Options).SingleAsync(e => e.Id == scope.ExamId, ct);
        var assigned = attempt.Answers.Select(a => a.ExamQuestionId).ToHashSet();
        return new(attempt, AssessmentDefinitionSnapshot.FromExam(exam, exam.ExamQuestions.Where(q => assigned.Contains(q.Id))));
    }

    public static async Task Lock(IAppDbContext db, ExamDraftScope scope, CancellationToken ct)
    {
        if (db is not DbContext context || context.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT "Id" FROM student_exam_attempts
            WHERE "Id" = {scope.AttemptId} AND "ExamId" = {scope.ExamId} AND "UserId" = {scope.UserId} FOR UPDATE
            """, ct);
        db.ClearTrackedChanges();
    }

    public string? WriteRejection(ExamDraftScope scope)
    {
        if (IsClosed) return "EXAM_DRAFT_CLOSED";
        if (RevisionId != (scope.RevisionId ?? Guid.Empty)
            || (Definition.Revision?.RequiresCompletion == true && Definition.CompletionStartedAt is null))
            return "EXAM_DRAFT_REVISION_CHANGED";
        if (Definition.DurationMinutes is int minutes && Attempt.StartedAt is DateTime started
            && DateTime.UtcNow > started.AddMinutes(minutes).AddSeconds(60)) return "EXAM_DRAFT_EXPIRED";
        return null;
    }

    public bool Accepts(Dictionary<Guid, string>? answers)
    {
        if (answers is null || answers.Count > 1000 || answers.Values.Any(a => a is null || a.Length > 20000)) return false;
        if (answers.Values.Sum(a => (long)a.Length) > 1_000_000) return false;
        var questions = EditableQuestions.ToDictionary(q => q.Id);
        return answers.All(a => questions.TryGetValue(a.Key, out var question)
            && (string.IsNullOrWhiteSpace(a.Value) || question.Type != (int)QuestionType.MCQ
                || (Guid.TryParse(a.Value, out var optionId) && question.Options.Any(o => o.Id == optionId))));
    }

    public ExamDraftDto View(StudentExamDraft? draft)
    {
        var assigned = EditableQuestions.Select(q => q.Id).ToHashSet();
        var answers = Parse(draft).Where(a => assigned.Contains(a.Key)).ToDictionary(a => a.Key, a => a.Value);
        return new(draft?.Version ?? 0, draft?.LastMutationId, draft?.SavedAt, answers, IsClosed);
    }

    public static Dictionary<Guid, string> Parse(StudentExamDraft? draft) => draft is null
        ? [] : JsonSerializer.Deserialize<Dictionary<Guid, string>>(draft.AnswersJson)!;

    public static async Task<ApiResponse<List<AnswerSubmissionDto>>> Submission(
        IAppDbContext db, SubmitExamCommand request, AssessmentDefinitionSnapshot definition, CancellationToken ct)
    {
        var revision = request.RevisionId ?? Guid.Empty;
        var currentRevision = definition.Revision?.RequiresCompletion == true
            ? definition.RevisionId ?? Guid.Empty : Guid.Empty;
        if (revision != currentRevision)
            return ApiResponse<List<AnswerSubmissionDto>>.Fail("تغيّرت أسئلة المحاولة؛ أعد فتح الامتحان.", ["EXAM_DRAFT_REVISION_CHANGED"]);
        var draft = await db.StudentExamDrafts.SingleOrDefaultAsync(d =>
            d.AttemptId == request.AttemptId && d.RevisionId == revision, ct);
        if (draft is null || request.DraftVersion != draft.Version)
            return ApiResponse<List<AnswerSubmissionDto>>.Fail("تغيّرت الإجابات المحفوظة. راجعها قبل التسليم.", ["EXAM_DRAFT_CONFLICT"]);
        var answers = Parse(draft);
        var submissions = definition.Questions.Where(q => answers.TryGetValue(q.Id, out var answer)
            && !string.IsNullOrWhiteSpace(answer)).Select(q => q.Type switch
            {
                (int)QuestionType.MCQ => new AnswerSubmissionDto(q.Id, Guid.Parse(answers[q.Id]), null),
                (int)QuestionType.FindTheMistake => new AnswerSubmissionDto(q.Id, null, null, answers[q.Id]),
                _ => new AnswerSubmissionDto(q.Id, null, answers[q.Id])
            }).ToList();
        return ApiResponse<List<AnswerSubmissionDto>>.Ok(submissions);
    }
}
