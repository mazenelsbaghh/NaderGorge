using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Android;

public record AndroidSyncStatusCommand(
    string PairingToken,
    decimal? CurrentBalance) : IRequest<ApiResponse<AndroidSyncStatusDto>>;

public class AndroidSyncStatusDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
    public decimal DailyReceived { get; set; }
    public decimal MonthlyReceived { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
    public List<string> SmsSenderFilters { get; set; } = new();
    public List<OtherDeviceDto> OtherDevices { get; set; } = new();
}

public class OtherDeviceDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public string DeviceStatus { get; set; } = string.Empty;
    public DateTime? LastSeenAt { get; set; }
}

public class AndroidSyncStatusCommandHandler : IRequestHandler<AndroidSyncStatusCommand, ApiResponse<AndroidSyncStatusDto>>
{
    private readonly IAppDbContext _db;

    public AndroidSyncStatusCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<AndroidSyncStatusDto>> Handle(AndroidSyncStatusCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PairingToken))
            return ApiResponse<AndroidSyncStatusDto>.Fail("pairing token invalid");

        var wallet = await _db.DigitalWallets
            .FirstOrDefaultAsync(w => w.PairingToken == request.PairingToken, ct);

        if (wallet == null)
            return ApiResponse<AndroidSyncStatusDto>.Fail("pairing token invalid");

        if (!wallet.IsActive)
            return ApiResponse<AndroidSyncStatusDto>.Fail("هذه المحفظة غير نشطة حالياً");

        // Update heartbeat and status
        wallet.DeviceStatus = "Connected";
        wallet.LastSeenAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Calculate limits received (Egypt Local Time)
        var egyptTime = DateTime.UtcNow.AddHours(3);
        var today = egyptTime.Date;
        var startOfMonth = new DateTime(egyptTime.Year, egyptTime.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var activeStatus = new[] { RechargeRequestStatus.Matched, RechargeRequestStatus.Approved };

        var rechargeRequests = await _db.RechargeRequests
            .Where(r => r.WalletId == wallet.Id && activeStatus.Contains(r.Status) && r.ResolvedAt >= startOfMonth.AddHours(-3))
            .ToListAsync(ct);

        var dailyReceived = rechargeRequests
            .Where(r => r.ResolvedAt.HasValue && r.ResolvedAt.Value.AddHours(3).Date == today)
            .Sum(r => r.Amount);

        var monthlyReceived = rechargeRequests
            .Where(r => r.ResolvedAt.HasValue && r.ResolvedAt.Value.AddHours(3) >= new DateTime(egyptTime.Year, egyptTime.Month, 1))
            .Sum(r => r.Amount);

        List<string> filters;
        try
        {
            filters = JsonSerializer.Deserialize<List<string>>(wallet.SmsSenderFilters) ?? new List<string>();
        }
        catch
        {
            filters = new List<string> { "VodafoneCash" };
        }

        // Fetch other devices
        var otherWallets = await _db.DigitalWallets
            .Where(w => w.Id != wallet.Id && w.IsActive)
            .ToListAsync(ct);

        var otherDevices = new List<OtherDeviceDto>();
        foreach (var ow in otherWallets)
        {
            var status = ow.DeviceStatus;
            if (status == "Connected" && ow.LastSeenAt.HasValue && DateTime.UtcNow - ow.LastSeenAt.Value > TimeSpan.FromMinutes(20))
            {
                status = "Disconnected";
            }

            otherDevices.Add(new OtherDeviceDto
            {
                PhoneNumber = ow.PhoneNumber,
                Label = ow.Label,
                CurrentBalance = ow.CurrentBalance,
                DeviceStatus = status,
                LastSeenAt = ow.LastSeenAt
            });
        }

        var result = new AndroidSyncStatusDto
        {
            PhoneNumber = wallet.PhoneNumber,
            Label = wallet.Label,
            DailyLimit = wallet.DailyLimit,
            MonthlyLimit = wallet.MonthlyLimit,
            DailyReceived = dailyReceived,
            MonthlyReceived = monthlyReceived,
            CurrentBalance = wallet.CurrentBalance,
            IsActive = wallet.IsActive,
            SmsSenderFilters = filters,
            OtherDevices = otherDevices
        };

        return ApiResponse<AndroidSyncStatusDto>.Ok(result, "تمت المزامنة وتحديث الحالة بنجاح");
    }
}
