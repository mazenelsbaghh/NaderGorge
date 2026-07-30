using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Finance.Queries;

public record GetCodeAccountingQuery(
    Guid? TeacherId = null,
    Guid? PackageId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<AdminCodeAccountingDto>>>;

public record AdminCodeAccountingDto(
    Guid Id,
    string PackageName,
    string TeacherName,
    string StudentName,
    long SerialNumber,
    decimal Price,
    decimal CommissionRate,
    decimal CommissionEarned,
    DateTime ActivatedAt
);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public class GetCodeAccountingQueryHandler : IRequestHandler<GetCodeAccountingQuery, ApiResponse<PagedResult<AdminCodeAccountingDto>>>
{
    private readonly IAppDbContext _db;

    public GetCodeAccountingQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<PagedResult<AdminCodeAccountingDto>>> Handle(GetCodeAccountingQuery request, CancellationToken ct)
    {
        var query = _db.AccessCodeActivationLogs
            .Include(l => l.Student)
            .Include(l => l.Package)
            .Include(l => l.Teacher).ThenInclude(t => t.User)
            .Include(l => l.AccessCode).ThenInclude(c => c.CodeGroup)
            .AsQueryable();

        if (request.TeacherId.HasValue)
        {
            query = query.Where(l => l.TeacherId == request.TeacherId.Value);
        }

        if (request.PackageId.HasValue)
        {
            query = query.Where(l => l.PackageId == request.PackageId.Value);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(l => l.ActivatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(l => l.ActivatedAt <= request.EndDate.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(l => l.ActivatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(l => new AdminCodeAccountingDto(
            l.Id,
            l.Package?.Name ?? l.AccessCode.CodeGroup.Name,
            l.Teacher.User?.FullName ?? "Unknown Teacher",
            l.Student?.FullName ?? "Unknown Student",
            l.AccessCode.SerialNumber,
            l.Price,
            l.CommissionRate,
            l.CommissionEarned,
            l.ActivatedAt
        )).ToList();

        return ApiResponse<PagedResult<AdminCodeAccountingDto>>.Ok(
            new PagedResult<AdminCodeAccountingDto>(dtos, totalCount, request.Page, request.PageSize)
        );
    }
}
