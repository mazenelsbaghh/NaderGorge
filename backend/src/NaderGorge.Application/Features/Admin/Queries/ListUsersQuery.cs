using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record ListUsersQuery(int Page = 1, int PageSize = 20, string? Search = null) : IRequest<ApiResponse<PagedResult<AdminUserListDto>>>;

public record AdminUserListDto(Guid Id, string PhoneNumber, string Status, string FullName, string Grade, string Track, DateTime CreatedAt);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, ApiResponse<PagedResult<AdminUserListDto>>>
{
    private readonly IAppDbContext _db;

    public ListUsersQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<PagedResult<AdminUserListDto>>> Handle(ListUsersQuery request, CancellationToken ct)
    {
        var query = _db.Users.Include(u => u.StudentProfile).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(u => u.PhoneNumber.Contains(request.Search) || 
                                     u.FullName.Contains(request.Search));
        }

        var total = await query.CountAsync(ct);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = users.Select(u => new AdminUserListDto(
            u.Id,
            u.PhoneNumber,
            u.IsActive ? "Active" : "Disabled",
            u.FullName,
            u.StudentProfile?.Grade ?? "N/A",
            u.StudentProfile?.Track ?? "N/A",
            u.CreatedAt
        )).ToList();

        return ApiResponse<PagedResult<AdminUserListDto>>.Ok(new PagedResult<AdminUserListDto>(dtos, total, request.Page, request.PageSize));
    }
}
