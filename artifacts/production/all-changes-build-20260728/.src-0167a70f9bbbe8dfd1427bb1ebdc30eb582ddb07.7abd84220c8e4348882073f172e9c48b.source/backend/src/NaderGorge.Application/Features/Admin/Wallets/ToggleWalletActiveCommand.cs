using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Wallets;

public record ToggleWalletActiveCommand(Guid WalletId, bool IsActive) : IRequest<ApiResponse>;

public class ToggleWalletActiveCommandHandler : IRequestHandler<ToggleWalletActiveCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public ToggleWalletActiveCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(ToggleWalletActiveCommand request, CancellationToken ct)
    {
        var wallet = await _db.DigitalWallets.FirstOrDefaultAsync(w => w.Id == request.WalletId, ct);
        if (wallet == null)
            return ApiResponse.Fail("المحفظة غير موجودة");

        wallet.IsActive = request.IsActive;
        wallet.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        string statusText = request.IsActive ? "تنشيط" : "إلغاء تنشيط";
        return ApiResponse.Ok($"تم {statusText} المحفظة بنجاح");
    }
}
