using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed class ContentArchiveAccessService(IAppDbContext db) : IContentArchiveAccessService
{
    public async Task<bool> CanViewAsync(
        Guid userId,
        ContentArchiveTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        if (await IsPrivilegedAsync(userId, cancellationToken)) return true;

        var path = await ArchivePathAsync(targetType, targetId, cancellationToken);
        if (path is null || path.Modes.Contains(ContentArchiveMode.HiddenFromEveryone)) return false;
        if (!path.Modes.Contains(ContentArchiveMode.ActiveSubscribersOnly)) return true;

        return await HasActiveGrantAsync(userId, path, cancellationToken);
    }

    public async Task<bool> CanAcquireAsync(
        ContentArchiveTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        var path = await ArchivePathAsync(targetType, targetId, cancellationToken);
        return path is not null && path.Modes.All(mode => mode == ContentArchiveMode.None);
    }

    private async Task<bool> IsPrivilegedAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .AnyAsync(userRole => userRole.Role.Type != RoleType.Student, cancellationToken);

    private Task<ArchivePath?> ArchivePathAsync(
        ContentArchiveTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken) => targetType switch
    {
        ContentArchiveTargetType.Package => PackagePathAsync(targetId, cancellationToken),
        ContentArchiveTargetType.Term => TermPathAsync(targetId, cancellationToken),
        ContentArchiveTargetType.Section => SectionPathAsync(targetId, cancellationToken),
        ContentArchiveTargetType.Lesson => LessonPathAsync(targetId, cancellationToken),
        ContentArchiveTargetType.Video => VideoPathAsync(targetId, cancellationToken),
        ContentArchiveTargetType.Resource => ResourcePathAsync(targetId, cancellationToken),
        ContentArchiveTargetType.Exam => ExamPathAsync(targetId, cancellationToken),
        ContentArchiveTargetType.Homework => HomeworkPathAsync(targetId, cancellationToken),
        _ => Task.FromResult<ArchivePath?>(null)
    };

    private async Task<ArchivePath?> PackagePathAsync(Guid id, CancellationToken ct) =>
        await db.Packages.Where(package => package.Id == id)
            .Select(package => new ArchivePath(package.Id, null, null, null, null, null, null, new[] { package.ArchiveMode }))
            .FirstOrDefaultAsync(ct);

    private async Task<ArchivePath?> TermPathAsync(Guid id, CancellationToken ct) =>
        await db.Terms.Where(term => term.Id == id)
            .Select(term => new ArchivePath(term.PackageId, term.Id, null, null, null, null, null,
                new[] { term.Package.ArchiveMode, term.ArchiveMode }))
            .FirstOrDefaultAsync(ct);

    private async Task<ArchivePath?> SectionPathAsync(Guid id, CancellationToken ct) =>
        await db.ContentSections.Where(section => section.Id == id)
            .Select(section => new ArchivePath(section.Term.PackageId, section.TermId, section.Id, null, null, null, null,
                new[] { section.Term.Package.ArchiveMode, section.Term.ArchiveMode, section.ArchiveMode }))
            .FirstOrDefaultAsync(ct);

    private async Task<ArchivePath?> LessonPathAsync(Guid id, CancellationToken ct) =>
        await db.Lessons.Where(lesson => lesson.Id == id)
            .Select(lesson => new ArchivePath(lesson.ContentSection.Term.PackageId, lesson.ContentSection.TermId,
                lesson.ContentSectionId, lesson.Id, null, null, null,
                new[] { lesson.ContentSection.Term.Package.ArchiveMode, lesson.ContentSection.Term.ArchiveMode,
                    lesson.ContentSection.ArchiveMode, lesson.ArchiveMode }))
            .FirstOrDefaultAsync(ct);

    private async Task<ArchivePath?> VideoPathAsync(Guid id, CancellationToken ct) =>
        await db.LessonVideos.Where(video => video.Id == id)
            .Select(video => new ArchivePath(video.Lesson.ContentSection.Term.PackageId, video.Lesson.ContentSection.TermId,
                video.Lesson.ContentSectionId, video.LessonId, video.Id, video.VideoTypeId, null,
                new[] { video.Lesson.ContentSection.Term.Package.ArchiveMode, video.Lesson.ContentSection.Term.ArchiveMode,
                    video.Lesson.ContentSection.ArchiveMode, video.Lesson.ArchiveMode, video.ArchiveMode }))
            .FirstOrDefaultAsync(ct);

    private async Task<ArchivePath?> ResourcePathAsync(Guid id, CancellationToken ct) =>
        await db.LessonResources.Where(resource => resource.Id == id)
            .Select(resource => new ArchivePath(resource.Lesson.ContentSection.Term.PackageId, resource.Lesson.ContentSection.TermId,
                resource.Lesson.ContentSectionId, resource.LessonId, null, null, null,
                new[] { resource.Lesson.ContentSection.Term.Package.ArchiveMode, resource.Lesson.ContentSection.Term.ArchiveMode,
                    resource.Lesson.ContentSection.ArchiveMode, resource.Lesson.ArchiveMode, resource.ArchiveMode }))
            .FirstOrDefaultAsync(ct);

    private async Task<ArchivePath?> HomeworkPathAsync(Guid id, CancellationToken ct) =>
        await db.Homeworks.Where(homework => homework.Id == id)
            .Join(db.Lessons, homework => homework.LessonId, lesson => lesson.Id, (homework, lesson) => new { homework, lesson })
            .Select(row => new ArchivePath(row.lesson.ContentSection.Term.PackageId, row.lesson.ContentSection.TermId,
                row.lesson.ContentSectionId, row.lesson.Id, null, null, null,
                new[] { row.lesson.ContentSection.Term.Package.ArchiveMode, row.lesson.ContentSection.Term.ArchiveMode,
                    row.lesson.ContentSection.ArchiveMode, row.lesson.ArchiveMode, row.homework.ArchiveMode }))
            .FirstOrDefaultAsync(ct);

    private async Task<ArchivePath?> ExamPathAsync(Guid id, CancellationToken ct)
    {
        var exam = await db.Exams.Where(candidate => candidate.Id == id)
            .Select(candidate => new { candidate.ArchiveMode, candidate.LessonVideoId })
            .FirstOrDefaultAsync(ct);
        if (exam is null) return null;

        var linkedLesson = await db.Lessons.Where(lesson => lesson.ExamId == id)
            .Select(lesson => new ArchivePath(lesson.ContentSection.Term.PackageId, lesson.ContentSection.TermId,
                lesson.ContentSectionId, lesson.Id, null, null, id,
                new[] { lesson.ContentSection.Term.Package.ArchiveMode, lesson.ContentSection.Term.ArchiveMode,
                    lesson.ContentSection.ArchiveMode, lesson.ArchiveMode, exam.ArchiveMode }))
            .FirstOrDefaultAsync(ct);
        if (linkedLesson is not null) return linkedLesson;

        var linkedVideoId = exam.LessonVideoId ?? await db.LessonVideos
            .Where(video => video.ExamId == id)
            .Select(video => (Guid?)video.Id)
            .FirstOrDefaultAsync(ct);
        var linkedVideo = await db.LessonVideos.Where(video => video.Id == linkedVideoId)
            .Select(video => new ArchivePath(video.Lesson.ContentSection.Term.PackageId, video.Lesson.ContentSection.TermId,
                video.Lesson.ContentSectionId, video.LessonId, video.Id, video.VideoTypeId, id,
                new[] { video.Lesson.ContentSection.Term.Package.ArchiveMode, video.Lesson.ContentSection.Term.ArchiveMode,
                    video.Lesson.ContentSection.ArchiveMode, video.Lesson.ArchiveMode, video.ArchiveMode,
                    exam.ArchiveMode }))
            .FirstOrDefaultAsync(ct);
        if (linkedVideo is not null) return linkedVideo;

        return new ArchivePath(null, null, null, null, null, null, id, new[] { exam.ArchiveMode });
    }

    private async Task<bool> HasActiveGrantAsync(Guid userId, ArchivePath path, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.StudentAccessGrants.AnyAsync(grant =>
            grant.UserId == userId && grant.IsActive &&
            (grant.ExpiresAt == null || grant.ExpiresAt > now) &&
            (grant.MaxUses == null || grant.UsesConsumed < grant.MaxUses) &&
            ((path.LessonId != null && grant.GrantType == CodeType.Lesson && grant.LessonId == path.LessonId) ||
             (path.SectionId != null && grant.GrantType == CodeType.Month && grant.ContentSectionId == path.SectionId) ||
             (path.TermId != null && grant.GrantType == CodeType.Term && grant.TermId == path.TermId) ||
             (path.PackageId != null && grant.GrantType == CodeType.Package && grant.PackageId == path.PackageId) ||
             (path.VideoId != null && grant.GrantType == CodeType.Video && grant.LessonVideoId == path.VideoId) ||
             (path.VideoTypeId != null && grant.GrantType == CodeType.Video && grant.VideoTypeId == path.VideoTypeId &&
              (grant.LessonId == null || grant.LessonId == path.LessonId) &&
              (grant.ContentSectionId == null || grant.ContentSectionId == path.SectionId) &&
              (grant.TermId == null || grant.TermId == path.TermId) &&
              (grant.PackageId == null || grant.PackageId == path.PackageId)) ||
             (path.ExamId != null && grant.GrantType == CodeType.Exam &&
              (grant.ExamId == path.ExamId ||
               (grant.PublicExamProductId != null && db.PublicExamProducts.Any(product =>
                   product.Id == grant.PublicExamProductId && product.ExamId == path.ExamId))))), ct);
    }

    private sealed record ArchivePath(
        Guid? PackageId,
        Guid? TermId,
        Guid? SectionId,
        Guid? LessonId,
        Guid? VideoId,
        Guid? VideoTypeId,
        Guid? ExamId,
        IReadOnlyList<ContentArchiveMode> Modes);
}
