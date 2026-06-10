using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public class TeacherAuthorizationService
{
    private readonly IAppDbContext _db;

    public TeacherAuthorizationService(IAppDbContext db)
    {
        _db = db;
    }

    private async Task<(bool isTeacher, Guid? teacherId, bool isAdmin)> GetUserStatusAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.TeacherProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null) return (false, null, false);

        var isAdmin = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Admin);
        var isTeacher = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher);
        var teacherId = user.TeacherProfile?.Id;

        return (isTeacher, teacherId, isAdmin);
    }

    public async Task<bool> CanAccessPackageAsync(Guid userId, Guid packageId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true; // Non-teachers aren't blocked by teacher boundaries
        if (status.teacherId == null) return false;

        var package = await _db.Packages.FindAsync(new object[] { packageId }, ct);
        return package != null && package.TeacherId == status.teacherId.Value;
    }


    public async Task<bool> CanAccessTermAsync(Guid userId, Guid termId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;

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

        var codeGroup = await _db.CodeGroups.FindAsync(new object[] { codeGroupId }, ct);
        return codeGroup != null && codeGroup.TeacherId == status.teacherId.Value;
    }

    public async Task<bool> CanAccessExamAsync(Guid userId, Guid examId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;

        var exam = await _db.Exams.FindAsync(new object[] { examId }, ct);
        return exam != null && exam.CreatedByTeacherId == status.teacherId.Value;
    }

    public async Task<bool> CanAccessQuestionAsync(Guid userId, Guid questionId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;

        var question = await _db.QuestionBankItems.FindAsync(new object[] { questionId }, ct);
        return question != null && question.CreatedByTeacherId == status.teacherId.Value;
    }

    public async Task<bool> CanAccessEssaySubmissionAsync(Guid userId, Guid submissionId, CancellationToken ct)
    {
        var status = await GetUserStatusAsync(userId, ct);
        if (status.isAdmin) return true;
        if (!status.isTeacher) return true;
        if (status.teacherId == null) return false;

        var submission = await _db.EssaySubmissions
            .Include(s => s.Question)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);

        return submission != null && submission.Question != null && submission.Question.CreatedByTeacherId == status.teacherId.Value;
    }
}
