using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Teacher.Finance.Queries;

public record GetTeacherTransactionsQuery(
    Guid TeacherUserId,
    int Page = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<TeacherTransactionDto>>>;

public record TeacherTransactionDto(
    Guid Id,
    string PackageName,
    string StudentName,
    long SerialNumber,
    decimal Price,
    decimal CommissionRate,
    decimal CommissionEarned,
    DateTime ActivatedAt
);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public class GetTeacherTransactionsQueryHandler : IRequestHandler<GetTeacherTransactionsQuery, ApiResponse<PagedResult<TeacherTransactionDto>>>
{
    private readonly IAppDbContext _db;

    public GetTeacherTransactionsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<PagedResult<TeacherTransactionDto>>> Handle(GetTeacherTransactionsQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<PagedResult<TeacherTransactionDto>>.Fail("حساب المعلم غير موجود");
        }

        var query = _db.AccessCodeActivationLogs
            .Include(l => l.Student)
            .Include(l => l.Package)
            .Include(l => l.AccessCode).ThenInclude(c => c.CodeGroup)
            .Where(l => l.TeacherId == teacherProfile.Id);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(l => l.ActivatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(l => new TeacherTransactionDto(
            l.Id,
            l.Package?.Name ?? l.AccessCode.CodeGroup.Name,
            l.Student?.FullName ?? "Unknown Student",
            l.AccessCode.SerialNumber,
            l.Price,
            l.CommissionRate,
            l.CommissionEarned,
            l.ActivatedAt
        )).ToList();

        return ApiResponse<PagedResult<TeacherTransactionDto>>.Ok(
            new PagedResult<TeacherTransactionDto>(dtos, totalCount, request.Page, request.PageSize)
        );
    }
}
