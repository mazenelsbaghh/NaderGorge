using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public class AccessCheckService : IAccessCheckService
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;
    private readonly IContentArchiveAccessService _archiveAccess;

    public AccessCheckService(
        IAppDbContext db,
        IAcademicScopeService? academicScope = null,
        IContentArchiveAccessService? archiveAccess = null)
    {
        _db = db;
        _academicScope = academicScope;
        _archiveAccess = archiveAccess ?? new ContentArchiveAccessService(db);
    }

    public async Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default)
    {
        var userRoles = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        if (userRoles.Contains("Admin") || userRoles.Contains("Teacher"))
            return true;

        if (!await _archiveAccess.CanViewAsync(userId, ContentArchiveTargetType.Package, packageId, ct))
            return false;

        var packageVisible = await _db.Packages
            .Where(package => package.Id == packageId)
            .Select(package => (bool?)package.Teacher.IsContentVisibleToStudents)
            .FirstOrDefaultAsync(ct);
        if (packageVisible == false)
            return false;

        // Only a Package-level grant gives access to the whole package
        var hasAccess = await _db.StudentAccessGrants
            .AnyAsync(g => g.UserId == userId &&
                           g.IsActive &&
                           g.GrantType == CodeType.Package &&
                           g.PackageId == packageId &&
                           (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow), ct);

        return hasAccess &&
            await IsAcademicallyEligibleAsync(StudentFacingScopeOwnerType.Package, packageId, userId, ct);
    }

    public async Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default)
    {
        var userRoles = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        if (userRoles.Contains("Admin") || userRoles.Contains("Teacher"))
            return true;

        if (!await _archiveAccess.CanViewAsync(userId, ContentArchiveTargetType.Lesson, lessonId, ct))
            return false;

        var lesson = await _db.Lessons
            .Include(l => l.ContentSection)
            .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct);

        if (lesson == null) return false;

        var sectionId = lesson.ContentSectionId;
        var termId = lesson.ContentSection?.TermId;
        var packageId = lesson.ContentSection?.Term?.PackageId;

        var teacherVisible = await _db.Lessons
            .Where(item => item.Id == lessonId)
            .Select(item => (bool?)item.ContentSection.Term.Package.Teacher.IsContentVisibleToStudents)
            .FirstOrDefaultAsync(ct);
        if (teacherVisible == false)
            return false;

        // Check cascading access: Lesson → Section → Term → Package
        // Each level must match its GrantType to prevent cross-level leaks
        var hasAccess = await _db.StudentAccessGrants
            .AnyAsync(g => g.UserId == userId &&
                           g.IsActive &&
                           (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow) &&
                           (
                               (g.GrantType == CodeType.Lesson && g.LessonId == lessonId) ||
                               (g.GrantType == CodeType.Month && g.ContentSectionId == sectionId) ||
                               (termId != null && g.GrantType == CodeType.Term && g.TermId == termId) ||
                               (packageId != null && g.GrantType == CodeType.Package && g.PackageId == packageId)
                           ),
                       ct);

        return hasAccess &&
            await IsAcademicallyEligibleAsync(StudentFacingScopeOwnerType.Lesson, lessonId, userId, ct);
    }

    public async Task<bool> HasAccessToVideoAsync(Guid userId, Guid lessonVideoId, CancellationToken ct = default)
    {
        if (!await _archiveAccess.CanViewAsync(userId, ContentArchiveTargetType.Video, lessonVideoId, ct))
            return false;

        var video = await _db.LessonVideos
            .AsNoTracking()
            .Where(v => v.Id == lessonVideoId && v.IsActive)
            .Select(v => new
            {
                v.LessonId,
                v.VideoTypeId,
                ContentSectionId = v.Lesson.ContentSectionId,
                TermId = v.Lesson.ContentSection.TermId,
                PackageId = v.Lesson.ContentSection.Term.PackageId,
                TeacherId = v.Lesson.ContentSection.Term.Package.TeacherId
            })
            .FirstOrDefaultAsync(ct);

        if (video == null)
            return false;

        var videoTeacherVisible = await _db.TeacherProfiles
            .Where(teacher => teacher.Id == video.TeacherId)
            .Select(teacher => (bool?)teacher.IsContentVisibleToStudents)
            .FirstOrDefaultAsync(ct);
        if (videoTeacherVisible == false)
            return false;

        if (await HasAccessToLessonAsync(userId, video.LessonId, ct))
            return true;

        var now = DateTime.UtcNow;
        var hasDirectVideoAccess = await _db.StudentAccessGrants.AnyAsync(g =>
            g.UserId == userId &&
            g.IsActive &&
            g.GrantType == CodeType.Video &&
            (g.ExpiresAt == null || g.ExpiresAt > now) &&
            (g.MaxUses == null || g.UsesConsumed < g.MaxUses) &&
            (
                g.LessonVideoId == lessonVideoId ||
                (
                    g.VideoTypeId != null &&
                    g.VideoTypeId == video.VideoTypeId &&
                    (g.LessonId == null || g.LessonId == video.LessonId) &&
                    (g.ContentSectionId == null || g.ContentSectionId == video.ContentSectionId) &&
                    (g.TermId == null || g.TermId == video.TermId) &&
                    (g.PackageId == null || g.PackageId == video.PackageId) &&
                    (
                        g.AccessCode == null ||
                        g.AccessCode.CodeGroup.TeacherId == null ||
                        g.AccessCode.CodeGroup.TeacherId == video.TeacherId
                    )
                )
            ), ct);

        return hasDirectVideoAccess &&
            await IsAcademicallyEligibleAsync(StudentFacingScopeOwnerType.LessonVideo, lessonVideoId, userId, ct);
    }

    public async Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default)
    {
        var userRoles = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        if (userRoles.Contains("Admin") || userRoles.Contains("Teacher"))
            return true;

        if (!await _archiveAccess.CanViewAsync(userId, ContentArchiveTargetType.Exam, examId, ct))
            return false;

        var now = DateTime.UtcNow;
        var examVisible = await _db.Exams
            .Where(exam => exam.Id == examId)
            .Select(exam => (bool?)exam.CreatedByTeacher.IsContentVisibleToStudents)
            .FirstOrDefaultAsync(ct);
        if (examVisible == false)
            return false;
        var publicProduct = await _db.PublicExamProducts
            .Where(x => x.ExamId == examId)
            .Select(x => new
            {
                x.Id,
                x.IsPublished,
                x.IsPaid,
                x.AvailableFrom,
                x.AvailableUntil,
                x.DisabledAt
            })
            .FirstOrDefaultAsync(ct);

        if (publicProduct != null)
        {
            if (!publicProduct.IsPublished ||
                publicProduct.DisabledAt != null ||
                (publicProduct.AvailableFrom != null && publicProduct.AvailableFrom > now) ||
                (publicProduct.AvailableUntil != null && publicProduct.AvailableUntil <= now))
                return false;

            if (!await IsAcademicallyEligibleAsync(StudentFacingScopeOwnerType.PublicExamProduct, publicProduct.Id, userId, ct))
                return false;

            if (!publicProduct.IsPaid)
                return true;

            var hasPublicExamAccess = await _db.StudentAccessGrants.AnyAsync(g =>
                g.UserId == userId &&
                g.IsActive &&
                g.GrantType == CodeType.Exam &&
                g.PublicExamProductId == publicProduct.Id &&
                (g.ExpiresAt == null || g.ExpiresAt > now), ct);

            if (hasPublicExamAccess)
                return true;
        }

        // 1. Direct Exam access grant
        var hasDirectAccess = await _db.StudentAccessGrants
            .AnyAsync(g => g.UserId == userId &&
                           g.IsActive &&
                           g.GrantType == CodeType.Exam &&
                           g.ExamId == examId &&
                           (g.ExpiresAt == null || g.ExpiresAt > now), ct);

        if (hasDirectAccess &&
            await IsAcademicallyEligibleAsync(StudentFacingScopeOwnerType.Exam, examId, userId, ct))
            return true;

        // 2. Lesson-linked Exam access
        var lessonIds = await _db.Lessons
            .Where(l => l.ExamId == examId)
            .Select(l => l.Id)
            .ToListAsync(ct);

        foreach (var lessonId in lessonIds)
        {
            if (await HasAccessToLessonAsync(userId, lessonId, ct))
                return true;
        }

        // 3. Video-linked Exam access (both foreign key directions)
        var videoLessons = await _db.LessonVideos
            .Where(v => v.ExamId == examId)
            .Select(v => new { v.Id, v.LessonId })
            .ToListAsync(ct);

        foreach (var video in videoLessons)
        {
            if (await HasAccessToLessonAsync(userId, video.LessonId, ct))
                return true;

            if (await HasAccessToVideoAsync(userId, video.Id, ct))
                return true;
        }

        var examWithVideo = await _db.Exams
            .Where(e => e.Id == examId && e.LessonVideoId != null)
            .Select(e => new { e.LessonVideoId })
            .FirstOrDefaultAsync(ct);

        if (examWithVideo?.LessonVideoId is Guid linkedVideoId)
        {
            var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == linkedVideoId, ct);
            if (video != null)
            {
                if (await HasAccessToLessonAsync(userId, video.LessonId, ct))
                    return true;

                if (await HasAccessToVideoAsync(userId, video.Id, ct))
                    return true;
            }
        }

        return false;
    }

    private async Task<bool> IsAcademicallyEligibleAsync(
        StudentFacingScopeOwnerType ownerType,
        Guid ownerId,
        Guid userId,
        CancellationToken ct)
    {
        return _academicScope == null ||
            await _academicScope.IsOwnerEligibleForStudentAsync(ownerType, ownerId, userId, ct);
    }
}
