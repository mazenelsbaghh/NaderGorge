using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public class DisconnectStudentDeviceCommand : IRequest<ApiResponse>
{
    public Guid UserId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid AdminId { get; set; }

    public DisconnectStudentDeviceCommand(Guid userId, Guid? deviceId, Guid adminId)
    {
        UserId = userId;
        DeviceId = deviceId;
        AdminId = adminId;
    }
}

public class DisconnectStudentDeviceCommandHandler : IRequestHandler<DisconnectStudentDeviceCommand, ApiResponse>
{
    private readonly IAppDbContext _context;

    public DisconnectStudentDeviceCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse> Handle(DisconnectStudentDeviceCommand request, CancellationToken cancellationToken)
    {
        var devicesQuery = _context.Devices.Where(d => d.UserId == request.UserId);
        
        if (request.DeviceId.HasValue)
        {
            devicesQuery = devicesQuery.Where(d => d.Id == request.DeviceId.Value);
        }

        var devices = await devicesQuery.ToListAsync(cancellationToken);
        
        if (!devices.Any())
            return ApiResponse.Fail("No devices found.");

        int count = devices.Count;
        _context.Devices.RemoveRange(devices);

        var audit = new AuditLog
        {
            EntityType = "User",
            EntityId = request.UserId,
            Action = "DISCONNECT_DEVICE",
            PerformedByUserId = request.AdminId,
            OldValues = JsonSerializer.Serialize(new { disconnectedDevices = count }),
            NewValues = JsonSerializer.Serialize(new { targetDeviceId = request.DeviceId })
        };
        _context.AuditLogs.Add(audit);

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Ok($"Disconnected {count} device(s).");
    }
}
