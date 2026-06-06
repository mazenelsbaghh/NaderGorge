using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetAdminPackagesListQuery : IRequest<ApiResponse<List<AdminPackageListItemDto>>>;

public record AdminPackageListItemDto(System.Guid Id, string Name);

public class GetAdminPackagesListQueryHandler : IRequestHandler<GetAdminPackagesListQuery, ApiResponse<List<AdminPackageListItemDto>>>
{
    private readonly IAppDbContext _context;

    public GetAdminPackagesListQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<AdminPackageListItemDto>>> Handle(GetAdminPackagesListQuery request, CancellationToken cancellationToken)
    {
        var packages = await _context.Packages
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new AdminPackageListItemDto(p.Id, p.Name))
            .ToListAsync(cancellationToken);

        return ApiResponse<List<AdminPackageListItemDto>>.Ok(packages);
    }
}
