using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;
using QuestionType = NaderGorge.Domain.Entities.QuestionType;

namespace NaderGorge.Application.Features.Assessments;

public record AssessmentEditorDto(AssessmentDefinitionSnapshot Definition, int AttemptCount, string RevisionToken);
public record AssessmentRevisionPreviewDto(string RevisionToken, int AttemptCount, int AddedQuestions,
    int RemovedQuestions, AssessmentRevisionPolicy Policy, IReadOnlyList<AssessmentAttemptImpact> Attempts);
public record AssessmentAttemptImpact(Guid AttemptId, decimal PreviousScore, decimal? RevisedScore,
    bool RequiresCompletion, bool RequiresReview);
public record GetAssessmentEditorQuery(AssessmentTarget Target) : IRequest<ApiResponse<AssessmentEditorDto>>;
public record PreviewAssessmentRevisionQuery(AssessmentTarget Target, AssessmentDefinitionSnapshot Definition,
    AssessmentRevisionPolicy Policy) : IRequest<ApiResponse<AssessmentRevisionPreviewDto>>;
public record SaveAssessmentRevisionCommand(AssessmentTarget Target, AssessmentDefinitionSnapshot Definition,
    AssessmentRevisionPolicy Policy, string RevisionToken, Guid OperationId, Guid? SubjectId = null, bool ConfirmPreviousAttempts = false)
    : IRequest<ApiResponse<AssessmentEditorDto>>;

public class AssessmentRevisionCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    : IRequestHandler<GetAssessmentEditorQuery, ApiResponse<AssessmentEditorDto>>,
      IRequestHandler<PreviewAssessmentRevisionQuery, ApiResponse<AssessmentRevisionPreviewDto>>,
      IRequestHandler<SaveAssessmentRevisionCommand, ApiResponse<AssessmentEditorDto>>
{
    public async Task<ApiResponse<AssessmentEditorDto>> Handle(GetAssessmentEditorQuery request, CancellationToken ct)
    {
        if (!await AssessmentAccess.Allowed(db, auth, request.Target, ct))
            return ApiResponse<AssessmentEditorDto>.Fail("غير مصرح بتعديل هذا الواجب أو الامتحان.");
        var workspace = await Load(request.Target, ct);
        return workspace is null ? ApiResponse<AssessmentEditorDto>.Fail("الواجب أو الامتحان غير موجود.")
            : ApiResponse<AssessmentEditorDto>.Ok(workspace.Editor());
    }

    public async Task<ApiResponse<AssessmentRevisionPreviewDto>> Handle(PreviewAssessmentRevisionQuery request, CancellationToken ct)
    {
        if (!await AssessmentAccess.Allowed(db, auth, request.Target, ct))
            return ApiResponse<AssessmentRevisionPreviewDto>.Fail("غير مصرح بتعديل هذا الواجب أو الامتحان.");
        var workspace = await Load(request.Target, ct);
        if (workspace is null) return ApiResponse<AssessmentRevisionPreviewDto>.Fail("الواجب أو الامتحان غير موجود.");
        var editor = workspace.Editor();
        var validation = ValidateDraft(request.Target, request.Definition, request.Policy);
        if (validation is not null) return ApiResponse<AssessmentRevisionPreviewDto>.Fail(validation);
        var ownershipError = await ValidateIdentities(workspace, request.Definition, ct);
        if (ownershipError is not null) return ApiResponse<AssessmentRevisionPreviewDto>.Fail(ownershipError);
        var currentIds = editor.Definition.Questions.Select(q => q.Id).ToHashSet();
        var proposedIds = request.Definition.Questions.Select(q => q.Id).ToHashSet();
        var impacts = request.Policy.PreviousAttempts == PreviousAttemptsPolicy.Preserve ? workspace.PreservedImpacts()
            : workspace.PrepareRevisions(new(editor.Definition, request.Definition, request.Policy))
                .Select(revision => revision.Impact(request.Policy)).ToArray();
        return ApiResponse<AssessmentRevisionPreviewDto>.Ok(new(PreviewToken(editor, request.Definition, request.Policy), editor.AttemptCount,
            proposedIds.Except(currentIds).Count(), currentIds.Except(proposedIds).Count(), request.Policy, impacts));
    }

    public Task<ApiResponse<AssessmentEditorDto>> Handle(SaveAssessmentRevisionCommand request, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ClearTrackedChanges();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, retryCt);
            var response = await SaveOnce(request, retryCt);
            if (response.Success) await transaction.CommitAsync(retryCt);
            return response;
        }, ct);

    private async Task<ApiResponse<AssessmentEditorDto>> SaveOnce(SaveAssessmentRevisionCommand request, CancellationToken ct)
    {
        if (!await AssessmentAccess.Allowed(db, auth, request.Target, ct))
            return ApiResponse<AssessmentEditorDto>.Fail("غير مصرح بتعديل هذا الواجب أو الامتحان.");
        var validation = ValidateDraft(request.Target, request.Definition, request.Policy);
        if (validation is not null) return ApiResponse<AssessmentEditorDto>.Fail(validation);
        if (request.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.RevisionToken))
            return ApiResponse<AssessmentEditorDto>.Fail("عاين تأثير التعديل قبل تأكيد الحفظ.");
        var workspace = await Load(request.Target, ct);
        if (workspace is null) return ApiResponse<AssessmentEditorDto>.Fail("الواجب أو الامتحان غير موجود.");
        var fingerprint = Hash(JsonSerializer.Serialize(request));
        var previousOperation = await db.AuditLogs.SingleOrDefaultAsync(a => a.Id == request.OperationId, ct);
        if (previousOperation is not null)
            return previousOperation.Action == "AssessmentDefinitionRevised" && previousOperation.RequestId == fingerprint
                && previousOperation.PerformedByUserId == request.Target.ActorId
                ? ApiResponse<AssessmentEditorDto>.Ok(workspace.Editor(), "تم حفظ هذه العملية بالفعل.")
                : ApiResponse<AssessmentEditorDto>.Fail("معرّف العملية مستخدم لتعديل آخر. افتح معاينة جديدة.");
        if (PreviewToken(workspace.Editor(), request.Definition, request.Policy) != request.RevisionToken)
            return ApiResponse<AssessmentEditorDto>.Fail("تغيّرت الأسئلة أو المحاولات أثناء التعديل. أعد المعاينة قبل الحفظ.");
        if (request.Policy.PreviousAttempts == PreviousAttemptsPolicy.Regrade && !request.ConfirmPreviousAttempts)
            return ApiResponse<AssessmentEditorDto>.Fail("أكد صراحة تطبيق إعادة التصحيح على المحاولات السابقة بعد مراجعة تأثيرها.");
        var ownershipError = await ValidateIdentities(workspace, request.Definition, ct);
        if (ownershipError is not null) return ApiResponse<AssessmentEditorDto>.Fail(ownershipError);
        var originalDefinition = workspace.Definition();
        IReadOnlyList<PreparedRevision> revisions = request.Policy.PreviousAttempts == PreviousAttemptsPolicy.Regrade
            ? workspace.PrepareRevisions(new(originalDefinition, request.Definition, request.Policy)) : [];
        foreach (var revision in revisions)
            if (!await AssessmentAccess.CanGrade(db, auth, request.Target with { AttemptId = revision.AttemptId }, ct))
                return ApiResponse<AssessmentEditorDto>.Fail("لا تملك صلاحية إعادة تصحيح جميع المحاولات المتأثرة.");
        var previousQuestionIds = (workspace.Exam?.ExamQuestions.Select(q => q.Id)
            ?? workspace.Homework!.Questions.Select(q => q.Id)).ToHashSet();
        var previousOptionIds = workspace.Exam?.ExamQuestions.SelectMany(q => q.Question.Options)
            .Select(o => o.Id).ToHashSet() ?? [];
        var authoredDefinition = request.Definition;
        if (workspace.Exam is not null)
        {
            var subjectId = workspace.Exam.ExamQuestions.FirstOrDefault()?.Question.SubjectId ?? request.SubjectId;
            if (authoredDefinition.Questions.Length > 0 && (subjectId is null
                || !await db.Subjects.AnyAsync(s => s.Id == subjectId, ct)))
                return ApiResponse<AssessmentEditorDto>.Fail("اختر مادة صحيحة لأسئلة الامتحان.");
            workspace.CaptureAttempts();
            authoredDefinition = await IsolateSharedQuestions(workspace.Exam, authoredDefinition, ct);
            try { AssessmentDefinitionWriter.ApplyExam(workspace.Exam, authoredDefinition,
                new(workspace.Exam.CreatedByTeacherId, subjectId ?? Guid.Empty)); }
            catch (ArgumentException error) { return ApiResponse<AssessmentEditorDto>.Fail(error.Message); }
        }
        else
        {
            workspace.CaptureAttempts();
            try { AssessmentDefinitionWriter.ApplyHomework(workspace.Homework!, authoredDefinition); }
            catch (ArgumentException error) { return ApiResponse<AssessmentEditorDto>.Fail(error.Message); }
        }
        RegisterNewRows(workspace, previousQuestionIds, previousOptionIds);
        var optionIds = request.Definition.Questions.SelectMany(question => question.Options.Zip(
                authoredDefinition.Questions.Single(q => q.Id == question.Id).Options))
            .Where(pair => pair.First.Id != pair.Second.Id).ToDictionary(pair => pair.First.Id, pair => pair.Second.Id);
        workspace.ApplyRevisions(revisions, new(workspace.Definition(), request.Policy, optionIds), request, db);
        db.AuditLogs.Add(new AuditLog
        {
            Id = request.OperationId, Action = "AssessmentDefinitionRevised", EntityType = request.Target.Kind.ToString(),
            EntityId = request.Target.AssessmentId, PerformedByUserId = request.Target.ActorId, RequestId = fingerprint,
            OldValues = originalDefinition.ToJson(),
            NewValues = JsonSerializer.Serialize(new { Definition = workspace.Definition(), request.Policy }),
            Reason = request.Policy.PreviousAttempts == PreviousAttemptsPolicy.Preserve
                ? "حفظ تعريف جديد مع الاحتفاظ بالمحاولات السابقة دون تغيير درجاتها" : "حفظ تعريف جديد وتطبيق اختيارات إعادة التصحيح المؤكدة"
        });
        await db.SaveChangesAsync(ct);
        return ApiResponse<AssessmentEditorDto>.Ok(workspace.Editor(), "تم حفظ التعديلات طبقًا لاختيارات تأثيرها على المحاولات.");
    }

    private void RegisterNewRows(RevisionWorkspace workspace, HashSet<Guid> previousQuestionIds, HashSet<Guid> previousOptionIds)
    {
        if (workspace.Homework is not null)
        {
            db.HomeworkQuestions.AddRange(workspace.Homework.Questions.Where(q => !previousQuestionIds.Contains(q.Id)));
            return;
        }
        db.ExamQuestions.AddRange(workspace.Exam!.ExamQuestions.Where(q => !previousQuestionIds.Contains(q.Id)));
        db.QuestionOptions.AddRange(workspace.Exam.ExamQuestions.SelectMany(q => q.Question.Options)
            .Where(o => !previousOptionIds.Contains(o.Id)));
    }

    private static string? ValidateDraft(AssessmentTarget target, AssessmentDefinitionSnapshot definition, AssessmentRevisionPolicy policy)
    {
        if (!Enum.IsDefined(target.Kind) || !Enum.IsDefined(policy.PreviousAttempts) || !Enum.IsDefined(policy.RemovedQuestions)
            || !Enum.IsDefined(policy.AddedQuestions) || !Enum.IsDefined(policy.ManualGrades)
            || !Enum.IsDefined(policy.ScoreDecrease)) return "اختيارات التعديل غير صحيحة.";
        try { AssessmentDefinitionWriter.Validate(definition, target.Kind == AssessmentKind.Homework ? "homework" : "exam", target.AssessmentId); }
        catch (ArgumentException error) { return error.Message; }
        return null;
    }

    private async Task<string?> ValidateIdentities(RevisionWorkspace workspace, AssessmentDefinitionSnapshot definition, CancellationToken ct)
    {
        var ids = definition.Questions.Select(q => q.Id).ToArray();
        if (workspace.Homework is not null)
        {
            var previousTypes = workspace.Homework.Questions.ToDictionary(q => q.Id, q => (int)q.QuestionType);
            if (definition.Questions.Any(q => previousTypes.TryGetValue(q.Id, out var type) && type != q.Type))
                return "تغيير نوع السؤال يحتاج معرّف سؤال جديد لتطبيق سياسة الأسئلة المحذوفة والجديدة.";
            return await db.HomeworkQuestions.AnyAsync(q => ids.Contains(q.Id) && q.HomeworkId != definition.AssessmentId, ct)
                ? "أحد الأسئلة ينتمي لواجب آخر." : null;
        }
        if (await db.ExamQuestions.AnyAsync(q => ids.Contains(q.Id) && q.ExamId != definition.AssessmentId, ct))
            return "أحد الأسئلة ينتمي لامتحان آخر.";
        var existingTypes = workspace.Exam!.ExamQuestions.ToDictionary(q => q.Id, q => (int)q.Question.Type);
        if (definition.Questions.Any(q => existingTypes.TryGetValue(q.Id, out var type) && type != q.Type))
            return "تغيير نوع السؤال يحتاج معرّف سؤال جديد لتطبيق سياسة الأسئلة المحذوفة والجديدة.";
        var optionIds = definition.Questions.SelectMany(q => q.Options).Select(o => o.Id).ToArray();
        if (optionIds.Distinct().Count() != optionIds.Length) return "لا يمكن مشاركة معرّف اختيار بين سؤالين.";
        var existingOptions = await db.QuestionOptions.Where(o => optionIds.Contains(o.Id))
            .Select(o => new { o.Id, o.QuestionBankItemId }).ToListAsync(ct);
        var owners = workspace.Exam!.ExamQuestions.ToDictionary(q => q.Id, q => q.QuestionBankItemId);
        foreach (var question in definition.Questions)
        {
            var questionOptions = question.Options.Select(o => o.Id).ToHashSet();
            if (existingOptions.Any(o => questionOptions.Contains(o.Id)
                && (!owners.TryGetValue(question.Id, out var bankId) || bankId != o.QuestionBankItemId)))
                return "أحد الاختيارات ينتمي لسؤال آخر.";
        }
        return null;
    }

    private async Task<AssessmentDefinitionSnapshot> IsolateSharedQuestions(Exam exam, AssessmentDefinitionSnapshot definition, CancellationToken ct)
    {
        var bankIds = exam.ExamQuestions.Select(q => q.QuestionBankItemId).ToArray();
        var sharedBankIds = await db.ExamQuestions.Where(q => q.ExamId != exam.Id && bankIds.Contains(q.QuestionBankItemId))
            .Select(q => q.QuestionBankItemId).Distinct().ToListAsync(ct);
        var revised = definition.Questions.ToArray();
        for (var index = 0; index < revised.Length; index++)
        {
            var existing = exam.ExamQuestions.SingleOrDefault(q => q.Id == revised[index].Id);
            if (existing is null || !sharedBankIds.Contains(existing.QuestionBankItemId)) continue;
            var detached = AssessmentDefinitionSnapshot.ResolveExam(exam,
                (definition with { Questions = [revised[index]] }).ToJson()).ExamQuestions.Single().Question;
            detached.Id = Guid.NewGuid();
            detached.CreatedByTeacherId = existing.Question.CreatedByTeacherId;
            detached.SubjectId = existing.Question.SubjectId;
            detached.Tags = existing.Question.Tags;
            foreach (var option in detached.Options) { option.Id = Guid.NewGuid(); option.QuestionBankItemId = detached.Id; }
            db.QuestionBankItems.Add(detached);
            existing.Question = detached;
            existing.QuestionBankItemId = detached.Id;
            revised[index] = revised[index] with { BankQuestionId = detached.Id,
                Options = detached.Options.Select(o => new AssessmentOptionSnapshot(o.Id, o.Text, o.IsCorrect)).ToArray() };
        }
        return definition with { Questions = revised };
    }

    private async Task<RevisionWorkspace?> Load(AssessmentTarget target, CancellationToken ct)
    {
        if (target.Kind == AssessmentKind.Exam)
        {
            var exam = await db.Exams.Include(e => e.ExamQuestions).ThenInclude(q => q.Question).ThenInclude(q => q.Options)
                .SingleOrDefaultAsync(e => e.Id == target.AssessmentId, ct);
            if (exam is null) return null;
            var attempts = await db.StudentExamAttempts.Include(a => a.Answers)
                .Where(a => a.ExamId == exam.Id).OrderBy(a => a.Id).ToListAsync(ct);
            var attemptIds = attempts.Select(a => a.Id).ToArray();
            var essays = await db.EssaySubmissions.Where(e => attemptIds.Contains(e.StudentExamAttemptId))
                .OrderBy(e => e.Id).ToListAsync(ct);
            var essayIds = essays.Select(e => e.Id).ToArray();
            var manual = await db.AuditLogs.Where(a => a.EntityId.HasValue
                && ((a.Action == "AssessmentManuallyGraded" && attemptIds.Contains(a.EntityId.Value))
                    || (a.Action == "EssayManuallyGraded" && essayIds.Contains(a.EntityId.Value))))
                .Select(a => a.EntityId!.Value).Distinct().ToListAsync(ct);
            return new(exam, null, attempts, [], new(essays, manual.ToHashSet()));
        }
        var homework = await db.Homeworks.Include(h => h.Questions).SingleOrDefaultAsync(h => h.Id == target.AssessmentId, ct);
        if (homework is null) return null;
        var submissions = await db.HomeworkSubmissions.Include(s => s.Answers).Where(s => s.HomeworkId == homework.Id)
            .OrderBy(s => s.Id).ToListAsync(ct);
        return new(null, homework, [], submissions, new([], []));
    }

    private static string Hash(string content)
    {
        using var document = JsonDocument.Parse(content);
        using var canonical = new MemoryStream();
        using (var writer = new Utf8JsonWriter(canonical)) WriteCanonicalJson(writer, document.RootElement);
        return Convert.ToHexString(SHA256.HashData(canonical.ToArray()));
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); WriteCanonicalJson(writer, property.Value); }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray()) WriteCanonicalJson(writer, child);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                // PostgreSQL restores numeric scale (e.g. 20.00); it is not a content revision.
                writer.WriteRawValue(element.GetDecimal().ToString("G29", CultureInfo.InvariantCulture));
                break;
            default: element.WriteTo(writer); break;
        }
    }

    private static string PreviewToken(AssessmentEditorDto editor, AssessmentDefinitionSnapshot definition, AssessmentRevisionPolicy policy) =>
        Hash(JsonSerializer.Serialize(new { editor.RevisionToken, Definition = definition, Policy = policy }));

    private sealed record GradingEvidence(List<EssaySubmission> Essays, HashSet<Guid> ManualEntityIds);

    private sealed record PreparedRevision(Guid AttemptId, decimal PreviousScore, AssessmentAttemptRevision Revised)
    {
        public AssessmentAttemptImpact Impact(AssessmentRevisionPolicy policy)
        {
            if (policy.PreviousAttempts == PreviousAttemptsPolicy.Preserve)
                return new(AttemptId, PreviousScore, PreviousScore, false, false);
            var grades = Revised.Grades;
            decimal? revisedScore = grades.RequiresCompletion || grades.RequiresReview ? null
                : grades.ScaledScore(Revised.Definition.TotalScore);
            return new(AttemptId, PreviousScore, revisedScore, grades.RequiresCompletion, grades.RequiresReview);
        }
    }

    private sealed record RevisionWorkspace(Exam? Exam, HomeworkEntity? Homework,
        List<StudentExamAttempt> Attempts, List<HomeworkSubmission> Submissions, GradingEvidence Evidence)
    {
        public AssessmentDefinitionSnapshot Definition() => Exam is not null
            ? AssessmentDefinitionSnapshot.FromExam(Exam) : AssessmentDefinitionSnapshot.FromHomework(Homework!);

        public AssessmentEditorDto Editor()
        {
            var definition = Definition();
            var revision = JsonSerializer.Serialize(new
            {
                Definition = definition,
                Attempts = Attempts.Select(a => new { a.Id, Snapshot = SavedJson(a.DefinitionSnapshotJson), a.ScoreAchieved, a.IsPassed, a.Evaluation, a.StartedAt, a.IsTimeExpired,
                    Answers = a.Answers.OrderBy(x => x.Id).Select(x => new { x.Id, x.ExamQuestionId, x.SelectedOptionId, x.SubmittedText, x.PointsAwarded, x.HintUsed }) }),
                Submissions = Submissions.Select(s => new { s.Id, Snapshot = SavedJson(s.DefinitionSnapshotJson), s.Status, s.OverallScore, s.StartedAt, s.SubmittedAt,
                    s.AssistantReviewerId,
                    Answers = s.Answers.OrderBy(x => x.Id).Select(x => new { x.Id, x.QuestionId, x.ProvidedAnswer, x.ScoreReceived }) }),
                Essays = Evidence.Essays.Select(e => new { e.Id, e.QuestionId, e.StudentExamAttemptId, e.Status, e.TeacherFinalScore, e.GradedByTeacherId }),
                ManuallyGraded = Evidence.ManualEntityIds.OrderBy(id => id)
            });
            return new(definition, Attempts.Count + Submissions.Count, Hash(revision));
        }

        private static JsonElement? SavedJson(string? snapshot) => snapshot is null ? null : JsonSerializer.Deserialize<JsonElement>(snapshot);

        public AssessmentAttemptImpact[] PreservedImpacts() => Attempts
            .Select(a => new AssessmentAttemptImpact(a.Id, a.ScoreAchieved, a.ScoreAchieved, false, false))
            .Concat(Submissions.Select(s => new AssessmentAttemptImpact(s.Id, s.OverallScore, s.OverallScore, false, false))).ToArray();

        public IReadOnlyList<PreparedRevision> PrepareRevisions(AssessmentDefinitionChange change)
        {
            var revisions = new List<PreparedRevision>();
            foreach (var attempt in Attempts)
            {
                var assigned = attempt.Answers.Select(a => a.ExamQuestionId).ToHashSet();
                var previous = attempt.DefinitionSnapshotJson is null
                    ? AssessmentDefinitionSnapshot.FromExam(Exam!, Exam!.ExamQuestions.Where(q => assigned.Contains(q.Id)))
                    : AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson, "exam", attempt.ExamId);
                var answers = previous.Questions.Select(q => ExamAnswer(attempt, q)).ToArray();
                revisions.Add(new(attempt.Id, attempt.ScoreAchieved, AssessmentAttemptRegrader.Regrade(previous, answers,
                    change with { PreviousScore = attempt.ScoreAchieved })));
            }
            foreach (var submission in Submissions)
            {
                var previous = submission.DefinitionSnapshotJson is null ? AssessmentDefinitionSnapshot.FromHomework(Homework!)
                    : AssessmentDefinitionSnapshot.Read(submission.DefinitionSnapshotJson, "homework", submission.HomeworkId);
                var answers = previous.Questions.Select(q => HomeworkAnswer(submission, q)).ToArray();
                revisions.Add(new(submission.Id, submission.OverallScore, AssessmentAttemptRegrader.Regrade(previous, answers,
                    change with { PreviousScore = submission.OverallScore })));
            }
            return revisions;
        }

        public void ApplyRevisions(IReadOnlyList<PreparedRevision> revisions, AssessmentRevisionBinding binding,
            SaveAssessmentRevisionCommand request, IAppDbContext db)
        {
            var persistence = new AssessmentRevisionPersistence(db);
            foreach (var revision in revisions)
            {
                var write = new AssessmentRevisionWrite(revision.Revised, binding, request.Target.ActorId, request.OperationId);
                if (Exam is not null)
                    persistence.ApplyExam(Attempts.Single(a => a.Id == revision.AttemptId),
                        Evidence.Essays.Where(e => e.StudentExamAttemptId == revision.AttemptId).ToArray(), write);
                else persistence.ApplyHomework(Submissions.Single(s => s.Id == revision.AttemptId), write);
            }
        }

        private RecordedAssessmentAnswer ExamAnswer(StudentExamAttempt attempt, AssessmentQuestionSnapshot question)
        {
            var answer = attempt.Answers.SingleOrDefault(a => a.ExamQuestionId == question.Id);
            var essay = Evidence.Essays.Where(e => e.StudentExamAttemptId == attempt.Id && e.QuestionId == question.BankQuestionId)
                .OrderByDescending(e => e.UpdatedAt ?? e.CreatedAt).ThenByDescending(e => e.Id).FirstOrDefault();
            var manual = Evidence.ManualEntityIds.Contains(attempt.Id)
                || (essay is not null && (essay.GradedByTeacherId.HasValue || Evidence.ManualEntityIds.Contains(essay.Id)));
            return new(question.Id, essay?.AnswerText ?? answer?.SubmittedText, answer?.SelectedOptionId,
                question.Type == (int)QuestionType.Essay ? essay?.TeacherFinalScore : answer?.PointsAwarded ?? 0, manual);
        }

        private static RecordedAssessmentAnswer HomeworkAnswer(HomeworkSubmission submission, AssessmentQuestionSnapshot question)
        {
            var answer = submission.Answers.SingleOrDefault(a => a.QuestionId == question.Id);
            return new(question.Id, answer?.ProvidedAnswer, null,
                question.Type == (int)QuestionType.Essay ? answer?.ScoreReceived : answer?.ScoreReceived ?? 0,
                submission.AssistantReviewerId.HasValue);
        }

        public void CaptureAttempts()
        {
            foreach (var attempt in Attempts)
            {
                var assignedIds = attempt.Answers.Select(a => a.ExamQuestionId).ToHashSet();
                attempt.DefinitionSnapshotJson ??= AssessmentDefinitionSnapshot.FromExam(Exam!,
                    Exam!.ExamQuestions.Where(q => assignedIds.Contains(q.Id))).ToJson();
            }
            foreach (var submission in Submissions)
            {
                submission.DefinitionSnapshotJson ??= AssessmentDefinitionSnapshot.FromHomework(Homework!).ToJson();
                var saved = AssessmentDefinitionSnapshot.Read(submission.DefinitionSnapshotJson, "homework", submission.HomeworkId);
                submission.PassingScoreSnapshot ??= saved.PassingScore ?? 0;
                submission.TotalScoreSnapshot ??= saved.TotalScore;
            }
        }
    }
}
