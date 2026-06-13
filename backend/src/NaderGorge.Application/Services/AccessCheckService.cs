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
}
