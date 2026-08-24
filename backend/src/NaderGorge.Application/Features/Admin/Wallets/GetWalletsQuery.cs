using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
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

        var (dayStartUtc, dayEndUtc) = CairoTime.GetCurrentDayRangeUtc();
        var (monthStartUtc, monthEndUtc) = CairoTime.GetCurrentMonthRangeUtc();

        var activeStatus = new[] { RechargeRequestStatus.Matched, RechargeRequestStatus.Approved };

        // Fetch successful recharge requests in this month for capacity calculation
        var rechargeRequests = await _db.RechargeRequests
            .Where(r => activeStatus.Contains(r.Status) && r.ResolvedAt >= monthStartUtc && r.ResolvedAt < monthEndUtc)
            .ToListAsync(ct);
        var totalReceivedByWallet = await _db.RechargeRequests
            .Where(r => activeStatus.Contains(r.Status))
            .GroupBy(r => r.WalletId)
            .Select(group => new { WalletId = group.Key, TotalReceived = group.Sum(item => item.Amount) })
            .ToDictionaryAsync(item => item.WalletId, item => item.TotalReceived, ct);

        var walletDtos = new List<WalletDto>();
        var now = DateTime.UtcNow;

        foreach (var w in wallets)
        {
            var reportedBalance = await _db.ReadLatestReportedBalanceAsync(w.Id, ct);

            // Calculate Daily Received (resolved today in Egypt time)
            var dailyReceived = rechargeRequests
                .Where(r => r.WalletId == w.Id && r.ResolvedAt >= dayStartUtc && r.ResolvedAt < dayEndUtc)
                .Sum(r => r.Amount);

            // Calculate Monthly Received (resolved this month in Egypt time)
            var monthlyReceived = rechargeRequests
                .Where(r => r.WalletId == w.Id)
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

            // Foreground sync heartbeats every 30 seconds; WorkManager fallback is limited to 15 minutes.
            // Keep the admin status connected long enough to survive temporary Android background throttling.
            var status = w.DeviceStatus;
            if (status == "Connected" && w.LastSeenAt.HasValue && DateTime.UtcNow - w.LastSeenAt.Value > TimeSpan.FromMinutes(20))
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
                CurrentBalance = reportedBalance ?? w.CurrentBalance,
                PairingToken = w.PairingToken,
                DeviceStatus = status,
                LastSeenAt = w.LastSeenAt,
                IsActive = w.IsActive,
                IsRechargePaused = w.IsRechargePaused && (!w.RechargeResumeAt.HasValue || w.RechargeResumeAt > now),
                RechargePauseMessage = w.RechargePauseMessage,
                RechargeResumeAt = w.RechargeResumeAt,
                SmsSenderFilters = filters,
                DailyReceived = dailyReceived,
                MonthlyReceived = monthlyReceived,
                TotalReceived = totalReceivedByWallet.GetValueOrDefault(w.Id),
                CreatedAt = w.CreatedAt
            });
        }

        return ApiResponse<List<WalletDto>>.Ok(walletDtos);
    }
}
