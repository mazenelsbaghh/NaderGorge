using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
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
    DateTime EnrolledAt,
    DateTime? LastWatchedAt,
    int WatchedVideosCount
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

        // Query StudentAccessGrants that are package-level enrollments for teacher's packages
        var query = _db.StudentAccessGrants
            .Where(sag => sag.PackageId != null
                && sag.IsActive
                && sag.GrantType == CodeType.Package
                && teacherPackageIds.Contains(sag.PackageId!.Value));

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
                PackageName = sag.PackageId != null
                    ? _db.Packages.Where(p => p.Id == sag.PackageId).Select(p => p.Name).FirstOrDefault() ?? ""
                    : "",
                sag.GrantedAt
            })
            .ToListAsync(ct);

        // Get watch tracking data for these students
        var studentIds = grants.Select(g => g.UserId).Distinct().ToList();

        var watchData = await _db.VideoWatchEvents
            .Where(vwe => studentIds.Contains(vwe.UserId))
            .GroupBy(vwe => vwe.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                LastWatchedAt = g.Max(vwe => vwe.UpdatedAt ?? vwe.CreatedAt),
                WatchedVideosCount = g.Count()
            })
            .ToListAsync(ct);

        var watchLookup = watchData.ToDictionary(w => w.UserId);

        var dtos = grants.Select(g =>
        {
            watchLookup.TryGetValue(g.UserId, out var watch);
            return new TeacherStudentDto(
                g.UserId,
                g.StudentName,
                g.StudentPhone,
                g.AvatarSlug,
                g.PackageName,
                g.GrantedAt,
                watch?.LastWatchedAt,
                watch?.WatchedVideosCount ?? 0
            );
        }).ToList();

        return ApiResponse<TeacherStudentsPagedResult>.Ok(
            new TeacherStudentsPagedResult(dtos, totalCount, request.Page, request.PageSize));
    }
}
