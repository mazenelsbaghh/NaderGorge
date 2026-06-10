using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetAdminPackagesListQuery(System.Guid? CurrentUserId = null) : IRequest<ApiResponse<List<AdminPackageListItemDto>>>;

public record AdminPackageListItemDto(System.Guid Id, string Name, System.Guid TeacherId, System.Guid SubjectId);

public class GetAdminPackagesListQueryHandler : IRequestHandler<GetAdminPackagesListQuery, ApiResponse<List<AdminPackageListItemDto>>>
{
    private readonly IAppDbContext _context;

    public GetAdminPackagesListQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<AdminPackageListItemDto>>> Handle(GetAdminPackagesListQuery request, CancellationToken cancellationToken)
    {
        Guid? teacherId = null;
        if (request.CurrentUserId.HasValue)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.TeacherProfile)
                .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId.Value, cancellationToken);

            if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
            {
                teacherId = user.TeacherProfile?.Id;
            }
        }

        var query = _context.Packages
            .Where(p => p.IsActive);

        if (teacherId.HasValue)
        {
            query = query.Where(p => p.TeacherId == teacherId.Value);
        }

        var packages = await query
            .OrderBy(p => p.Name)
            .Select(p => new AdminPackageListItemDto(
                p.Id, 
                p.Name, 
                p.TeacherId, 
                p.SubjectId
            ))
            .ToListAsync(cancellationToken);

        return ApiResponse<List<AdminPackageListItemDto>>.Ok(packages);
    }
}
