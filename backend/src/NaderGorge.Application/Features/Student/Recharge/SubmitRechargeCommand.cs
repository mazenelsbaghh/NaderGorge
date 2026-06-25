using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Recharge;

public record SubmitRechargeCommand(
    Guid RechargeRequestId,
    string SenderPhoneNumber,
    byte[] ScreenshotBytes) : IRequest<ApiResponse<SubmitRechargeDto>>;

public class SubmitRechargeDto
{
    public bool IsMatched { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ReviewCode { get; set; } = string.Empty;
}

public class SubmitRechargeCommandHandler : IRequestHandler<SubmitRechargeCommand, ApiResponse<SubmitRechargeDto>>
{
    private readonly IAppDbContext _db;
    private readonly IContentImageStorage _imageStorage;
    private readonly BalanceService _balanceService;

    public SubmitRechargeCommandHandler(IAppDbContext db, IContentImageStorage imageStorage, BalanceService balanceService)
    {
        _db = db;
        _imageStorage = imageStorage;
        _balanceService = balanceService;
    }

    public async Task<ApiResponse<SubmitRechargeDto>> Handle(SubmitRechargeCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SenderPhoneNumber))
            return ApiResponse<SubmitRechargeDto>.Fail("رقم الهاتف المرسل مطلوب");

        if (request.ScreenshotBytes == null || request.ScreenshotBytes.Length == 0)
            return ApiResponse<SubmitRechargeDto>.Fail("صورة إثبات التحويل مطلوبة");

        var rechargeRequest = await _db.RechargeRequests
            .Include(r => r.Wallet)
            .FirstOrDefaultAsync(r => r.Id == request.RechargeRequestId, ct);

        if (rechargeRequest == null)
            return ApiResponse<SubmitRechargeDto>.Fail("طلب الشحن هذا غير موجود");

        if (rechargeRequest.Status != RechargeRequestStatus.Pending)
            return ApiResponse<SubmitRechargeDto>.Fail("تم معالجة هذا الطلب بالفعل مسبقاً");

        if (rechargeRequest.ReservationExpiresAt.HasValue && rechargeRequest.ReservationExpiresAt.Value < DateTime.UtcNow)
        {
            return ApiResponse<SubmitRechargeDto>.Fail("انتهت صلاحية حجز المعاملة (20 دقيقة)، يرجى البدء بطلب جديد.");
        }

        // Save screenshot as Webp
        string screenshotUrl;
        try
        {
            using var stream = new MemoryStream(request.ScreenshotBytes);
            screenshotUrl = await _imageStorage.SaveAsWebpAsync(stream, "recharges", ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return ApiResponse<SubmitRechargeDto>.Fail("فشل في حفظ الصورة. يرجى التأكد من أنها صورة صالحة.");
        }

        // Update request details
        rechargeRequest.SenderPhoneNumber = request.SenderPhoneNumber.Trim();
        rechargeRequest.ScreenshotUrl = screenshotUrl;
        rechargeRequest.ReservationExpiresAt = null; // Clear expiration since it is now submitted

        // 5. Try to find a matching, unmatched SMS that was already received
        var startTime = rechargeRequest.CreatedAt.AddHours(-2);
        var endTime = rechargeRequest.CreatedAt.AddHours(2);

        var matchedSms = await _db.IncomingSmsLogs
            .FirstOrDefaultAsync(l => 
                l.WalletId == rechargeRequest.WalletId &&
                l.ParsedAmount == rechargeRequest.Amount &&
                l.ParsedSenderPhone == rechargeRequest.SenderPhoneNumber &&
                !l.IsMatched &&
                l.ReceivedAt >= startTime &&
                l.ReceivedAt <= endTime, ct);

        bool isMatched = false;
        string message;

        if (matchedSms != null)
        {
            var hasActiveTransaction = _db is DbContext efDb && efDb.Database.CurrentTransaction != null;
            var transaction = hasActiveTransaction ? null : await _db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            
            try
            {
                // Update request
                rechargeRequest.Status = RechargeRequestStatus.Matched;
                rechargeRequest.ResolvedAt = DateTime.UtcNow;
                rechargeRequest.MatchedSmsLogId = matchedSms.Id;

                // Update SMS log
                matchedSms.IsMatched = true;
                matchedSms.MatchedRechargeRequestId = rechargeRequest.Id;

                // Update Wallet balance
                rechargeRequest.Wallet.CurrentBalance += rechargeRequest.Amount;

                await _db.SaveChangesAsync(ct);

                // Credit student balance
                await _balanceService.AddCredit(
                    rechargeRequest.UserId,
                    rechargeRequest.Amount,
                    $"شحن رصيد تلقائي - محفظة {rechargeRequest.Wallet.Label}",
                    rechargeRequest.Id,
                    "DigitalRecharge",
                    ct);

                if (transaction != null)
                {
                    await transaction.CommitAsync(ct);
                }

                isMatched = true;
                message = "تم مطابقة الدفع وتفعيل الرصيد تلقائياً بنجاح! تم إضافة المبلغ لحسابك.";
            }
            catch (Exception)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(ct);
                }
                throw;
            }
        }
        else
        {
            await _db.SaveChangesAsync(ct);
            message = "تم تسجيل طلب الشحن وإثبات الدفع بنجاح. سيقوم النظام بمطابقتها تلقائياً عند وصول الرسالة، أو مراجعتها يدوياً من قبل الإدارة.";
        }

        var dto = new SubmitRechargeDto
        {
            IsMatched = isMatched,
            Message = message,
            ReviewCode = rechargeRequest.Id.ToString("N")[..8].ToUpperInvariant()
        };

        return ApiResponse<SubmitRechargeDto>.Ok(dto, "تم إرسال الطلب بنجاح");
    }
}
