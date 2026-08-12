using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
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
    string? AvatarSlug,
    string PurchaseType,
    string PurchaseMethod
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
        if (ContentSubscriberGrantQuery.MapContentType(request.ContentType) is null)
            return ApiResponse<ContentSubscribersPagedResult>.Fail("Invalid content type");

        var matchingGrants = ContentSubscriberGrantQuery.Build(
            _db,
            request.ContentType,
            request.ContentId,
            request.Search);
        var query = ContentSubscriberGrantQuery.RepresentativePerStudent(matchingGrants);
        var balanceStudentIds = ContentSubscriberGrantQuery.BalanceStudentIds(
            _db,
            request.ContentType,
            request.ContentId);
        var totalCount = await query.CountAsync(ct);
        var now = DateTime.UtcNow;
        var activeStudentIds = matchingGrants
            .Where(grant => grant.IsActive && (!grant.ExpiresAt.HasValue || grant.ExpiresAt > now))
            .Select(grant => grant.UserId)
            .Distinct();

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
                activeStudentIds.Contains(sag.UserId),
                sag.User.StudentProfile != null ? sag.User.StudentProfile.AvatarSlug : null,
                sag.GrantType.ToString(),
                sag.AccessCodeId != null ? "Code" : sag.GiftRecipientId != null ? "Gift" : balanceStudentIds.Contains(sag.UserId) ? "Balance" : "Direct"
            ))
            .ToListAsync(ct);

        return ApiResponse<ContentSubscribersPagedResult>.Ok(
            new ContentSubscribersPagedResult(items, totalCount, request.Page, request.PageSize));
    }
}
