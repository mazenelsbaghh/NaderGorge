using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public class AccessCheckService : IAccessCheckService
{
    private readonly IAppDbContext _db;

    public AccessCheckService(IAppDbContext db)
    {
        _db = db;
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

        // Only a Package-level grant gives access to the whole package
        var hasAccess = await _db.StudentAccessGrants
            .AnyAsync(g => g.UserId == userId &&
                           g.IsActive &&
                           g.GrantType == CodeType.Package &&
                           g.PackageId == packageId &&
                           (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow), ct);

        return hasAccess;
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

        var lesson = await _db.Lessons
            .Include(l => l.ContentSection)
            .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct);

        if (lesson == null) return false;

        var sectionId = lesson.ContentSectionId;
        var termId = lesson.ContentSection?.TermId;
        var packageId = lesson.ContentSection?.Term?.PackageId;

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

        return hasAccess;
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

        // 1. Direct Exam access grant
        var hasDirectAccess = await _db.StudentAccessGrants
            .AnyAsync(g => g.UserId == userId &&
                           g.IsActive &&
                           g.GrantType == CodeType.Exam &&
                           g.ExamId == examId &&
                           (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow), ct);

        if (hasDirectAccess) return true;

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

            var hasVideoGrant = await _db.StudentAccessGrants
                .AnyAsync(g => g.UserId == userId &&
                               g.IsActive &&
                               g.GrantType == CodeType.Video &&
                               g.LessonVideoId == video.Id &&
                               (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow), ct);

            if (hasVideoGrant) return true;
        }

        var examWithVideo = await _db.Exams
            .Where(e => e.Id == examId && e.LessonVideoId != null)
            .Select(e => new { e.LessonVideoId })
            .FirstOrDefaultAsync(ct);

        if (examWithVideo != null)
        {
            var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.Id == examWithVideo.LessonVideoId.Value, ct);
            if (video != null)
            {
                if (await HasAccessToLessonAsync(userId, video.LessonId, ct))
                    return true;

                var hasVideoGrant = await _db.StudentAccessGrants
                    .AnyAsync(g => g.UserId == userId &&
                                   g.IsActive &&
                                   g.GrantType == CodeType.Video &&
                                   g.LessonVideoId == video.Id &&
                                   (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow), ct);

                if (hasVideoGrant) return true;
            }
        }

        return false;
    }
}
