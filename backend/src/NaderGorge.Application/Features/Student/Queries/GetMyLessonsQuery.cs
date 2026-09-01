using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetMyLessonsQuery(Guid UserId) : IRequest<ApiResponse<List<MyLessonDto>>>;

public record MyLessonDto(
    Guid Id,
    string Title,
    int Order,
    Guid PackageId,
    string PackageName,
    string TermTitle,
    string SectionTitle,
    string TeacherName,
    string? ImageUrl,
    bool IsCompleted,
    int VideoCount);

public sealed class GetMyLessonsQueryHandler : IRequestHandler<GetMyLessonsQuery, ApiResponse<List<MyLessonDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService _academicScope;
    private readonly IContentArchiveAccessService _archiveAccess;

    public GetMyLessonsQueryHandler(
        IAppDbContext db,
        IAcademicScopeService academicScope,
        IContentArchiveAccessService? archiveAccess = null)
    {
        _db = db;
        _academicScope = academicScope;
        _archiveAccess = archiveAccess ?? new ContentArchiveAccessService(db);
    }

    public async Task<ApiResponse<List<MyLessonDto>>> Handle(GetMyLessonsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var grants = await _db.StudentAccessGrants
            .AsNoTracking()
            .Where(grant => grant.UserId == request.UserId &&
                            grant.IsActive &&
                            (grant.ExpiresAt == null || grant.ExpiresAt > now) &&
                            grant.GrantType != CodeType.Video &&
                            grant.GrantType != CodeType.Exam &&
                            grant.GrantType != CodeType.Balance)
            .Select(grant => new
            {
                grant.GrantType,
                grant.PackageId,
                grant.TermId,
                grant.ContentSectionId,
                grant.LessonId
            })
            .ToListAsync(ct);

        if (grants.Count == 0)
            return ApiResponse<List<MyLessonDto>>.Ok([]);

        var packageIds = grants
            .Where(grant => grant.GrantType == CodeType.Package && grant.PackageId.HasValue)
            .Select(grant => grant.PackageId!.Value)
            .Distinct()
            .ToList();
        var termIds = grants
            .Where(grant => grant.GrantType == CodeType.Term && grant.TermId.HasValue)
            .Select(grant => grant.TermId!.Value)
            .Distinct()
            .ToList();
        var sectionIds = grants
            .Where(grant => grant.GrantType == CodeType.Month && grant.ContentSectionId.HasValue)
            .Select(grant => grant.ContentSectionId!.Value)
            .Distinct()
            .ToList();
        var lessonIds = grants
            .Where(grant => grant.GrantType == CodeType.Lesson && grant.LessonId.HasValue)
            .Select(grant => grant.LessonId!.Value)
            .Distinct()
            .ToList();

        var lessons = await _db.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.ContentSection.Term.Package.Teacher.IsContentVisibleToStudents &&
                (lessonIds.Contains(lesson.Id) ||
                 sectionIds.Contains(lesson.ContentSectionId) ||
                 termIds.Contains(lesson.ContentSection.TermId) ||
                 packageIds.Contains(lesson.ContentSection.Term.PackageId)))
            .OrderBy(lesson => lesson.ContentSection.Term.Package.Name)
            .ThenBy(lesson => lesson.ContentSection.Term.Order)
            .ThenBy(lesson => lesson.ContentSection.Order)
            .ThenBy(lesson => lesson.Order)
            .Select(lesson => new
            {
                lesson.Id,
                lesson.Title,
                lesson.Order,
                PackageId = lesson.ContentSection.Term.PackageId,
                PackageName = lesson.ContentSection.Term.Package.Name,
                TermTitle = lesson.ContentSection.Term.Title,
                SectionTitle = lesson.ContentSection.Title,
                TeacherName = lesson.ContentSection.Term.Package.Teacher.User.FullName,
                ImageUrl = lesson.ContentSection.ImageUrl ?? lesson.ContentSection.Term.Package.ImageUrl,
                VideoCount = lesson.Videos.Count(video => video.IsActive)
            })
            .ToListAsync(ct);

        var accessibleLessonIds = lessons.Select(lesson => lesson.Id).ToList();
        var completedLessonIds = (await _db.LessonProgresses
                .AsNoTracking()
                .Where(progress => progress.UserId == request.UserId &&
                                   progress.IsCompleted &&
                                   accessibleLessonIds.Contains(progress.LessonId))
                .Select(progress => progress.LessonId)
                .ToListAsync(ct))
            .ToHashSet();

        var result = new List<MyLessonDto>(lessons.Count);
        foreach (var lesson in lessons)
        {
            if (!await _academicScope.IsOwnerEligibleForStudentAsync(
                    StudentFacingScopeOwnerType.Lesson,
                    lesson.Id,
                    request.UserId,
                    ct) ||
                !await _archiveAccess.CanViewAsync(
                    request.UserId,
                    ContentArchiveTargetType.Lesson,
                    lesson.Id,
                    ct))
            {
                continue;
            }

            result.Add(new MyLessonDto(
                lesson.Id,
                lesson.Title,
                lesson.Order,
                lesson.PackageId,
                lesson.PackageName,
                lesson.TermTitle,
                lesson.SectionTitle,
                lesson.TeacherName,
                lesson.ImageUrl,
                completedLessonIds.Contains(lesson.Id),
                lesson.VideoCount));
        }

        return ApiResponse<List<MyLessonDto>>.Ok(result);
    }
}
