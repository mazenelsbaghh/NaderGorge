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

namespace NaderGorge.Application.Features.Admin.Wallets;

public record GetWalletsQuery : IRequest<ApiResponse<List<WalletDto>>>;

public class GetWalletsQueryHandler : IRequestHandler<GetWalletsQuery, ApiResponse<List<WalletDto>>>
{
    private readonly IAppDbContext _db;

    public GetWalletsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<WalletDto>>> Handle(GetWalletsQuery request, CancellationToken ct)
    {
        var wallets = await _db.DigitalWallets
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

        var egyptTime = DateTime.UtcNow.AddHours(3);
        var today = egyptTime.Date;
        var startOfMonth = new DateTime(egyptTime.Year, egyptTime.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var activeStatus = new[] { RechargeRequestStatus.Matched, RechargeRequestStatus.Approved };

        // Fetch successful recharge requests in this month for capacity calculation
        var rechargeRequests = await _db.RechargeRequests
            .Where(r => activeStatus.Contains(r.Status) && r.ResolvedAt >= startOfMonth.AddHours(-3)) // Buffer to catch all local month starts
            .ToListAsync(ct);

        var walletDtos = new List<WalletDto>();

        foreach (var w in wallets)
        {
            // Calculate Daily Received (resolved today in Egypt time)
            var dailyReceived = rechargeRequests
                .Where(r => r.WalletId == w.Id && r.ResolvedAt.HasValue && r.ResolvedAt.Value.AddHours(3).Date == today)
                .Sum(r => r.Amount);

            // Calculate Monthly Received (resolved this month in Egypt time)
            var monthlyReceived = rechargeRequests
                .Where(r => r.WalletId == w.Id && r.ResolvedAt.HasValue && r.ResolvedAt.Value.AddHours(3) >= new DateTime(egyptTime.Year, egyptTime.Month, 1))
                .Sum(r => r.Amount);

            List<string> filters;
            try
            {
                filters = JsonSerializer.Deserialize<List<string>>(w.SmsSenderFilters) ?? new List<string>();
            }
            catch
            {
                filters = new List<string> { "VodafoneCash" };
            }

            // Check heartbeat timeout to auto-disconnect device after 2 minutes
            var status = w.DeviceStatus;
            if (status == "Connected" && w.LastSeenAt.HasValue && DateTime.UtcNow - w.LastSeenAt.Value > TimeSpan.FromMinutes(2))
            {
                status = "Disconnected";
                w.DeviceStatus = "Disconnected";
                // We don't save changes here to avoid side effects in a query handler,
                // but we return the correct status to the admin.
            }

            walletDtos.Add(new WalletDto
            {
                Id = w.Id,
                PhoneNumber = w.PhoneNumber,
                Label = w.Label,
                DailyLimit = w.DailyLimit,
                MonthlyLimit = w.MonthlyLimit,
                CurrentBalance = w.CurrentBalance,
                PairingToken = w.PairingToken,
                DeviceStatus = status,
                LastSeenAt = w.LastSeenAt,
                IsActive = w.IsActive,
                SmsSenderFilters = filters,
                DailyReceived = dailyReceived,
                MonthlyReceived = monthlyReceived,
                CreatedAt = w.CreatedAt
            });
        }

        return ApiResponse<List<WalletDto>>.Ok(walletDtos);
    }
}
