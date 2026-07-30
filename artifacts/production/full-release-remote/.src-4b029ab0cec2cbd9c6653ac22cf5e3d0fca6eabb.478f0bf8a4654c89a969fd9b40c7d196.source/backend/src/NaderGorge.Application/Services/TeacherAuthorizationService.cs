using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed record TeacherWorkspaceAccess(
    Guid TeacherId,
    Guid TeacherUserId,
    bool IsOwner,
    IReadOnlySet<string> PermissionKeys);

public class TeacherAuthorizationService
{
    private const string ContentPermission = "content";
    private const string CodesPermission = "codes";
    private const string EssaysPermission = "essays";
    private readonly IAppDbContext _db;

    public TeacherAuthorizationService(IAppDbContext db)
    {
        _db = db;
    }

    private async Task<(bool isTeacher, Guid? teacherId, bool isAdmin, bool isOwner, IReadOnlySet<string> permissions)> GetUserStatusAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.TeacherProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null) return (false, null, false, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var isAdmin = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Admin);
        var isTeacher = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher);
        var teacherId = user.TeacherProfile?.Id;
        var isOwner = teacherId.HasValue;
        IReadOnlySet<string> permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (teacherId == null && isTeacher)
        {
            var membership = await _db.TeacherStaffMembers
                .Where(member => member.UserId == userId && member.IsActive && member.User.IsActive)
                .Select(member => new { member.TeacherId, member.PermissionKeys })
                .FirstOrDefaultAsync(ct);
            teacherId = membership?.TeacherId;
            permissions = ParsePermissions(membership?.PermissionKeys);
        }

        return (isTeacher, teacherId, isAdmin, isOwner, permissions);
    }

    private static IReadOnlySet<string> ParsePermissions(string? permissionKeys) =>
        (permissionKeys ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool HasPermission((bool isTeacher, Guid? teacherId, bool isAdmin, bool isOwner, IReadOnlySet<string> permissions) status, string permission) =>
        status.isAdmin || status.isOwner || status.permissions.Contains(permission);

    public async Task<TeacherWorkspaceAccess?> GetWorkspaceAccessAsync(Guid userId, CancellationToken ct)
    {
        var teacher = await _db.TeacherProfiles.AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == userId, ct);
        if (teacher != null)
            return new TeacherWorkspaceAccess(teacher.Id, teacher.UserId, true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var membership = await _db.TeacherStaffMembers.AsNoTracking()
            .Include(member => member.Teacher)
            .FirstOrDefaultAsync(member => member.UserId == userId && member.IsActive && member.User.IsActive, ct);
        return membership == null
            ? null
            : new TeacherWorkspaceAccess(
                membership.TeacherId,
                membership.Teacher.UserId,
                false,
                ParsePermissions(membership.PermissionKeys));
    }

    public async Task<bool> CanAccessTeacherWorkspacePermissionAsync(Guid userId, string permission, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        return !status.isTeacher || HasPermission(status, permission);
    }

    public async Task<bool> IsTeacherOwnerOrNonTeacherAsync(Guid userId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        return !status.isTeacher || status.isOwner;
    }

    public async Task<bool> CanAccessPackageAsync(Guid userId, Guid packageId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true; // Non-teachers aren't blocked by teacher boundaries
        if (status.teacherId == null) return false;
        if (!HasPermission(status, ContentPermission)) return false;

        var package = await _db.Packages.FindAsync(new object[] { packageId }, ct);
        return package != null && package.TeacherId == status.teacherId.Value;
    }


    public async Task<bool> CanAccessTermAsync(Guid userId, Guid termId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;
        if (!HasPermission(status, ContentPermission)) return false;

        var term = await _db.Terms
            .Include(t => t.Package)
            .FirstOrDefaultAsync(t => t.Id == termId, ct);

        return term != null && term.Package != null && term.Package.TeacherId == status.teacherId.Value;
    }

    public async Task<bool> CanAccessSectionAsync(Guid userId, Guid sectionId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;
        if (!HasPermission(status, ContentPermission)) return false;

        var section = await _db.ContentSections
            .Include(s => s.Term)
                .ThenInclude(t => t.Package)
            .FirstOrDefaultAsync(s => s.Id == sectionId, ct);

        return section != null && section.Term != null && section.Term.Package != null && section.Term.Package.TeacherId == status.teacherId.Value;
    }

    public async Task<bool> CanAccessLessonAsync(Guid userId, Guid lessonId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;
        if (!HasPermission(status, ContentPermission)) return false;

        var lesson = await _db.Lessons
            .Include(l => l.ContentSection)
                .ThenInclude(s => s.Term)
                    .ThenInclude(t => t.Package)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct);

        return lesson != null && lesson.ContentSection != null && lesson.ContentSection.Term != null && lesson.ContentSection.Term.Package != null && lesson.ContentSection.Term.Package.TeacherId == status.teacherId.Value;
    }

    public async Task<bool> CanAccessCodeGroupAsync(Guid userId, Guid codeGroupId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;
        if (!HasPermission(status, CodesPermission)) return false;

        var codeGroup = await _db.CodeGroups.FindAsync(new object[] { codeGroupId }, ct);
        return codeGroup != null && codeGroup.TeacherId.HasValue && codeGroup.TeacherId.Value == status.teacherId.Value;
    }

    public async Task<bool> CanAccessExamAsync(Guid userId, Guid examId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;
        if (!HasPermission(status, ContentPermission)) return false;

        var exam = await _db.Exams.FindAsync(new object[] { examId }, ct);
        return exam != null && exam.CreatedByTeacherId == status.teacherId.Value;
    }

    public async Task<bool> CanAccessQuestionAsync(Guid userId, Guid questionId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;
        if (!HasPermission(status, ContentPermission)) return false;

        var question = await _db.QuestionBankItems.FindAsync(new object[] { questionId }, ct);
        return question != null && question.CreatedByTeacherId == status.teacherId.Value;
    }

    public async Task<bool> CanAccessEssaySubmissionAsync(Guid userId, Guid submissionId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;
        if (!HasPermission(status, EssaysPermission)) return false;

        var submission = await _db.EssaySubmissions
            .Include(s => s.Question)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);

        return submission != null && submission.Question != null && submission.Question.CreatedByTeacherId == status.teacherId.Value;
    }
}
