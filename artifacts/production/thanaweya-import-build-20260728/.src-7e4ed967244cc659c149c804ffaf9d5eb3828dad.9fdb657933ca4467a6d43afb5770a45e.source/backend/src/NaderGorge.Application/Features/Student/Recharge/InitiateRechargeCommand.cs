using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Recharge;

public record InitiateRechargeCommand(
    Guid UserId,
    decimal Amount,
    Guid? TeacherId = null) : IRequest<ApiResponse<InitiateRechargeDto>>;

public class InitiateRechargeDto
{
    public Guid RechargeRequestId { get; set; }
    public string ReviewCode { get; set; } = string.Empty;
    public string WalletPhoneNumber { get; set; } = string.Empty;
    public string WalletLabel { get; set; } = string.Empty;
    public DateTime ExpirationTime { get; set; }
}

public class InitiateRechargeCommandHandler : IRequestHandler<InitiateRechargeCommand, ApiResponse<InitiateRechargeDto>>
{
    private readonly IAppDbContext _db;

    public InitiateRechargeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<InitiateRechargeDto>> Handle(InitiateRechargeCommand request, CancellationToken ct)
    {
        await RechargeRequestExpiryService.RejectPendingOlderThan24Hours(_db, ct);

        if (request.Amount <= 0)
            return ApiResponse<InitiateRechargeDto>.Fail("قيمة الشحن يجب أن تكون أكبر من صفر");

        if (request.TeacherId.HasValue)
        {
            var teacherExists = await _db.TeacherProfiles.AnyAsync(
                teacher => teacher.Id == request.TeacherId.Value && teacher.User.IsActive && !teacher.User.IsDeleted && teacher.IsVisibleToStudents,
                ct);
            if (!teacherExists)
                return ApiResponse<InitiateRechargeDto>.Fail("المدرس المختار غير متاح للشحن حالياً.");
        }

        // Fetch active wallets
        var activeWallets = await _db.DigitalWallets
            .Where(w => w.IsActive)
            .ToListAsync(ct);

        if (!activeWallets.Any())
            return ApiResponse<InitiateRechargeDto>.Fail("عذراً، لا توجد محافظ شحن نشطة حالياً. يرجى المحاولة لاحقاً.");

        // Calculate capacities and choose best wallet
        var (dayStartUtc, dayEndUtc) = CairoTime.GetCurrentDayRangeUtc();
        var (monthStartUtc, monthEndUtc) = CairoTime.GetCurrentMonthRangeUtc();
        
        var activeStatus = new[] { RechargeRequestStatus.Matched, RechargeRequestStatus.Approved };

        DigitalWallet? selectedWallet = null;
        decimal maxRemainingCapacity = -1m;

        foreach (var wallet in activeWallets)
        {
            // Get all requests resolved or pending (not expired) in this month
            var walletRequests = await _db.RechargeRequests
                .Where(r => r.WalletId == wallet.Id && 
                    (activeStatus.Contains(r.Status) || (r.Status == RechargeRequestStatus.Pending && r.ReservationExpiresAt > DateTime.UtcNow)) &&
                    r.CreatedAt >= monthStartUtc && r.CreatedAt < monthEndUtc)
                .ToListAsync(ct);

            // Daily Received/Reserved today (Egypt Local Time)
            var dailyUsed = walletRequests
                .Where(r => r.CreatedAt >= dayStartUtc && r.CreatedAt < dayEndUtc)
                .Sum(r => r.Amount);

            // Monthly Received/Reserved this month (Egypt Local Time)
            var monthlyUsed = walletRequests
                .Sum(r => r.Amount);

            var remainingDaily = wallet.DailyLimit - dailyUsed;
            var remainingMonthly = wallet.MonthlyLimit - monthlyUsed;

            // Check if this wallet has capacity for this amount
            if (dailyUsed + request.Amount <= wallet.DailyLimit && monthlyUsed + request.Amount <= wallet.MonthlyLimit)
            {
                if (remainingDaily > maxRemainingCapacity)
                {
                    maxRemainingCapacity = remainingDaily;
                    selectedWallet = wallet;
                }
            }
        }

        if (selectedWallet == null)
            return ApiResponse<InitiateRechargeDto>.Fail("عذراً، تم الوصول للحد الأقصى لجميع محافظ الاستقبال اليوم. يرجى المحاولة لاحقاً.");

        var expiration = DateTime.UtcNow.AddHours(1);

        var rechargeRequest = new RechargeRequest
        {
            UserId = request.UserId,
            WalletId = selectedWallet.Id,
            Amount = request.Amount,
            TeacherId = request.TeacherId,
            Status = RechargeRequestStatus.Pending,
            ReservationExpiresAt = expiration
        };

        _db.RechargeRequests.Add(rechargeRequest);
        await _db.SaveChangesAsync(ct);

        var dto = new InitiateRechargeDto
        {
            RechargeRequestId = rechargeRequest.Id,
            ReviewCode = rechargeRequest.Id.ToString("N")[..8].ToUpperInvariant(),
            WalletPhoneNumber = selectedWallet.PhoneNumber,
            WalletLabel = selectedWallet.Label,
            ExpirationTime = expiration
        };

        return ApiResponse<InitiateRechargeDto>.Ok(dto, "تم حجز المحفظة بنجاح، يرجى إتمام التحويل ورفع الإثبات خلال ساعة واحدة.");
    }
}
