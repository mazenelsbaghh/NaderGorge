using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record MissingHomeworkStudentDto(Guid StudentId, string Name, string Phone);
public record MissingHomeworkStudentsDto(List<MissingHomeworkStudentDto> Students, bool HasMore);
public record GetMissingHomeworkStudentsQuery(Guid HomeworkId, Guid ActorId, int Page = 1) : IRequest<ApiResponse<MissingHomeworkStudentsDto>>;
public class GetMissingHomeworkStudentsQueryHandler(IAppDbContext db, TeacherAuthorizationService auth, IAccessCheckService access)
    : IRequestHandler<GetMissingHomeworkStudentsQuery, ApiResponse<MissingHomeworkStudentsDto>>
{
    public async Task<ApiResponse<MissingHomeworkStudentsDto>> Handle(GetMissingHomeworkStudentsQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.Page > 10000) return ApiResponse<MissingHomeworkStudentsDto>.Fail("رقم الصفحة غير صالح.");
        var target = new AssessmentTarget(AssessmentKind.Homework, request.HomeworkId, Guid.Empty, request.ActorId);
        if (!await AssessmentAccess.Allowed(db, auth, target, ct)) return ApiResponse<MissingHomeworkStudentsDto>.Fail("غير مصرح بعرض طلاب هذا الواجب.");
        var lessonId = await db.Homeworks.Where(h => h.Id == request.HomeworkId).Select(h => h.LessonId).SingleAsync(ct);
        var lesson = await db.Lessons.Where(l => l.Id == lessonId)
            .Select(l => new { LessonId = l.Id, l.ContentSectionId, l.ContentSection.TermId, l.ContentSection.Term.PackageId }).SingleAsync(ct);
        var now = DateTime.UtcNow;
        var candidates = await db.StudentAccessGrants.AsNoTracking().Where(g => g.IsActive && (g.ExpiresAt == null || g.ExpiresAt > now) &&
            ((g.GrantType == CodeType.Lesson && g.LessonId == lesson.LessonId) ||
             (g.GrantType == CodeType.Month && g.ContentSectionId == lesson.ContentSectionId) ||
             (g.GrantType == CodeType.Term && g.TermId == lesson.TermId) ||
             (g.GrantType == CodeType.Package && g.PackageId == lesson.PackageId)))
            .Select(g => g.User).Where(u => u.IsActive && u.UserRoles.Any(r => r.Role.Type == RoleType.Student) &&
                !db.HomeworkSubmissions.Any(s => s.HomeworkId == request.HomeworkId && s.StudentId == u.Id && s.SubmittedAt != null))
            .Select(u => new { StudentId = u.Id, Name = u.FullName, Phone = u.PhoneNumber }).Distinct()
            .OrderBy(u => u.StudentId).Skip((request.Page - 1) * 100).Take(101).ToListAsync(ct);
        var students = new List<MissingHomeworkStudentDto>();
        // Reuse the authoritative archive + academic-scope policy; access grants
        // alone do not prove that the student is currently entitled to this lesson.
        foreach (var candidate in candidates.Take(100))
            if (await access.HasAccessToLessonAsync(candidate.StudentId, lesson.LessonId, ct))
                students.Add(new(candidate.StudentId, candidate.Name, candidate.Phone));
        return ApiResponse<MissingHomeworkStudentsDto>.Ok(new(students, candidates.Count > 100));
    }
}
