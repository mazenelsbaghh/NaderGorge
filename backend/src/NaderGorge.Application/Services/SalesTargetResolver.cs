using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class SalesTargetResolver : ISalesTargetResolver
{
    private readonly IAppDbContext _db;

    public SalesTargetResolver(IAppDbContext db) => _db = db;

    public Task<SalesTargetContext?> ResolveFromCodeTypeAsync(CodeType contentType, Guid contentId, CancellationToken cancellationToken = default)
    {
        return contentType switch
        {
            CodeType.Package => ResolveAsync(SalesTargetType.Package, contentId, cancellationToken),
            CodeType.Term => ResolveAsync(SalesTargetType.Term, contentId, cancellationToken),
            CodeType.Month => ResolveAsync(SalesTargetType.ContentSection, contentId, cancellationToken),
            CodeType.Lesson => ResolveAsync(SalesTargetType.Lesson, contentId, cancellationToken),
            CodeType.Video => ResolveAsync(SalesTargetType.SpecificVideo, contentId, cancellationToken),
            CodeType.Exam => ResolveAsync(SalesTargetType.PublicExam, contentId, cancellationToken),
            _ => Task.FromResult<SalesTargetContext?>(null)
        };
    }

    public async Task<SalesTargetContext?> ResolveAsync(SalesTargetType targetType, Guid? targetId, CancellationToken cancellationToken = default)
    {
        if (targetType is SalesTargetType.Platform)
            return new SalesTargetContext(targetType, targetId, 0, null, null, null, null, true, "المنصة");

        if (targetId is null)
            return null;

        return targetType switch
        {
            SalesTargetType.Package => await _db.Packages
                .Where(x => x.Id == targetId.Value)
                .Select(x => new SalesTargetContext(targetType, x.Id, x.Price, x.TeacherId, x.SubjectId, x.TargetGrade, null, x.IsActive, x.Name))
                .FirstOrDefaultAsync(cancellationToken),

            SalesTargetType.Term => await _db.Terms
                .Where(x => x.Id == targetId.Value)
                .Select(x => new SalesTargetContext(targetType, x.Id, x.Price, x.Package.TeacherId, x.Package.SubjectId, x.Package.TargetGrade, null, x.Package.IsActive, x.Title))
                .FirstOrDefaultAsync(cancellationToken),

            SalesTargetType.ContentSection => await _db.ContentSections
                .Where(x => x.Id == targetId.Value)
                .Select(x => new SalesTargetContext(targetType, x.Id, x.Price, x.Term.Package.TeacherId, x.Term.Package.SubjectId, x.Term.Package.TargetGrade, null, x.Term.Package.IsActive, x.Title))
                .FirstOrDefaultAsync(cancellationToken),

            SalesTargetType.Lesson => await _db.Lessons
                .Where(x => x.Id == targetId.Value)
                .Select(x => new SalesTargetContext(targetType, x.Id, x.Price, x.ContentSection.Term.Package.TeacherId, x.ContentSection.Term.Package.SubjectId, x.ContentSection.Term.Package.TargetGrade, null, x.ContentSection.Term.Package.IsActive, x.Title))
                .FirstOrDefaultAsync(cancellationToken),

            SalesTargetType.SpecificVideo => await _db.LessonVideos
                .Where(x => x.Id == targetId.Value)
                .Select(x => new SalesTargetContext(targetType, x.Id, 0, x.Lesson.ContentSection.Term.Package.TeacherId, x.Lesson.ContentSection.Term.Package.SubjectId, x.Lesson.ContentSection.Term.Package.TargetGrade, x.VideoTypeId, x.IsActive && x.Lesson.ContentSection.Term.Package.IsActive, x.Title))
                .FirstOrDefaultAsync(cancellationToken),

            SalesTargetType.VideoType => await _db.VideoTypes
                .Where(x => x.Id == targetId.Value)
                .Select(x => new SalesTargetContext(targetType, x.Id, 0, null, null, null, x.Id, x.IsActive, x.Name))
                .FirstOrDefaultAsync(cancellationToken),

            SalesTargetType.PublicExam => await _db.PublicExamProducts
                .Where(x => x.Id == targetId.Value || x.ExamId == targetId.Value)
                .Select(x => new SalesTargetContext(targetType, x.Id, x.Price, x.TeacherId, x.SubjectId, x.GradeLevel, null, x.IsPublished && x.DisabledAt == null, x.Exam.Title))
                .FirstOrDefaultAsync(cancellationToken),

            SalesTargetType.Teacher => await _db.TeacherProfiles
                .Where(x => x.Id == targetId.Value)
                .Select(x => new SalesTargetContext(targetType, x.Id, 0, x.Id, null, null, null, true, x.User.FullName))
                .FirstOrDefaultAsync(cancellationToken),

            _ => null
        };
    }
}
