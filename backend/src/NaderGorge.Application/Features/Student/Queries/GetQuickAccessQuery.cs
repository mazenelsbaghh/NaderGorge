using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetQuickAccessQuery(Guid UserId) : IRequest<ApiResponse<List<QuickAccessItemDto>>>;

public record QuickAccessItemDto(
    string Title,
    string PathBreadcrumb,
    string Url,
    CodeType AccessType,
    Guid? PackageId = null,
    string? ParentUrl = null,
    string? ImageUrl = null,
    string? TeacherName = null,
    string? TeacherProfileImageUrl = null,
    string? Badge = null
);

public class GetQuickAccessQueryHandler : IRequestHandler<GetQuickAccessQuery, ApiResponse<List<QuickAccessItemDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService _academicScope;

    public GetQuickAccessQueryHandler(IAppDbContext db, IAcademicScopeService academicScope)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<List<QuickAccessItemDto>>> Handle(GetQuickAccessQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var grants = await _db.StudentAccessGrants
            .Include(g => g.AccessCode)
                .ThenInclude(c => c!.CodeGroup)
            .Where(g => g.UserId == request.UserId && g.IsActive &&
                        (g.ExpiresAt == null || g.ExpiresAt > now) &&
                        g.GrantType != CodeType.Package)
            .OrderByDescending(g => g.GrantedAt)
            .ToListAsync(ct);

        var list = new List<QuickAccessItemDto>();

        foreach (var grant in grants)
        {
            if (grant.GrantType == CodeType.Term && grant.TermId.HasValue)
            {
                if (!await IsEligibleAsync(StudentFacingScopeOwnerType.Term, grant.TermId.Value, request.UserId, ct))
                    continue;

                var term = await _db.Terms
                    .Include(t => t.Package)
                        .ThenInclude(p => p.Teacher)
                            .ThenInclude(t => t.User)
                    .FirstOrDefaultAsync(t => t.Id == grant.TermId.Value, ct);

                if (term != null)
                {
                    list.Add(new QuickAccessItemDto(
                        term.Title,
                        $"{term.Package?.Name ?? "بدون باقة"} > {term.Title}",
                        $"/student/packages/{term.PackageId}/terms/{term.Id}",
                        CodeType.Term,
                        term.PackageId,
                        $"/student/packages/{term.PackageId}",
                        term.Package?.ImageUrl,
                        term.Package?.Teacher?.User?.FullName,
                        term.Package?.Teacher?.ProfileImageUrl,
                        "ترم"
                    ));
                }
            }
            else if (grant.GrantType == CodeType.Month && grant.ContentSectionId.HasValue)
            {
                if (!await IsEligibleAsync(StudentFacingScopeOwnerType.ContentSection, grant.ContentSectionId.Value, request.UserId, ct))
                    continue;

                var section = await _db.ContentSections
                    .Include(s => s.Term)
                        .ThenInclude(t => t.Package)
                            .ThenInclude(p => p.Teacher)
                                .ThenInclude(t => t.User)
                    .FirstOrDefaultAsync(s => s.Id == grant.ContentSectionId.Value, ct);

                if (section != null)
                {
                    list.Add(new QuickAccessItemDto(
                        section.Title,
                        $"{section.Term?.Package?.Name ?? "بدون باقة"} > {section.Term?.Title ?? "بدون ترم"} > {section.Title}",
                        $"/student/packages/{section.Term?.PackageId}/terms/{section.TermId}/sections/{section.Id}",
                        CodeType.Month,
                        section.Term?.PackageId,
                        section.Term != null ? $"/student/packages/{section.Term.PackageId}/terms/{section.TermId}" : null,
                        section.ImageUrl ?? section.Term?.Package?.ImageUrl,
                        section.Term?.Package?.Teacher?.User?.FullName,
                        section.Term?.Package?.Teacher?.ProfileImageUrl,
                        "شهر"
                    ));
                }
            }
            else if (grant.GrantType == CodeType.Lesson && grant.LessonId.HasValue)
            {
                if (!await IsEligibleAsync(StudentFacingScopeOwnerType.Lesson, grant.LessonId.Value, request.UserId, ct))
                    continue;

                var lesson = await _db.Lessons
                    .Include(l => l.ContentSection)
                        .ThenInclude(s => s.Term)
                            .ThenInclude(t => t.Package)
                                .ThenInclude(p => p.Teacher)
                                    .ThenInclude(t => t.User)
                    .FirstOrDefaultAsync(l => l.Id == grant.LessonId.Value, ct);

                if (lesson != null)
                {
                    list.Add(new QuickAccessItemDto(
                        lesson.Title,
                        $"{lesson.ContentSection?.Term?.Package?.Name ?? "بدون باقة"} > {lesson.ContentSection?.Term?.Title ?? "بدون ترم"} > {lesson.ContentSection?.Title ?? "بدون شهر"} > {lesson.Title}",
                        $"/student/packages/{lesson.ContentSection?.Term?.PackageId}/lessons/{lesson.Id}",
                        CodeType.Lesson,
                        lesson.ContentSection?.Term?.PackageId,
                        lesson.ContentSection?.Term != null ? $"/student/packages/{lesson.ContentSection.Term.PackageId}/terms/{lesson.ContentSection.TermId}/sections/{lesson.ContentSectionId}" : null,
                        lesson.ContentSection?.ImageUrl ?? lesson.ContentSection?.Term?.Package?.ImageUrl,
                        lesson.ContentSection?.Term?.Package?.Teacher?.User?.FullName,
                        lesson.ContentSection?.Term?.Package?.Teacher?.ProfileImageUrl,
                        "حصة"
                    ));
                }
            }
            else if (grant.GrantType == CodeType.Video)
            {
                var query = _db.LessonVideos
                    .AsNoTracking()
                    .Where(v => v.IsActive);

                if (grant.LessonVideoId.HasValue)
                {
                    query = query.Where(v => v.Id == grant.LessonVideoId.Value);
                }
                else if (grant.VideoTypeId.HasValue)
                {
                    var codeGroupTeacherId = grant.AccessCode?.CodeGroup?.TeacherId;
                    query = query.Where(v =>
                        v.VideoTypeId == grant.VideoTypeId.Value &&
                        (grant.LessonId == null || v.LessonId == grant.LessonId) &&
                        (grant.ContentSectionId == null || v.Lesson.ContentSectionId == grant.ContentSectionId) &&
                        (grant.TermId == null || v.Lesson.ContentSection.TermId == grant.TermId) &&
                        (grant.PackageId == null || v.Lesson.ContentSection.Term.PackageId == grant.PackageId) &&
                        (codeGroupTeacherId == null || v.Lesson.ContentSection.Term.Package.TeacherId == codeGroupTeacherId));
                }
                else
                {
                    continue;
                }

                var videos = await query
                    .OrderBy(v => v.Lesson.ContentSection.Term.Package.Name)
                    .ThenBy(v => v.Lesson.ContentSection.Term.Order)
                    .ThenBy(v => v.Lesson.ContentSection.Order)
                    .ThenBy(v => v.Lesson.Order)
                    .ThenBy(v => v.Order)
                    .Take(60)
                    .Select(v => new
                    {
                        v.Id,
                        v.Title,
                        v.LessonId,
                        VideoTypeName = v.VideoType.Name,
                        LessonTitle = v.Lesson.Title,
                        SectionId = v.Lesson.ContentSectionId,
                        SectionTitle = v.Lesson.ContentSection.Title,
                        SectionImageUrl = v.Lesson.ContentSection.ImageUrl,
                        TermId = v.Lesson.ContentSection.TermId,
                        TermTitle = v.Lesson.ContentSection.Term.Title,
                        PackageId = v.Lesson.ContentSection.Term.PackageId,
                        PackageName = v.Lesson.ContentSection.Term.Package.Name,
                        PackageImageUrl = v.Lesson.ContentSection.Term.Package.ImageUrl,
                        TeacherName = v.Lesson.ContentSection.Term.Package.Teacher.User.FullName,
                        TeacherProfileImageUrl = v.Lesson.ContentSection.Term.Package.Teacher.ProfileImageUrl
                    })
                    .ToListAsync(ct);

                foreach (var video in videos)
                {
                    if (!await IsEligibleAsync(StudentFacingScopeOwnerType.LessonVideo, video.Id, request.UserId, ct))
                        continue;

                    var url = $"/student/packages/{video.PackageId}/lessons/{video.LessonId}?videoId={video.Id}";
                    if (list.Any(item => item.AccessType == CodeType.Video && item.Url == url))
                        continue;

                    list.Add(new QuickAccessItemDto(
                        video.Title,
                        $"{video.PackageName} > {video.TermTitle} > {video.SectionTitle} > {video.LessonTitle}",
                        url,
                        CodeType.Video,
                        video.PackageId,
                        $"/student/packages/{video.PackageId}/terms/{video.TermId}/sections/{video.SectionId}",
                        video.SectionImageUrl ?? video.PackageImageUrl,
                        video.TeacherName,
                        video.TeacherProfileImageUrl,
                        video.VideoTypeName
                    ));
                }
            }
        }

        return ApiResponse<List<QuickAccessItemDto>>.Ok(list);
    }

    private async Task<bool> IsEligibleAsync(
        StudentFacingScopeOwnerType ownerType,
        Guid ownerId,
        Guid userId,
        CancellationToken ct)
    {
        return await _academicScope.IsOwnerEligibleForStudentAsync(ownerType, ownerId, userId, ct);
    }
}
