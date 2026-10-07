using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public static class TeacherEssayGradingPolicy
{
    public const string SettingPrefix = "ManualEssayGradingTeacher:";

    // Read the authoritative setting on every dispatch/callback: a cached value
    // could allow an in-flight AI result to overwrite the teacher's review queue.
    public static IQueryable<Guid> ManualTeacherIds(IAppDbContext db) =>
        db.TeacherProfiles.Where(t => db.PlatformSettings.Any(s =>
            s.Key == SettingPrefix + t.Id.ToString() && s.Value == "true")).Select(t => t.Id);

    public static IQueryable<Guid> ManualHomeworkIds(IAppDbContext db) =>
        from homework in db.Homeworks
        join lesson in db.Lessons on homework.LessonId equals lesson.Id
        where ManualTeacherIds(db).Contains(lesson.ContentSection.Term.Package.TeacherId)
        select homework.Id;

    public static Task<bool> IsManualEssayAsync(IAppDbContext db, Guid essayId, CancellationToken ct) =>
        db.EssaySubmissions.AnyAsync(e => e.Id == essayId
            && ManualTeacherIds(db).Contains(e.Attempt.Exam.CreatedByTeacherId), ct);

    public static Task<bool> IsManualHomeworkAsync(IAppDbContext db, Guid submissionId, CancellationToken ct) =>
        db.HomeworkSubmissions.AnyAsync(s => s.Id == submissionId && ManualHomeworkIds(db).Contains(s.HomeworkId), ct);

    public static async Task<bool> HoldEssayForTeacherAsync(IAppDbContext db, Guid essayId, CancellationToken ct)
    {
        if (!await IsManualEssayAsync(db, essayId, ct)) return false;
        await db.EssaySubmissions.Where(e => e.Id == essayId && e.Status == EssaySubmissionStatus.WaitAI)
            .ExecuteUpdateAsync(update => update.SetProperty(e => e.Status, EssaySubmissionStatus.WaitTeacher)
                .SetProperty(e => e.AiNextRetryAt, (DateTime?)null), ct);
        return true;
    }
}
