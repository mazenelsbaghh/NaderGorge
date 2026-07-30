using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Teachers.Queries;

public record GetTeacherActivationsQuery(
    Guid TeacherId,
    int Page = 1,
    int PageSize = 20
) : IRequest<ApiResponse<TeacherActivationsPagedResult>>;

public record TeacherActivationDto(
    Guid Id,
    string StudentName,
    string PackageName,
    decimal Price,
    decimal CommissionRate,
    decimal CommissionEarned,
    DateTime ActivatedAt
);

public record TeacherActivationsPagedResult(
    List<TeacherActivationDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public class GetTeacherActivationsQueryHandler : IRequestHandler<GetTeacherActivationsQuery, ApiResponse<TeacherActivationsPagedResult>>
{
    private readonly IAppDbContext _db;

    public GetTeacherActivationsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<TeacherActivationsPagedResult>> Handle(GetTeacherActivationsQuery request, CancellationToken ct)
    {
        var query = _db.AccessCodeActivationLogs
            .Where(log => log.TeacherId == request.TeacherId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(log => log.ActivatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(log => new TeacherActivationDto(
                log.Id,
                log.Student.FullName,
                log.Package != null ? log.Package.Name : "",
                log.Price,
                log.CommissionRate,
                log.CommissionEarned,
                log.ActivatedAt
            ))
            .ToListAsync(ct);

        return ApiResponse<TeacherActivationsPagedResult>.Ok(
            new TeacherActivationsPagedResult(items, totalCount, request.Page, request.PageSize));
    }
}
