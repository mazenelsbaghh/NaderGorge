using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Android;

public record AndroidUploadSmsCommand(
    string PairingToken,
    string Sender,
    string Body,
    DateTime ReceivedAt) : IRequest<ApiResponse<AndroidSmsUploadDto>>;

public class AndroidSmsUploadDto
{
    public bool IsMatched { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AndroidUploadSmsCommandHandler : IRequestHandler<AndroidUploadSmsCommand, ApiResponse<AndroidSmsUploadDto>>
{
    private readonly IAppDbContext _db;
    private readonly BalanceService _balanceService;

    public AndroidUploadSmsCommandHandler(IAppDbContext db, BalanceService balanceService)
    {
        _db = db;
        _balanceService = balanceService;
    }

    public async Task<ApiResponse<AndroidSmsUploadDto>> Handle(AndroidUploadSmsCommand request, CancellationToken ct)
    {
        await RechargeRequestExpiryService.RejectPendingOlderThan24Hours(_db, ct);

        if (string.IsNullOrWhiteSpace(request.PairingToken))
            return ApiResponse<AndroidSmsUploadDto>.Fail("pairing token invalid");

        var wallet = await _db.DigitalWallets
            .FirstOrDefaultAsync(w => w.PairingToken == request.PairingToken, ct);

        if (wallet == null)
            return ApiResponse<AndroidSmsUploadDto>.Fail("pairing token invalid");

        if (!wallet.IsActive)
            return ApiResponse<AndroidSmsUploadDto>.Fail("هذه المحفظة غير نشطة حالياً");

        // 1. Calculate minute-precision deduplication hash
        var normalizedReceivedAt = request.ReceivedAt.ToString("yyyy-MM-dd HH:mm");
        var hashInput = $"{wallet.Id}:{request.Sender}:{request.Body}:{normalizedReceivedAt}";
        var deduplicationHash = CalculateSha256(hashInput);

        // 2. Check if already processed
        var existingLog = await _db.IncomingSmsLogs
            .FirstOrDefaultAsync(l => l.DeduplicationHash == deduplicationHash, ct);

        if (existingLog != null)
        {
            return ApiResponse<AndroidSmsUploadDto>.Ok(new AndroidSmsUploadDto
            {
                IsMatched = existingLog.IsMatched,
                Message = "تم معالجة هذه الرسالة مسبقاً"
            }, "تم معالجة هذه الرسالة مسبقاً");
        }

        // 3. Parse SMS body
        var parserResult = SmsParser.Parse(request.Body);

        var smsLog = new IncomingSmsLog
        {
            WalletId = wallet.Id,
            Sender = request.Sender,
            Body = request.Body,
            ReceivedAt = request.ReceivedAt,
            DeduplicationHash = deduplicationHash,
            ParsedAmount = parserResult.Amount,
            ParsedSenderPhone = parserResult.SenderPhone,
            IsMatched = false
        };

        bool isMatched = false;
        RechargeRequest? matchedRequest = null;

        // 4. Try matching with pending requests if successfully parsed
        if (parserResult.IsParsedSuccessfully)
        {
            var amount = parserResult.Amount!.Value;
            var senderPhone = parserResult.SenderPhone!;

            // Search window: resolved within 2 hours of SMS receipt
            var startTime = request.ReceivedAt.AddHours(-2);
            var endTime = request.ReceivedAt.AddHours(2);

            matchedRequest = await _db.RechargeRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => 
                    r.WalletId == wallet.Id &&
                    r.Amount == amount &&
                    r.SenderPhoneNumber == senderPhone &&
                    r.Status == RechargeRequestStatus.Pending &&
                    r.CreatedAt >= startTime &&
                    r.CreatedAt <= endTime, ct);

            if (matchedRequest != null)
            {
                // Run in transaction to ensure atomicity
                var hasActiveTransaction = _db is DbContext efDb && efDb.Database.CurrentTransaction != null;
                var transaction = hasActiveTransaction ? null : await _db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
                
                try
                {
                    // Update request status
                    matchedRequest.Status = RechargeRequestStatus.Matched;
                    matchedRequest.ResolvedAt = DateTime.UtcNow;

                    // Update SMS log
                    smsLog.IsMatched = true;
                    smsLog.MatchedRechargeRequestId = matchedRequest.Id;
                    matchedRequest.MatchedSmsLogId = smsLog.Id;
                    matchedRequest.MatchedSmsLog = smsLog;

                    // Vodafone Cash messages include the authoritative wallet balance after the transfer.
                    wallet.CurrentBalance = parserResult.CurrentBalance ?? wallet.CurrentBalance + amount;

                    // Save matching state first so entities exist/have IDs
                    _db.IncomingSmsLogs.Add(smsLog);
                    await _db.SaveChangesAsync(ct);

                    // Credit student balance
                    await _balanceService.AddCredit(
                        matchedRequest.UserId,
                        amount,
                        $"شحن رصيد تلقائي - محفظة {wallet.Label}",
                        matchedRequest.Id,
                        "DigitalRecharge",
                        ct);

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(ct);
                    }

                    isMatched = true;
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
        }

        // If not matched (or not parsed successfully), just log it
        if (!isMatched)
        {
            if (parserResult.CurrentBalance.HasValue)
            {
                wallet.CurrentBalance = parserResult.CurrentBalance.Value;
            }

            _db.IncomingSmsLogs.Add(smsLog);
            await _db.SaveChangesAsync(ct);
        }

        return ApiResponse<AndroidSmsUploadDto>.Ok(new AndroidSmsUploadDto
        {
            IsMatched = isMatched,
            Message = isMatched 
                ? $"تم مطابقة الدفع وتفعيل رصيد بقيمة {parserResult.Amount} ج.م للطالب بنجاح." 
                : "تم استلام الرسالة وتسجيلها في النظام، ولكن لم يتم مطابقتها مع أي طلب شحن معلق."
        }, "تمت المعالجة بنجاح");
    }

    private static string CalculateSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder();
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

}
