using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Content;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

internal sealed class AssessmentExportRoster(IAppDbContext db, IAccessCheckService access)
{
    public async Task<IReadOnlyList<AssessmentExportRow>> AbsentAsync(AssessmentTarget target,
        AssessmentExportDefinition definition, IReadOnlyList<AssessmentExportRow> attempts, CancellationToken ct)
    {
        var submitted = attempts.Select(a => a.Student.Id).Distinct().ToArray();
        var candidates = await CandidatesAsync(target, definition, submitted, ct);
        if (candidates.Count > GetAssessmentExportQueryHandler.RowLimit)
            throw new ArgumentException("يتجاوز كشف الغياب 50000 طالب. لم يتم تنزيل كشف ناقص؛ تواصل مع الدعم.");
        var absent = new List<AssessmentExportRow>();
        foreach (var student in candidates)
        {
            var eligible = target.Kind == AssessmentKind.Homework
                ? await access.HasAccessToLessonAsync(student.Id, definition.LessonId!.Value, ct)
                : await access.HasAccessToExamAsync(student.Id, target.AssessmentId, ct);
            if (eligible) absent.Add(new(student, null, null, null,
                target.Kind == AssessmentKind.Homework ? "لم يسلّم الواجب" : "لم يسلّم الامتحان", null, null, null, false));
        }
        return absent;
    }

    private async Task<List<AssessmentExportStudent>> CandidatesAsync(AssessmentTarget target,
        AssessmentExportDefinition definition, Guid[] submitted, CancellationToken ct)
    {
        var isExam = target.Kind == AssessmentKind.Exam;
        var videos = db.LessonVideos.Where(v => isExam && (v.ExamId == target.AssessmentId ||
            db.Exams.Any(e => e.Id == target.AssessmentId && e.LessonVideoId == v.Id)));
        var lessons = db.Lessons.Where(l => l.Id == definition.LessonId ||
            (isExam && (l.ExamId == target.AssessmentId || videos.Any(v => v.LessonId == l.Id))));
        var publicProductIds = db.PublicExamProducts.Where(p => isExam && p.ExamId == target.AssessmentId).Select(p => p.Id);
        var now = DateTime.UtcNow;
        var grants = db.StudentAccessGrants.Where(g => g.IsActive && g.CancelledAt == null && g.GrantedAt <= now &&
            (g.ExpiresAt == null || g.ExpiresAt > now) &&
            (g.GrantType != CodeType.Video || g.MaxUses == null || g.UsesConsumed < g.MaxUses));
        var matching = grants.Where(g =>
            (g.GrantType == CodeType.Package && lessons.Any(l => l.ContentSection.Term.PackageId == g.PackageId)) ||
            (g.GrantType == CodeType.Term && lessons.Any(l => l.ContentSection.TermId == g.TermId)) ||
            (g.GrantType == CodeType.Month && lessons.Any(l => l.ContentSectionId == g.ContentSectionId)) ||
            (g.GrantType == CodeType.Lesson && lessons.Any(l => l.Id == g.LessonId)) ||
            (isExam && g.GrantType == CodeType.Exam && (g.ExamId == target.AssessmentId || publicProductIds.Contains(g.PublicExamProductId ?? Guid.Empty))) ||
            (isExam && g.GrantType == CodeType.Video && videos.Any(v => v.Id == g.LessonVideoId ||
                (g.VideoTypeId == v.VideoTypeId && (g.LessonId == null || g.LessonId == v.LessonId) &&
                 (g.ContentSectionId == null || g.ContentSectionId == v.Lesson.ContentSectionId) &&
                 (g.TermId == null || g.TermId == v.Lesson.ContentSection.TermId) &&
                 (g.PackageId == null || g.PackageId == v.Lesson.ContentSection.Term.PackageId) &&
                 (g.AccessCodeId == null || g.AccessCode!.CodeGroup.TeacherId == null ||
                  g.AccessCode.CodeGroup.TeacherId == v.Lesson.ContentSection.Term.Package.TeacherId)))));
        var freePublicStudents = await FreePublicStudentsAsync(target, ct);
        var studentIds = matching.Select(g => g.UserId);
        return await db.Users.AsNoTracking().Where(u => (studentIds.Contains(u.Id) || freePublicStudents.Contains(u.Id)) && !submitted.Contains(u.Id) &&
                u.IsActive && u.UserRoles.Any(r => r.Role.Type == RoleType.Student))
            .OrderBy(u => u.FullName).ThenBy(u => u.Id).Take(GetAssessmentExportQueryHandler.RowLimit + 1)
            .Select(u => new AssessmentExportStudent(u.Id, u.FullName, u.PhoneNumber,
                u.StudentProfile == null ? null : u.StudentProfile.StudentCode,
                u.StudentProfile == null ? null : u.StudentProfile.ParentPhone,
                u.StudentProfile == null ? null : u.StudentProfile.MotherPhone)).ToListAsync(ct);
    }

    private async Task<Guid[]> FreePublicStudentsAsync(AssessmentTarget target, CancellationToken ct)
    {
        if (target.Kind != AssessmentKind.Exam || !await db.PublicExamProducts.AnyAsync(p => p.ExamId == target.AssessmentId && !p.IsPaid, ct))
            return [];
        var packageIds = await db.Packages.Where(p => db.Exams.Any(e => e.Id == target.AssessmentId &&
            e.CreatedByTeacherId == p.TeacherId)).Select(p => p.Id).ToArrayAsync(ct);
        var facts = await new ContentGrantFactSource(db).LoadAsync(new(packageIds), ct);
        var now = DateTime.UtcNow;
        return facts.Where(f => f.IsActive && f.GrantedAt <= now && (f.ExpiresAt == null || f.ExpiresAt > now))
            .Select(f => f.UserId).Distinct().ToArray();
    }
}
