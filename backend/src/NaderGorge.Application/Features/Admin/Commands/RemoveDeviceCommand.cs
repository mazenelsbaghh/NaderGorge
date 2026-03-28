using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record RemoveDeviceCommand(Guid DeviceId, Guid AdminId) : IRequest<ApiResponse>;

public class RemoveDeviceCommandHandler : IRequestHandler<RemoveDeviceCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public RemoveDeviceCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(RemoveDeviceCommand request, CancellationToken ct)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == request.DeviceId, ct);
        if (device == null) return ApiResponse.Fail("Device not found");

        var userId = device.UserId;
        var fingerprint = device.DeviceFingerprint;

        _db.Devices.Remove(device);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "RemoveDevice",
            EntityType = "Device",
            EntityId = device.Id,
            PerformedByUserId = request.AdminId,
            OldValues = $"Removed device {fingerprint} for user {userId}",
            IpAddress = "System"
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("Device successfully removed.");
    }
}
