using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.LearningCenter;

public sealed class LearningFormGenerator(IAppDbContext db)
{
    public async Task<List<GeneratedLearningForm>> GenerateAsync(Guid actorId, GenerateLearningForms request, CancellationToken ct)
    {
        Validate(request);
        var packages = await new LearningCenterScope(db).PackagesAsync(actorId, new(PackageId: request.PackageId), ct);
        var package = await packages.SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException();
        var existing = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.Id == request.RequestId, ct);
        var fingerprint = JsonSerializer.Serialize(request);
        if (existing is not null)
        {
            if (existing.PerformedByUserId != actorId || existing.Action != "LearningFormsGenerated" || existing.OldValues != fingerprint)
                throw new ArgumentException("معرف الطلب مستخدم لمواصفات مختلفة.");
            return JsonSerializer.Deserialize<List<GeneratedLearningForm>>(existing.NewValues!)!;
        }
        var questions = await db.QuestionBankItems.Include(q => q.Options).Where(q => q.LearningLesson != null &&
            q.LearningLesson.ContentSection.Term.PackageId == package.Id && q.CreatedByTeacherId == package.TeacherId &&
            q.SupersededByQuestionId == null && q.Type == QuestionType.MCQ && q.DefaultPoints > 0).ToListAsync(ct);
        var allocation = Allocate(questions, request);
        var forms = new List<GeneratedLearningForm>();
        for (var index = 0; index < request.Forms; index++)
        {
            var selected = allocation[index];
            var exam = new Exam { Title = $"{request.Title.Trim()} - نموذج {index + 1}",
                Description = "نماذج بنفس توزيع الأفكار والصعوبة والدرجات. يلزم ربط النموذج بالمحتوى قبل إتاحته للطلاب.",
                CreatedByTeacherId = package.TeacherId, TotalScore = selected.Sum(q => q.DefaultPoints),
                DurationMinutes = request.DurationMinutes, IsActive = false, IsMandatory = false };
            exam.PassingScore = Math.Round(exam.TotalScore * request.PassingPercent / 100, 2);
            exam.ExamQuestions = selected.Select((q, order) => new ExamQuestion { ExamId = exam.Id,
                QuestionBankItemId = q.Id, Points = q.DefaultPoints, Order = order + 1 }).ToList();
            db.Exams.Add(exam);
            forms.Add(new(exam.Id, exam.Title, selected.Count, exam.TotalScore));
        }
        db.AuditLogs.Add(new AuditLog { Id = request.RequestId, Action = "LearningFormsGenerated", EntityType = nameof(Exam),
            PerformedByUserId = actorId, OldValues = fingerprint, NewValues = JsonSerializer.Serialize(forms) });
        await db.SaveChangesAsync(ct);
        return forms;
    }

    public static List<QuestionBankItem>[] Allocate(List<QuestionBankItem> questions, GenerateLearningForms request)
    {
        var allocation = Enumerable.Range(0, request.Forms).Select(_ => new List<QuestionBankItem>()).ToArray();
        foreach (var row in request.Blueprint)
        {
            var candidates = questions.Where(q => q.LearningLessonId == row.LessonId && q.LearningConcept == row.Concept.Trim() &&
                q.LearningDifficulty == row.Difficulty && q.Options.Count(o => !o.IsRetired) >= 2 &&
                q.Options.Count(o => !o.IsRetired && o.IsCorrect) == 1)
                .GroupBy(q => q.DefaultPoints).OrderBy(g => g.Key).ToArray();
            var slots = candidates.SelectMany(g => g.OrderBy(_ => Guid.NewGuid()).Chunk(request.Forms)
                .Where(chunk => chunk.Length == request.Forms)).Take(row.Count).ToArray();
            if (slots.Length < row.Count)
                throw new ArgumentException($"أسئلة غير كافية للفكرة «{row.Concept}» بالصعوبة المطلوبة وبدرجات متساوية بين النماذج.");
            foreach (var slot in slots)
                for (var index = 0; index < request.Forms; index++) allocation[index].Add(slot[index]);
        }
        return allocation;
    }

    private static void Validate(GenerateLearningForms request)
    {
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 180 ||
            request.Forms is < 1 or > 5 || request.DurationMinutes is < 1 or > 180 || request.PassingPercent is < 1 or > 100 ||
            request.Blueprint.Count is < 1 or > 40 || request.Blueprint.Sum(r => r.Count) > 100 ||
            request.Blueprint.Any(r => r.Count is < 1 or > 50 || r.Difficulty is < 1 or > 3 || string.IsNullOrWhiteSpace(r.Concept)) ||
            request.Blueprint.Select(r => (r.LessonId, r.Concept.Trim(), r.Difficulty)).Distinct().Count() != request.Blueprint.Count)
            throw new ArgumentException("راجع مواصفات النماذج. الحد الأقصى 100 سؤال للنموذج، ولا يجوز تكرار نفس صف المواصفات.");
    }
}
