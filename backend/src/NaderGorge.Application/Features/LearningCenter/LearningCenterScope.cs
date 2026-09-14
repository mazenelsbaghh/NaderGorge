using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.LearningCenter;

public sealed class LearningCenterScope(IAppDbContext db)
{
    public async Task<Guid?> TeacherAsync(Guid actorId, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().Include(u => u.UserRoles).ThenInclude(r => r.Role)
            .SingleOrDefaultAsync(u => u.Id == actorId && u.IsActive && !u.IsDeleted, ct);
        if (actor is null) throw new UnauthorizedAccessException();
        if (actor.UserRoles.Any(r => r.Role.Type == RoleType.Admin)) return null;
        if (!actor.UserRoles.Any(r => r.Role.Type == RoleType.Teacher)) throw new UnauthorizedAccessException();
        var workspace = await new TeacherAuthorizationService(db).GetWorkspaceAccessAsync(actorId, ct);
        // This workspace combines private student results and content authoring; staff need both permissions.
        if (workspace is null || (!workspace.IsOwner &&
            (!workspace.PermissionKeys.Contains("content") || !workspace.PermissionKeys.Contains("students"))))
            throw new UnauthorizedAccessException();
        return workspace.TeacherId;
    }

    public async Task<IQueryable<Package>> PackagesAsync(Guid actorId, LearningFilter filter, CancellationToken ct)
    {
        var teacherId = await TeacherAsync(actorId, ct);
        if (teacherId.HasValue && filter.TeacherId.HasValue && filter.TeacherId != teacherId)
            throw new UnauthorizedAccessException();
        var packages = db.Packages.AsNoTracking().AsQueryable();
        var effectiveTeacher = teacherId ?? filter.TeacherId;
        if (effectiveTeacher.HasValue) packages = packages.Where(p => p.TeacherId == effectiveTeacher);
        if (filter.SubjectId.HasValue) packages = packages.Where(p => p.SubjectId == filter.SubjectId);
        if (filter.PackageId.HasValue) packages = packages.Where(p => p.Id == filter.PackageId);
        if (!string.IsNullOrWhiteSpace(filter.Grade)) packages = packages.Where(p => p.TargetGrade == filter.Grade);
        return packages;
    }

    public async Task<Lesson> LessonAsync(Guid actorId, Guid lessonId, CancellationToken ct)
    {
        var packages = await PackagesAsync(actorId, new(), ct);
        return await db.Lessons.Include(l => l.ContentSection).ThenInclude(s => s.Term).ThenInclude(t => t.Package)
            .SingleOrDefaultAsync(l => l.Id == lessonId && packages.Any(p => p.Id == l.ContentSection.Term.PackageId), ct)
            ?? throw new UnauthorizedAccessException();
    }

    public async Task<QuestionBankItem> QuestionAsync(Guid actorId, Guid questionId, CancellationToken ct)
    {
        var teacherId = await TeacherAsync(actorId, ct);
        return await db.QuestionBankItems.Include(q => q.Options)
            .SingleOrDefaultAsync(q => q.Id == questionId && (!teacherId.HasValue || q.CreatedByTeacherId == teacherId), ct)
            ?? throw new UnauthorizedAccessException();
    }

    public static void Validate(LearningFilter filter)
    {
        if (filter.Days is < 1 or > 365 || filter.InactiveDays is < 1 or > 90 ||
            filter.DeclinePoints is < 1 or > 100 || filter.RepeatedAttempts is < 2 or > 10)
            throw new ArgumentException("راجع الفترة وحدود التنبيه المختارة.");
    }
}
