using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.LearningCenter;

public sealed class LearningQuestionBank(IAppDbContext db)
{
    private readonly LearningCenterScope scope = new(db);

    public async Task<LearningOptionsDto> OptionsAsync(Guid actorId, CancellationToken ct)
    {
        var query = await scope.PackagesAsync(actorId, new(), ct);
        var packages = await query.OrderBy(p => p.Name).Select(p => new LearningPackageDto(p.Id, p.Name,
            p.TeacherId, p.Teacher.User.FullName, p.SubjectId, p.Subject.Name, p.TargetGrade)).ToListAsync(ct);
        var ids = packages.Select(p => p.Id).ToArray();
        var lessons = await db.Lessons.AsNoTracking().Where(l => ids.Contains(l.ContentSection.Term.PackageId))
            .OrderBy(l => l.Order).Select(l => new LearningLessonDto(l.Id, l.Title, l.ContentSection.Term.PackageId)).ToListAsync(ct);
        var lessonIds = lessons.Select(l => l.Id).ToArray();
        var concepts = await db.QuestionBankItems.AsNoTracking().Where(q => q.LearningLessonId.HasValue &&
                lessonIds.Contains(q.LearningLessonId.Value) && q.SupersededByQuestionId == null && q.LearningConcept != "")
            .Select(q => new LearningConceptChoice(q.LearningLessonId!.Value, q.LearningConcept)).Distinct().ToListAsync(ct);
        return new(packages, lessons, concepts);
    }

    public async Task<PagedResult<LearningQuestionDto>> ListAsync(Guid actorId, LearningQuestionFilter filter, CancellationToken ct)
    {
        var teacherId = await scope.TeacherAsync(actorId, ct);
        var questions = db.QuestionBankItems.AsNoTracking().Where(q => q.SupersededByQuestionId == null);
        if (teacherId.HasValue) questions = questions.Where(q => q.CreatedByTeacherId == teacherId);
        if (filter.TeacherId.HasValue) questions = questions.Where(q => q.CreatedByTeacherId == filter.TeacherId);
        if (filter.SubjectId.HasValue) questions = questions.Where(q => q.SubjectId == filter.SubjectId);
        if (filter.PackageId.HasValue) questions = questions.Where(q => q.LearningLesson != null && q.LearningLesson.ContentSection.Term.PackageId == filter.PackageId);
        if (filter.LessonId.HasValue) questions = questions.Where(q => q.LearningLessonId == filter.LessonId);
        if (filter.Difficulty.HasValue) questions = questions.Where(q => q.LearningDifficulty == filter.Difficulty);
        if (!string.IsNullOrWhiteSpace(filter.Concept)) questions = questions.Where(q => q.LearningConcept == filter.Concept);
        if (!string.IsNullOrWhiteSpace(filter.Search)) questions = questions.Where(q => q.Text.Contains(filter.Search) || q.LearningConcept.Contains(filter.Search) || q.Tags.Contains(filter.Search));
        var page = Math.Clamp(filter.Page, 1, 100000);
        var total = await questions.CountAsync(ct);
        var rows = await questions.Include(q => q.Options.Where(o => !o.IsRetired)).OrderByDescending(q => q.CreatedAt).ThenBy(q => q.Id)
            .Skip((page - 1) * 20).Take(20).ToListAsync(ct);
        return new(rows.Select(ToDto).ToList(), total, page, 20);
    }

    public async Task<Guid> SaveAsync(Guid actorId, QuestionSaveOperation operation, CancellationToken ct)
    {
        await using var transaction = operation.QuestionId.HasValue
            ? await db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var request = operation.Question;
        ValidateQuestion(request);
        var lesson = await scope.LessonAsync(actorId, request.LessonId, ct);
        QuestionBankItem? previous = operation.QuestionId.HasValue ? await scope.QuestionAsync(actorId, operation.QuestionId.Value, ct) : null;
        if (previous is not null && (previous.SupersededByQuestionId.HasValue || previous.Type != QuestionType.MCQ))
            throw new ArgumentException("يمكن تعديل أحدث نسخة من سؤال الاختيار من متعدد فقط.");
        if (previous is not null && previous.CreatedByTeacherId != lesson.ContentSection.Term.Package.TeacherId)
            throw new ArgumentException("اختر درسًا لنفس مدرس السؤال.");
        var question = new QuestionBankItem
        {
            Text = request.Text.Trim(), DefaultPoints = request.Points, Tags = request.Tags.Trim(),
            WrittenCorrection = request.Correction, LearningLessonId = lesson.Id,
            LearningConcept = request.Concept.Trim(), LearningDifficulty = request.Difficulty,
            SubjectId = lesson.ContentSection.Term.Package.SubjectId, CreatedByTeacherId = lesson.ContentSection.Term.Package.TeacherId,
            ImageUrl = previous?.ImageUrl, AudioUrl = previous?.AudioUrl, HintText = previous?.HintText,
            Options = request.Options.Select(o => new QuestionOption { Text = o.Text.Trim(), IsCorrect = o.IsCorrect }).ToList()
        };
        db.QuestionBankItems.Add(question);
        // Referenced questions remain immutable; existing exam links and attempt snapshots keep their original version.
        if (previous is not null) previous.SupersededByQuestionId = question.Id;
        Audit(new(actorId, question.Id, previous is null ? "LearningQuestionCreated" : "LearningQuestionVersioned",
            previous is null ? null : ToDto(previous), ToDto(question)));
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return question.Id;
    }

    public async Task ClassifyAsync(Guid actorId, QuestionClassification operation, CancellationToken ct)
    {
        var request = operation.Classification;
        if (string.IsNullOrWhiteSpace(request.Concept) || request.Concept.Length > 160 || request.Difficulty is < 1 or > 3)
            throw new ArgumentException("أدخل الفكرة ومستوى الصعوبة.");
        var question = await scope.QuestionAsync(actorId, operation.QuestionId, ct);
        var lesson = await scope.LessonAsync(actorId, request.LessonId, ct);
        if (question.CreatedByTeacherId != lesson.ContentSection.Term.Package.TeacherId || question.SubjectId != lesson.ContentSection.Term.Package.SubjectId)
            throw new ArgumentException("الدرس يجب أن ينتمي لنفس مدرس السؤال ومادته.");
        var before = new { question.LearningLessonId, question.LearningConcept, question.LearningDifficulty };
        question.LearningLessonId = lesson.Id;
        question.LearningConcept = request.Concept.Trim();
        question.LearningDifficulty = request.Difficulty;
        Audit(new(actorId, question.Id, "LearningQuestionClassified", before, request));
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Guid>> ImportAsync(Guid actorId, List<SaveLearningQuestion> questions, CancellationToken ct)
    {
        if (questions.Count is < 1 or > 100) throw new ArgumentException("يمكن استيراد 1 إلى 100 سؤال في المرة الواحدة.");
        foreach (var question in questions) ValidateQuestion(question);
        await using var transaction = await db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var ids = new List<Guid>();
        foreach (var question in questions) ids.Add(await SaveAsync(actorId, new(null, question), ct));
        await transaction.CommitAsync(ct);
        return ids;
    }

    public static LearningQuestionDto ToDto(QuestionBankItem q) => new(q.Id, q.Text, (int)q.Type, q.DefaultPoints,
        q.Tags, q.CreatedByTeacherId, q.SubjectId, q.LearningLessonId, q.LearningConcept, q.LearningDifficulty,
        q.WrittenCorrection, q.Options.Where(o => !o.IsRetired).Select(o => new LearningOptionDto(o.Id, o.Text, o.IsCorrect)).ToList());

    private static void ValidateQuestion(SaveLearningQuestion request)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 10000 || string.IsNullOrWhiteSpace(request.Concept) ||
            request.Concept.Length > 160 || request.Difficulty is < 1 or > 3 || request.Points is < 0.5m or > 100 ||
            request.Options.Count is < 2 or > 8 || request.Options.Count(o => o.IsCorrect) != 1 ||
            request.Options.Any(o => string.IsNullOrWhiteSpace(o.Text) || o.Text.Length > 4000))
            throw new ArgumentException("راجع نص السؤال والفكرة والدرجة، وحدد إجابة صحيحة واحدة.");
    }

    private void Audit(QuestionAudit change) => db.AuditLogs.Add(new AuditLog
    {
        Action = change.Action, EntityType = nameof(QuestionBankItem), EntityId = change.QuestionId, PerformedByUserId = change.ActorId,
        OldValues = change.Before is null ? null : JsonSerializer.Serialize(change.Before), NewValues = JsonSerializer.Serialize(change.After)
    });
    private sealed record QuestionAudit(Guid ActorId, Guid QuestionId, string Action, object? Before, object After);
}
public sealed record LearningQuestionFilter(Guid? PackageId = null, Guid? LessonId = null, int? Difficulty = null,
    string? Concept = null, string? Search = null, int Page = 1, Guid? TeacherId = null, Guid? SubjectId = null);
public sealed record QuestionSaveOperation(Guid? QuestionId, SaveLearningQuestion Question);
public sealed record QuestionClassification(Guid QuestionId, ClassifyLearningQuestion Classification);
