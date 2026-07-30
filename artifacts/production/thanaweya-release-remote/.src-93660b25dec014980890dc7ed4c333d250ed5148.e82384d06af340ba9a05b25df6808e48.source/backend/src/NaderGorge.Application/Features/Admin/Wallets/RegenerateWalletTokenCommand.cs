using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Wallets;

public record RegenerateWalletTokenCommand(Guid WalletId) : IRequest<ApiResponse<string>>;

public class RegenerateWalletTokenCommandHandler : IRequestHandler<RegenerateWalletTokenCommand, ApiResponse<string>>
{
    private readonly IAppDbContext _db;

    public RegenerateWalletTokenCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<string>> Handle(RegenerateWalletTokenCommand request, CancellationToken ct)
    {
        var wallet = await _db.DigitalWallets.FirstOrDefaultAsync(w => w.Id == request.WalletId, ct);
        if (wallet == null)
            return ApiResponse<string>.Fail("المحفظة غير موجودة");

        // Generate unique 8-character pairing code
        string pairingToken;
        bool tokenExists;
        do
        {
            pairingToken = GeneratePairingToken();
            tokenExists = await _db.DigitalWallets.AnyAsync(w => w.PairingToken == pairingToken, ct);
        } while (tokenExists);

        wallet.PairingToken = pairingToken;
        wallet.DeviceStatus = "Disconnected";
        wallet.LastSeenAt = null;
        wallet.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ApiResponse<string>.Ok(pairingToken, "تم إعادة توليد كود الربط بنجاح؛ سيتم إلغاء اتصال الهاتف الحالي حتى يتم ربطه مجدداً");
    }

    private static string GeneratePairingToken()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var result = new char[8];
        for (int i = 0; i < 8; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        return new string(result);
    }
}
