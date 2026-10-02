using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

internal sealed record TeacherReportContent(SalesTargetType Type, Guid Id, Guid CourseId,
    string Course, string Kind, string Name);

internal sealed class TeacherReportContentReader(IAppDbContext db)
{
    public async Task<IReadOnlyDictionary<(SalesTargetType, Guid), TeacherReportContent>> ReadAsync(
        Guid teacherId, CancellationToken ct)
    {
        var content = await db.Packages.AsNoTracking().Where(x => x.TeacherId == teacherId)
            .Select(x => new TeacherReportContent(SalesTargetType.Package, x.Id, x.Id, x.Name, "السنة / الباقة", x.Name)).ToListAsync(ct);
        content.AddRange(await db.Terms.AsNoTracking().Where(x => x.Package.TeacherId == teacherId)
            .Select(x => new TeacherReportContent(SalesTargetType.Term, x.Id, x.PackageId, x.Package.Name, "الترم / الكورس", x.Title)).ToListAsync(ct));
        content.AddRange(await db.ContentSections.AsNoTracking().Where(x => x.Term.Package.TeacherId == teacherId)
            .Select(x => new TeacherReportContent(SalesTargetType.ContentSection, x.Id, x.Term.PackageId, x.Term.Package.Name, "الشهر", x.Title)).ToListAsync(ct));
        content.AddRange(await db.Lessons.AsNoTracking().Where(x => x.ContentSection.Term.Package.TeacherId == teacherId)
            .Select(x => new TeacherReportContent(SalesTargetType.Lesson, x.Id, x.ContentSection.Term.PackageId, x.ContentSection.Term.Package.Name, "الحصة", x.Title)).ToListAsync(ct));
        content.AddRange(await db.LessonVideos.AsNoTracking().Where(x => x.Lesson.ContentSection.Term.Package.TeacherId == teacherId)
            .Select(x => new TeacherReportContent(SalesTargetType.SpecificVideo, x.Id, x.Lesson.ContentSection.Term.PackageId, x.Lesson.ContentSection.Term.Package.Name, "فيديو", x.Title)).ToListAsync(ct));
        content.AddRange(await db.PublicExamProducts.AsNoTracking().Where(x => x.TeacherId == teacherId)
            .Select(x => new TeacherReportContent(SalesTargetType.PublicExam, x.Id, x.Id, x.Exam.Title, "امتحان", x.Exam.Title)).ToListAsync(ct));
        return content.ToDictionary(x => (x.Type, x.Id));
    }

    public static (SalesTargetType, Guid)? Target(NaderGorge.Domain.Entities.StudentAccessGrant grant) => grant.GrantType switch
    {
        CodeType.Package when grant.PackageId.HasValue => (SalesTargetType.Package, grant.PackageId.Value),
        CodeType.Term when grant.TermId.HasValue => (SalesTargetType.Term, grant.TermId.Value),
        CodeType.Month when grant.ContentSectionId.HasValue => (SalesTargetType.ContentSection, grant.ContentSectionId.Value),
        CodeType.Lesson when grant.LessonId.HasValue => (SalesTargetType.Lesson, grant.LessonId.Value),
        CodeType.Video when grant.LessonVideoId.HasValue => (SalesTargetType.SpecificVideo, grant.LessonVideoId.Value),
        CodeType.Exam when grant.PublicExamProductId.HasValue => (SalesTargetType.PublicExam, grant.PublicExamProductId.Value),
        _ => null
    };
}
