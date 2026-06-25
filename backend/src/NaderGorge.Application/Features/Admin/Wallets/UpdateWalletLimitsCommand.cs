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

public record UpdateWalletLimitsCommand(
    Guid WalletId,
    string Label,
    decimal DailyLimit,
    decimal MonthlyLimit,
    List<string> SmsSenderFilters) : IRequest<ApiResponse>;

public class UpdateWalletLimitsCommandHandler : IRequestHandler<UpdateWalletLimitsCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateWalletLimitsCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdateWalletLimitsCommand request, CancellationToken ct)
    {
        var wallet = await _db.DigitalWallets.FirstOrDefaultAsync(w => w.Id == request.WalletId, ct);
        if (wallet == null)
            return ApiResponse.Fail("المحفظة غير موجودة");

        if (string.IsNullOrWhiteSpace(request.Label))
            return ApiResponse.Fail("الاسم التعريفي للمحفظة مطلوب");

        if (request.DailyLimit <= 0)
            return ApiResponse.Fail("الحد اليومي يجب أن يكون أكبر من صفر");

        if (request.MonthlyLimit <= 0)
            return ApiResponse.Fail("الحد الشهري يجب أن يكون أكبر من صفر");

        if (request.MonthlyLimit < request.DailyLimit)
            return ApiResponse.Fail("الحد الشهري لا يمكن أن يكون أقل من الحد اليومي");

        wallet.Label = request.Label;
        wallet.DailyLimit = request.DailyLimit;
        wallet.MonthlyLimit = request.MonthlyLimit;
        
        if (request.SmsSenderFilters != null && request.SmsSenderFilters.Any())
        {
            wallet.SmsSenderFilters = JsonSerializer.Serialize(request.SmsSenderFilters);
        }

        wallet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok("تم تحديث إعدادات وحدود المحفظة بنجاح");
    }
}
