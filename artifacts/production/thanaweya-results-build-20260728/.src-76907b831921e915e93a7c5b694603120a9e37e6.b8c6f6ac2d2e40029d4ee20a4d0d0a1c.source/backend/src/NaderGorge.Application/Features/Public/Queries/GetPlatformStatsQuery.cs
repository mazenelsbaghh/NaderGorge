using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Public.Queries;

public record PlatformStatsDto(int RegisteredStudentsCount);

public record GetPlatformStatsQuery() : IRequest<ApiResponse<PlatformStatsDto>>;

public class GetPlatformStatsQueryHandler : IRequestHandler<GetPlatformStatsQuery, ApiResponse<PlatformStatsDto>>
{
    private readonly IAppDbContext _db;

    public GetPlatformStatsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<PlatformStatsDto>> Handle(GetPlatformStatsQuery request, CancellationToken ct)
    {
        var registeredStudentsCount = await _db.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.Role.Type == RoleType.Student)
            .Select(userRole => userRole.UserId)
            .Distinct()
            .CountAsync(ct);

        return ApiResponse<PlatformStatsDto>.Ok(new PlatformStatsDto(registeredStudentsCount));
    }
}
