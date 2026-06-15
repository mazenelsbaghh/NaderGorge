using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Content.Queries;

public record GetContentSubscribersQuery(
    string ContentType,
    Guid ContentId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<ApiResponse<ContentSubscribersPagedResult>>;

public record ContentSubscriberDto(
    Guid StudentId,
    string FullName,
    string Phone,
    string Governorate,
    string? District,
    string EducationStage,
    string GradeLevel,
    string? SchoolName,
    string? ParentPhone,
    string? MotherPhone,
    DateTime EnrolledAt,
    bool IsActive,
    string? AvatarSlug
);

public record ContentSubscribersPagedResult(
    List<ContentSubscriberDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public class GetContentSubscribersQueryHandler : IRequestHandler<GetContentSubscribersQuery, ApiResponse<ContentSubscribersPagedResult>>
{
    private readonly IAppDbContext _db;

    public GetContentSubscribersQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<ContentSubscribersPagedResult>> Handle(GetContentSubscribersQuery request, CancellationToken ct)
    {
        var grantType = MapContentType(request.ContentType);
        if (grantType is null)
            return ApiResponse<ContentSubscribersPagedResult>.Fail("Invalid content type");

        var query = _db.StudentAccessGrants
            .Where(sag => sag.GrantType == grantType.Value);

        query = request.ContentType.ToLowerInvariant() switch
        {
            "package" => query.Where(sag => sag.PackageId == request.ContentId),
            "term" => query.Where(sag => sag.TermId == request.ContentId),
            "section" => query.Where(sag => sag.ContentSectionId == request.ContentId),
            _ => query.Where(sag => false)
        };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(sag =>
                sag.User.FullName.ToLower().Contains(search) ||
                sag.User.PhoneNumber.Contains(search));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(sag => sag.GrantedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(sag => new ContentSubscriberDto(
                sag.UserId,
                sag.User.FullName,
                sag.User.PhoneNumber,
                sag.User.StudentProfile != null ? sag.User.StudentProfile.Governorate : "",
                sag.User.StudentProfile != null ? sag.User.StudentProfile.District : null,
                sag.User.StudentProfile != null ? sag.User.StudentProfile.EducationStage.ToString() : "",
                sag.User.StudentProfile != null ? sag.User.StudentProfile.GradeLevel.ToString() : "",
                sag.User.StudentProfile != null ? sag.User.StudentProfile.SchoolName : null,
                sag.User.StudentProfile != null ? sag.User.StudentProfile.ParentPhone : null,
                sag.User.StudentProfile != null ? sag.User.StudentProfile.MotherPhone : null,
                sag.GrantedAt,
                sag.IsActive,
                sag.User.StudentProfile != null ? sag.User.StudentProfile.AvatarSlug : null
            ))
            .ToListAsync(ct);

        return ApiResponse<ContentSubscribersPagedResult>.Ok(
            new ContentSubscribersPagedResult(items, totalCount, request.Page, request.PageSize));
    }

    private static CodeType? MapContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "package" => CodeType.Package,
            "term" => CodeType.Term,
            "section" => CodeType.Month,
            _ => null
        };
    }
}
