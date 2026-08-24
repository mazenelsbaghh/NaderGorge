using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Wallets;

public sealed record WalletSettingsUpdate
{
    public required string Label { get; init; }
    public required decimal DailyLimit { get; init; }
    public required decimal MonthlyLimit { get; init; }
    public required List<string> SmsSenderFilters { get; init; }
    public bool? IsRechargePaused { get; init; }
    public string? RechargePauseMessage { get; init; }
    public DateTime? RechargeResumeAt { get; init; }
}

public record UpdateWalletLimitsCommand(Guid WalletId, WalletSettingsUpdate Settings) : IRequest<ApiResponse>;

public class UpdateWalletLimitsCommandHandler : IRequestHandler<UpdateWalletLimitsCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateWalletLimitsCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdateWalletLimitsCommand request, CancellationToken ct)
    {
        var wallet = await _db.DigitalWallets.FirstOrDefaultAsync(w => w.Id == request.WalletId, ct);
        if (wallet == null)
            return ApiResponse.Fail("المحفظة غير موجودة");

        var settings = request.Settings;
        if (string.IsNullOrWhiteSpace(settings.Label))
            return ApiResponse.Fail("الاسم التعريفي للمحفظة مطلوب");

        if (settings.DailyLimit <= 0)
            return ApiResponse.Fail("الحد اليومي يجب أن يكون أكبر من صفر");

        if (settings.MonthlyLimit <= 0)
            return ApiResponse.Fail("الحد الشهري يجب أن يكون أكبر من صفر");

        if (settings.MonthlyLimit < settings.DailyLimit)
            return ApiResponse.Fail("الحد الشهري لا يمكن أن يكون أقل من الحد اليومي");

        var pauseMessage = settings.RechargePauseMessage?.Trim() ?? string.Empty;
        if (settings.IsRechargePaused == true && string.IsNullOrWhiteSpace(pauseMessage))
            return ApiResponse.Fail("اكتب رسالة واضحة تظهر للطالب أثناء إيقاف التحويل");

        if (pauseMessage.Length > 500)
            return ApiResponse.Fail("رسالة إيقاف التحويل يجب ألا تتجاوز 500 حرف");

        wallet.Label = settings.Label;
        wallet.DailyLimit = settings.DailyLimit;
        wallet.MonthlyLimit = settings.MonthlyLimit;
        if (settings.IsRechargePaused.HasValue)
        {
            wallet.IsRechargePaused = settings.IsRechargePaused.Value;
            wallet.RechargePauseMessage = settings.IsRechargePaused.Value ? pauseMessage : string.Empty;
            wallet.RechargeResumeAt = settings.IsRechargePaused.Value ? settings.RechargeResumeAt : null;
        }

        if (settings.SmsSenderFilters.Any())
        {
            var filters = settings.SmsSenderFilters
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            wallet.SmsSenderFilters = JsonSerializer.Serialize(filters);
        }

        wallet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok("تم تحديث إعدادات وحدود المحفظة بنجاح");
    }
}
