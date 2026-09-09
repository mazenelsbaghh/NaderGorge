using System.Text.Json;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;
using QuestionType = NaderGorge.Domain.Entities.QuestionType;

namespace NaderGorge.Application.Features.Assessments;

public sealed record AssessmentRevisionBinding(AssessmentDefinitionSnapshot Current, AssessmentRevisionPolicy Policy,
    IReadOnlyDictionary<Guid, Guid> OptionIds);
public sealed record AssessmentRevisionWrite(AssessmentAttemptRevision Revised, AssessmentRevisionBinding Binding,
    Guid ActorId, Guid OperationId);

public sealed class AssessmentRevisionPersistence(IAppDbContext db)
{
    public void ApplyHomework(HomeworkSubmission submission, AssessmentRevisionWrite write)
    {
        var before = JsonSerializer.Serialize(new
        {
            submission.DefinitionSnapshotJson, submission.OverallScore, submission.Status, submission.SubmittedAt,
            submission.GradedAt, submission.StartedAt, submission.AssistantReviewerId,
            Answers = submission.Answers.Select(a => new { a.Id, a.QuestionId, a.ProvidedAnswer, a.ScoreReceived })
        });
        var previous = AssessmentDefinitionSnapshot.Read(submission.DefinitionSnapshotJson!, "homework", submission.HomeworkId);
        var submitted = submission.Status != SubmissionStatus.InProgress || previous.Revision is not null;
        var definition = write.Revised.Definition with { RevisionId = write.OperationId };
        foreach (var grade in write.Revised.Grades.Answers)
        {
            var answer = submission.Answers.SingleOrDefault(a => a.QuestionId == grade.QuestionId);
            if (answer is null)
            {
                answer = new HomeworkAnswer { HomeworkSubmissionId = submission.Id, QuestionId = grade.QuestionId };
                submission.Answers.Add(answer);
                db.HomeworkAnswers.Add(answer);
            }
            answer.ScoreReceived = grade.Excluded ? 0 : grade.AwardedPoints is decimal points ? checked((int)points) : null;
        }
        var grades = write.Revised.Grades;
        submission.OverallScore = Score(write.Revised);
        submission.PassingScoreSnapshot = definition.PassingScore ?? 0;
        submission.TotalScoreSnapshot = definition.TotalScore;
        submission.Status = !submitted || grades.RequiresCompletion ? SubmissionStatus.InProgress
            : grades.RequiresReview ? SubmissionStatus.PendingReview : SubmissionStatus.Graded;
        submission.Evaluation = submission.Status == SubmissionStatus.Graded
            ? Evaluation(write.Revised, submission.OverallScore) : null;
        submission.GradedAt = submission.Status == SubmissionStatus.Graded ? DateTime.UtcNow : null;
        if (grades.RequiresCompletion) submission.SubmittedAt = null;
        submission.DefinitionSnapshotJson = (submitted ? definition : definition with { Revision = null }).ToJson();
        Audit(submission.Id, "Homework", before, write);
    }

    public void ApplyExam(StudentExamAttempt attempt, IReadOnlyList<EssaySubmission> essays, AssessmentRevisionWrite write)
    {
        var before = JsonSerializer.Serialize(new
        {
            attempt.DefinitionSnapshotJson, attempt.ScoreAchieved, attempt.IsPassed, attempt.Evaluation,
            attempt.StartedAt, attempt.IsTimeExpired,
            Answers = attempt.Answers.Select(a => new { a.Id, a.ExamQuestionId, a.SelectedOptionId, a.SubmittedText, a.PointsAwarded, a.IsCorrect }),
            Essays = essays.Select(e => new { e.Id, e.QuestionId, e.AnswerText, e.AudioUrl, e.TeacherFinalScore, e.Status })
        });
        var original = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", attempt.ExamId);
        var submitted = original.Revision is not null || attempt.Evaluation is not null || essays.Count > 0
            || attempt.Answers.Any(a => a.SelectedOptionId.HasValue || !string.IsNullOrWhiteSpace(a.SubmittedText));
        var bound = BindExamDefinition(write) with { RevisionId = write.OperationId };
        foreach (var grade in write.Revised.Grades.Answers)
            ApplyExamAnswer(attempt, grade, new(original, bound, essays, write.Binding.OptionIds));
        var grades = write.Revised.Grades;
        attempt.ScoreAchieved = Score(write.Revised);
        attempt.IsPassed = submitted && !grades.RequiresCompletion && !grades.RequiresReview && !attempt.IsTimeExpired
            && attempt.ScoreAchieved >= (bound.PassingScore ?? 0);
        attempt.Evaluation = !submitted || grades.RequiresCompletion ? null
            : grades.RequiresReview ? "قيد التصحيح" : Evaluation(write.Revised, attempt.ScoreAchieved);
        if (submitted && grades.RequiresCompletion) attempt.StartedAt = null;
        attempt.DefinitionSnapshotJson = (submitted ? bound : bound with { Revision = null }).ToJson();
        Audit(attempt.Id, "Exam", before, write with { Revised = write.Revised with { Definition = bound } });
    }

    private sealed record ExamAnswerRevisionContext(AssessmentDefinitionSnapshot Original, AssessmentDefinitionSnapshot Bound,
        IReadOnlyList<EssaySubmission> Essays, IReadOnlyDictionary<Guid, Guid> OptionIds);

    private void ApplyExamAnswer(StudentExamAttempt attempt, RevisionAnswer grade, ExamAnswerRevisionContext context)
    {
        var answer = attempt.Answers.SingleOrDefault(a => a.ExamQuestionId == grade.QuestionId);
        if (answer is null)
        {
            answer = new StudentAnswer { StudentExamAttemptId = attempt.Id, ExamQuestionId = grade.QuestionId };
            attempt.Answers.Add(answer);
            db.StudentAnswers.Add(answer);
        }
        var original = context.Original.Questions.SingleOrDefault(q => q.Id == grade.QuestionId);
        var revised = context.Bound.Questions.Single(q => q.Id == grade.QuestionId);
        if (original is not null && original.BankQuestionId != revised.BankQuestionId && answer.SelectedOptionId is Guid selectedId)
        {
            answer.SubmittedText ??= original.Options.SingleOrDefault(o => o.Id == selectedId)?.Text;
            if (context.OptionIds.TryGetValue(selectedId, out var replacementId)) answer.SelectedOptionId = replacementId;
        }
        answer.PointsAwarded = grade.Excluded ? 0 : grade.AwardedPoints ?? 0;
        answer.IsCorrect = !grade.Excluded && grade.AwardedPoints.HasValue && grade.AwardedPoints >= grade.MaximumPoints;
        if (original is null || original.Type != (int)QuestionType.Essay) return;
        foreach (var essay in context.Essays.Where(e => e.QuestionId == original.BankQuestionId))
        {
            essay.QuestionId = revised.BankQuestionId;
            essay.TeacherFinalScore = grade.Excluded ? 0 : grade.AwardedPoints;
            // A result from the previous rubric must never overwrite the explicitly requested revision.
            essay.Status = grade.Excluded || grade.AwardedPoints.HasValue
                ? EssaySubmissionStatus.TeacherGraded : EssaySubmissionStatus.WaitTeacher;
        }
    }

    private static AssessmentDefinitionSnapshot BindExamDefinition(AssessmentRevisionWrite write)
    {
        var current = write.Binding.Current.Questions.ToDictionary(q => q.Id);
        var grades = write.Revised.Grades.Answers.ToDictionary(a => a.QuestionId);
        var questions = write.Revised.Definition.Questions.Select(question =>
        {
            if (!current.TryGetValue(question.Id, out var authored)
                || (grades[question.Id].ManuallyGraded && write.Binding.Policy.ManualGrades == ManualGradePolicy.Preserve))
                return question;
            return question with { BankQuestionId = authored.BankQuestionId, Options = authored.Options };
        }).ToArray();
        var assignedIds = questions.Select(q => q.Id).ToHashSet();
        return write.Revised.Definition with
        { Questions = questions, ReserveQuestions = write.Binding.Current.Questions.Where(q => !assignedIds.Contains(q.Id)).ToArray() };
    }

    private static decimal Score(AssessmentAttemptRevision revision) => revision.Grades.ScaledScore(revision.Definition.TotalScore);

    private static string Evaluation(AssessmentAttemptRevision revision, decimal score) =>
        GradingEvaluationService.DetermineEvaluation(score, revision.Definition.PassingScore ?? 0, revision.Definition.TotalScore);

    private void Audit(Guid attemptId, string kind, string before, AssessmentRevisionWrite write) => db.AuditLogs.Add(new AuditLog
    {
        Action = "AssessmentAttemptRegraded", EntityType = kind, EntityId = attemptId,
        PerformedByUserId = write.ActorId, CorrelationId = write.OperationId.ToString(), OldValues = before,
        NewValues = JsonSerializer.Serialize(write.Revised), Reason = "تطبيق اختيارات إعادة التصحيح المؤكدة دون حذف إجابات الطالب"
    });
}
