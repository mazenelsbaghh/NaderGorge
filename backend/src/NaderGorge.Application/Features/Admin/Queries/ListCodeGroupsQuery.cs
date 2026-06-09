using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record ListCodeGroupsQuery(Guid? CurrentUserId = null) : IRequest<ApiResponse<List<CodeGroupDto>>>;

public record CodeGroupDto(Guid Id, string Name, DateTime CreatedAt, Guid? PackageId, Guid? LessonId, int CodeCount, int UsedCount, Guid TeacherId);

public class ListCodeGroupsQueryHandler : IRequestHandler<ListCodeGroupsQuery, ApiResponse<List<CodeGroupDto>>>
{
    private readonly IAppDbContext _db;

    public ListCodeGroupsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<CodeGroupDto>>> Handle(ListCodeGroupsQuery request, CancellationToken ct)
    {
        Guid? teacherId = null;
        if (request.CurrentUserId.HasValue)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.TeacherProfile)
                .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId.Value, ct);

            if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
            {
                teacherId = user.TeacherProfile?.Id;
            }
        }

        var query = _db.CodeGroups.AsQueryable();

        if (teacherId.HasValue)
        {
            query = query.Where(cg => cg.TeacherId == teacherId.Value);
        }

        var groups = await query
            .Include(cg => cg.AccessCodes)
            .OrderByDescending(cg => cg.CreatedAt)
            .ToListAsync(ct);

        var dtos = groups.Select(cg => new CodeGroupDto(
            cg.Id,
            cg.Name,
            cg.CreatedAt,
            cg.PackageId,
            cg.LessonId,
            cg.AccessCodes.Count,
            cg.AccessCodes.Count(c => c.IsConsumed),
            cg.TeacherId
        )).ToList();

        return ApiResponse<List<CodeGroupDto>>.Ok(dtos);
    }
}
