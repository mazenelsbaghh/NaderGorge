using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Wallets;

public record CreateWalletCommand(
    string PhoneNumber,
    string Label,
    decimal DailyLimit,
    decimal MonthlyLimit,
    List<string> SmsSenderFilters) : IRequest<ApiResponse<WalletDto>>;

public class CreateWalletCommandHandler : IRequestHandler<CreateWalletCommand, ApiResponse<WalletDto>>
{
    private readonly IAppDbContext _db;

    public CreateWalletCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<WalletDto>> Handle(CreateWalletCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return ApiResponse<WalletDto>.Fail("رقم الهاتف مطلوب");

        if (string.IsNullOrWhiteSpace(request.Label))
            return ApiResponse<WalletDto>.Fail("الاسم التعريفي للمحفظة مطلوب");

        if (request.DailyLimit <= 0)
            return ApiResponse<WalletDto>.Fail("الحد اليومي يجب أن يكون أكبر من صفر");

        if (request.MonthlyLimit <= 0)
            return ApiResponse<WalletDto>.Fail("الحد الشهري يجب أن يكون أكبر من صفر");

        if (request.MonthlyLimit < request.DailyLimit)
            return ApiResponse<WalletDto>.Fail("الحد الشهري لا يمكن أن يكون أقل من الحد اليومي");

        // Validate unique phone number
        var exists = await _db.DigitalWallets.AnyAsync(w => w.PhoneNumber == request.PhoneNumber, ct);
        if (exists)
            return ApiResponse<WalletDto>.Fail("رقم الهاتف هذا مسجل بالفعل لمحفظة أخرى");

        // Generate unique 8-character pairing code
        string pairingToken;
        bool tokenExists;
        do
        {
            pairingToken = GeneratePairingToken();
            tokenExists = await _db.DigitalWallets.AnyAsync(w => w.PairingToken == pairingToken, ct);
        } while (tokenExists);

        var filters = request.SmsSenderFilters != null && request.SmsSenderFilters.Any()
            ? request.SmsSenderFilters.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string> { "VF-Cash", "VodafoneCash" };

        var wallet = new DigitalWallet
        {
            PhoneNumber = request.PhoneNumber,
            Label = request.Label,
            DailyLimit = request.DailyLimit,
            MonthlyLimit = request.MonthlyLimit,
            CurrentBalance = 0m,
            PairingToken = pairingToken,
            DeviceStatus = "Disconnected",
            IsActive = true,
            SmsSenderFilters = JsonSerializer.Serialize(filters)
        };

        _db.DigitalWallets.Add(wallet);
        await _db.SaveChangesAsync(ct);

        var dto = new WalletDto
        {
            Id = wallet.Id,
            PhoneNumber = wallet.PhoneNumber,
            Label = wallet.Label,
            DailyLimit = wallet.DailyLimit,
            MonthlyLimit = wallet.MonthlyLimit,
            CurrentBalance = wallet.CurrentBalance,
            PairingToken = wallet.PairingToken,
            DeviceStatus = wallet.DeviceStatus,
            LastSeenAt = wallet.LastSeenAt,
            IsActive = wallet.IsActive,
            SmsSenderFilters = filters,
            DailyReceived = 0m,
            MonthlyReceived = 0m,
            CreatedAt = wallet.CreatedAt
        };

        return ApiResponse<WalletDto>.Ok(dto, "تم إنشاء المحفظة بنجاح وتوليد كود الربط");
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
