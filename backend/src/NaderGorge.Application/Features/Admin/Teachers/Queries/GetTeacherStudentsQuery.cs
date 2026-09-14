using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Content.Queries;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Teachers.Queries;

public record GetTeacherStudentsQuery(
    Guid TeacherId,
    int Page = 1,
    int PageSize = 20
) : IRequest<ApiResponse<TeacherStudentsPagedResult>>;

public record TeacherStudentDto(
    Guid StudentId,
    string FullName,
    string Phone,
    string? AvatarSlug,
    string PackageName,
    decimal Price,
    DateTime EnrolledAt,
    DateTime? LastWatchedAt,
    int WatchedVideosCount,
    IReadOnlyList<TeacherStudentPackageDto> Packages
);

public record TeacherStudentPackageDto(
    Guid PackageId,
    string PackageName,
    decimal Price,
    DateTime EnrolledAt
);

public record TeacherStudentsPagedResult(
    List<TeacherStudentDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public class GetTeacherStudentsQueryHandler : IRequestHandler<GetTeacherStudentsQuery, ApiResponse<TeacherStudentsPagedResult>>
{
    private readonly IAppDbContext _db;

    public GetTeacherStudentsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<TeacherStudentsPagedResult>> Handle(GetTeacherStudentsQuery request, CancellationToken ct)
    {
        // Get all package IDs belonging to this teacher
        var teacherPackageIds = await _db.Packages
            .Where(p => p.TeacherId == request.TeacherId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (teacherPackageIds.Count == 0)
            return ApiResponse<TeacherStudentsPagedResult>.Ok(
                new TeacherStudentsPagedResult(new List<TeacherStudentDto>(), 0, request.Page, request.PageSize));

        var now = DateTime.UtcNow;
        var scopedGrants = _db.StudentAccessGrants
            .Where(grant =>
                grant.IsActive &&
                !grant.CancelledAt.HasValue &&
                (!grant.ExpiresAt.HasValue || grant.ExpiresAt > now) &&
                ((grant.GrantType == CodeType.Package && grant.PackageId.HasValue && teacherPackageIds.Contains(grant.PackageId.Value)) ||
                 (grant.GrantType == CodeType.Term && grant.TermId.HasValue &&
                  _db.Terms.Any(term => term.Id == grant.TermId.Value && teacherPackageIds.Contains(term.PackageId))) ||
                 (grant.GrantType == CodeType.Month && grant.ContentSectionId.HasValue &&
                  _db.ContentSections.Any(section => section.Id == grant.ContentSectionId.Value && teacherPackageIds.Contains(section.Term.PackageId))) ||
                 (grant.GrantType == CodeType.Lesson && grant.LessonId.HasValue &&
                  _db.Lessons.Any(lesson => lesson.Id == grant.LessonId.Value && teacherPackageIds.Contains(lesson.ContentSection.Term.PackageId)))));
        var query = ContentSubscriberGrantQuery.RepresentativePerStudent(scopedGrants);

        var totalCount = await query.CountAsync(ct);

        var grants = await query
            .OrderByDescending(sag => sag.GrantedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(sag => new
            {
                sag.UserId,
                StudentName = sag.User.FullName,
                StudentPhone = sag.User.PhoneNumber,
                AvatarSlug = sag.User.StudentProfile != null ? sag.User.StudentProfile.AvatarSlug : null,
                PackageName = sag.GrantType == CodeType.Package && sag.PackageId.HasValue
                    ? _db.Packages.Where(package => package.Id == sag.PackageId).Select(package => package.Name).FirstOrDefault() ?? ""
                    : sag.GrantType == CodeType.Term && sag.TermId.HasValue
                        ? _db.Terms.Where(term => term.Id == sag.TermId).Select(term => term.Package.Name).FirstOrDefault() ?? ""
                        : sag.GrantType == CodeType.Month && sag.ContentSectionId.HasValue
                            ? _db.ContentSections.Where(section => section.Id == sag.ContentSectionId).Select(section => section.Term.Package.Name).FirstOrDefault() ?? ""
                            : sag.GrantType == CodeType.Lesson && sag.LessonId.HasValue
                                ? _db.Lessons.Where(lesson => lesson.Id == sag.LessonId).Select(lesson => lesson.ContentSection.Term.Package.Name).FirstOrDefault() ?? ""
                                : "",
                PackagePrice = sag.GrantType == CodeType.Package && sag.PackageId.HasValue
                    ? _db.Packages.Where(package => package.Id == sag.PackageId).Select(package => package.Price).FirstOrDefault()
                    : sag.GrantType == CodeType.Term && sag.TermId.HasValue
                        ? _db.Terms.Where(term => term.Id == sag.TermId).Select(term => term.Price).FirstOrDefault()
                        : sag.GrantType == CodeType.Month && sag.ContentSectionId.HasValue
                            ? _db.ContentSections.Where(section => section.Id == sag.ContentSectionId).Select(section => section.Price).FirstOrDefault()
                            : sag.GrantType == CodeType.Lesson && sag.LessonId.HasValue
                                ? _db.Lessons.Where(lesson => lesson.Id == sag.LessonId).Select(lesson => lesson.Price).FirstOrDefault()
                                : 0m,
                sag.GrantedAt
            })
            .ToListAsync(ct);

        // Get watch tracking data for these students
        var studentIds = grants.Select(g => g.UserId).Distinct().ToList();

        var membershipRows = await scopedGrants
            .Where(grant => studentIds.Contains(grant.UserId))
            .Select(grant => new
            {
                grant.UserId,
                PackageId = grant.GrantType == CodeType.Package && grant.PackageId.HasValue
                    ? grant.PackageId
                    : grant.GrantType == CodeType.Term && grant.TermId.HasValue
                        ? _db.Terms.Where(term => term.Id == grant.TermId).Select(term => (Guid?)term.PackageId).FirstOrDefault()
                        : grant.GrantType == CodeType.Month && grant.ContentSectionId.HasValue
                            ? _db.ContentSections.Where(section => section.Id == grant.ContentSectionId).Select(section => (Guid?)section.Term.PackageId).FirstOrDefault()
                            : grant.GrantType == CodeType.Lesson && grant.LessonId.HasValue
                                ? _db.Lessons.Where(lesson => lesson.Id == grant.LessonId).Select(lesson => (Guid?)lesson.ContentSection.Term.PackageId).FirstOrDefault()
                                : null,
                PackageName = grant.GrantType == CodeType.Package && grant.PackageId.HasValue
                    ? _db.Packages.Where(package => package.Id == grant.PackageId).Select(package => package.Name).FirstOrDefault() ?? ""
                    : grant.GrantType == CodeType.Term && grant.TermId.HasValue
                        ? _db.Terms.Where(term => term.Id == grant.TermId).Select(term => term.Package.Name).FirstOrDefault() ?? ""
                        : grant.GrantType == CodeType.Month && grant.ContentSectionId.HasValue
                            ? _db.ContentSections.Where(section => section.Id == grant.ContentSectionId).Select(section => section.Term.Package.Name).FirstOrDefault() ?? ""
                            : grant.GrantType == CodeType.Lesson && grant.LessonId.HasValue
                                ? _db.Lessons.Where(lesson => lesson.Id == grant.LessonId).Select(lesson => lesson.ContentSection.Term.Package.Name).FirstOrDefault() ?? ""
                                : "",
                Price = grant.GrantType == CodeType.Package && grant.PackageId.HasValue
                    ? _db.Packages.Where(package => package.Id == grant.PackageId).Select(package => package.Price).FirstOrDefault()
                    : grant.GrantType == CodeType.Term && grant.TermId.HasValue
                        ? _db.Terms.Where(term => term.Id == grant.TermId).Select(term => term.Price).FirstOrDefault()
                        : grant.GrantType == CodeType.Month && grant.ContentSectionId.HasValue
                            ? _db.ContentSections.Where(section => section.Id == grant.ContentSectionId).Select(section => section.Price).FirstOrDefault()
                            : grant.GrantType == CodeType.Lesson && grant.LessonId.HasValue
                                ? _db.Lessons.Where(lesson => lesson.Id == grant.LessonId).Select(lesson => lesson.Price).FirstOrDefault()
                                : 0m,
                grant.GrantedAt
            })
            .ToListAsync(ct);
        var membershipsByStudent = membershipRows
            .Where(row => row.PackageId.HasValue)
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TeacherStudentPackageDto>)group
                    .GroupBy(row => row.PackageId!.Value)
                    .Select(packageGroup => packageGroup.OrderByDescending(row => row.GrantedAt).First())
                    .OrderBy(row => row.PackageName)
                    .Select(row => new TeacherStudentPackageDto(
                        row.PackageId!.Value,
                        row.PackageName,
                        row.Price,
                        row.GrantedAt))
                    .ToList());

        var watchData = await _db.VideoWatchEvents
            .Where(vwe =>
                studentIds.Contains(vwe.UserId) &&
                teacherPackageIds.Contains(vwe.LessonVideo.Lesson.ContentSection.Term.PackageId))
            .GroupBy(vwe => vwe.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                LastWatchedAt = g.Max(vwe => vwe.UpdatedAt ?? vwe.CreatedAt),
                WatchedVideosCount = g.Select(vwe => vwe.LessonVideoId).Distinct().Count()
            })
            .ToListAsync(ct);

        var watchLookup = watchData.ToDictionary(w => w.UserId);

        var dtos = grants.Select(g =>
        {
            watchLookup.TryGetValue(g.UserId, out var watch);
            var memberships = membershipsByStudent.GetValueOrDefault(g.UserId) ?? [];
            return new TeacherStudentDto(
                g.UserId,
                g.StudentName,
                g.StudentPhone,
                g.AvatarSlug,
                g.PackageName,
                g.PackagePrice,
                g.GrantedAt,
                watch?.LastWatchedAt,
                watch?.WatchedVideosCount ?? 0,
                memberships
            );
        }).ToList();

        return ApiResponse<TeacherStudentsPagedResult>.Ok(
            new TeacherStudentsPagedResult(dtos, totalCount, request.Page, request.PageSize));
    }
}
