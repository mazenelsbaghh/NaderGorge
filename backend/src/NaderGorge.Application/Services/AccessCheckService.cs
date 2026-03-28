using Microsoft.EntityFrameworkCore;
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
        // Admins, Teachers might bypass this, but for students we check StudentAccessGrants
        var userRoles = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        if (userRoles.Contains("Admin") || userRoles.Contains("Teacher"))
            return true;

        var hasAccess = await _db.StudentAccessGrants
            .AnyAsync(g => g.UserId == userId &&
                           g.IsActive &&
                           (g.PackageId == packageId || g.PackageId == null) &&
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

        // Check if user explicitly has grant for this exact lesson, or the package
        var packageId = lesson.ContentSection.Term?.PackageId;

        var hasAccess = await _db.StudentAccessGrants
            .AnyAsync(g => g.UserId == userId &&
                           g.IsActive &&
                           (g.LessonId == lessonId || g.PackageId == packageId) &&
                           (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow), ct);

        return hasAccess;
    }
}
