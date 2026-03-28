using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetPackagesQuery(Guid UserId) : IRequest<ApiResponse<List<PackageDto>>>;

public record PackageDto(Guid Id, string Name, string Description, decimal Price, Guid ProgramId, bool IsEnrolled);

public class GetPackagesQueryHandler : IRequestHandler<GetPackagesQuery, ApiResponse<List<PackageDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public GetPackagesQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<List<PackageDto>>> Handle(GetPackagesQuery request, CancellationToken ct)
    {
        var packages = await _db.Packages.ToListAsync(ct);
        
        var dtos = new List<PackageDto>();
        foreach (var pk in packages)
        {
            var isEnrolled = await _access.HasAccessToPackageAsync(request.UserId, pk.Id, ct);
            dtos.Add(new PackageDto(pk.Id, pk.Name, pk.Description, pk.Price, pk.ProgramId, isEnrolled));
        }

        return ApiResponse<List<PackageDto>>.Ok(dtos);
    }
}
