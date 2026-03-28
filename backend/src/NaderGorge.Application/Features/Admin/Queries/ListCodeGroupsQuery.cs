using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record ListCodeGroupsQuery() : IRequest<ApiResponse<List<CodeGroupDto>>>;

public record CodeGroupDto(Guid Id, DateTime CreatedAt, Guid? PackageId, Guid? LessonId, int CodeCount, int UsedCount);

public class ListCodeGroupsQueryHandler : IRequestHandler<ListCodeGroupsQuery, ApiResponse<List<CodeGroupDto>>>
{
    private readonly IAppDbContext _db;

    public ListCodeGroupsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<CodeGroupDto>>> Handle(ListCodeGroupsQuery request, CancellationToken ct)
    {
        var groups = await _db.CodeGroups
            .Include(cg => cg.AccessCodes)
            .OrderByDescending(cg => cg.CreatedAt)
            .ToListAsync(ct);

        var dtos = groups.Select(cg => new CodeGroupDto(
            cg.Id,
            cg.CreatedAt,
            cg.PackageId,
            cg.LessonId,
            cg.AccessCodes.Count,
            cg.AccessCodes.Count(c => c.IsConsumed)
        )).ToList();

        return ApiResponse<List<CodeGroupDto>>.Ok(dtos);
    }
}
