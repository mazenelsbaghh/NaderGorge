using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetUserDevicesQuery(Guid UserId) : IRequest<ApiResponse<List<DeviceDto>>>;

public record DeviceDto(Guid Id, string Fingerprint, string Browser, string Os, DateTime LastUsedAt, bool IsActive);

public class GetUserDevicesQueryHandler : IRequestHandler<GetUserDevicesQuery, ApiResponse<List<DeviceDto>>>
{
    private readonly IAppDbContext _db;

    public GetUserDevicesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<DeviceDto>>> Handle(GetUserDevicesQuery request, CancellationToken ct)
    {
        var devices = await _db.Devices
            .Where(d => d.UserId == request.UserId)
            .OrderByDescending(d => d.LastUsedAt)
            .ToListAsync(ct);

        var dtos = devices.Select(d => new DeviceDto(d.Id, d.DeviceFingerprint, d.DeviceName ?? "Unknown", d.IpAddress ?? "Unknown", d.LastUsedAt, d.IsActive)).ToList();

        return ApiResponse<List<DeviceDto>>.Ok(dtos);
    }
}
