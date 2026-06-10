using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetPackageByIdQuery(Guid Id, Guid? CurrentUserId = null) : IRequest<ApiResponse<PackageDetailDto>>;

public record PackageDetailDto(Guid Id, string Name, string Description, decimal Price, Guid ProgramId, bool IsActive, List<TermDto> Terms);
public record TermDto(Guid Id, string Title, int Order, decimal Price);

public class GetPackageByIdQueryHandler : IRequestHandler<GetPackageByIdQuery, ApiResponse<PackageDetailDto>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public GetPackageByIdQueryHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<PackageDetailDto>> Handle(GetPackageByIdQuery request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessPackageAsync(request.CurrentUserId.Value, request.Id, ct);
            if (!canAccess)
            {
                return ApiResponse<PackageDetailDto>.Fail("Unauthorized access to this package.");
            }
        }

        var package = await _db.Packages
            .Include(p => p.Terms)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);

        if (package == null)
            return ApiResponse<PackageDetailDto>.Fail("Package not found");

        var dtos = package.Terms.OrderBy(t => t.Order).Select(t => new TermDto(t.Id, t.Title, t.Order, t.Price)).ToList();

        var packageDto = new PackageDetailDto(
            package.Id,
            package.Name,
            package.Description,
            package.Price,
            package.SubjectId,
            package.IsActive,
            dtos);

        return ApiResponse<PackageDetailDto>.Ok(packageDto);
    }
}
