using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    Guid UserId,
    Guid RechargeRequestId,
    string SenderPhoneNumber,
    byte[] ScreenshotBytes,
    string ScreenshotFileName,
    string? ScreenshotContentType,
    bool ConfirmSenderPhone = false) : IRequest<ApiResponse<SubmitRechargeDto>>;

public class SubmitRechargeDto
{
    public bool IsMatched { get; set; }
    public bool RequiresSenderPhoneConfirmation { get; set; }
    public string OriginalSenderPhoneNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ReviewCode { get; set; } = string.Empty;
}

public class SubmitRechargeCommandHandler : IRequestHandler<SubmitRechargeCommand, ApiResponse<SubmitRechargeDto>>
{
    private static readonly Regex EgyptianMobileRegex = new(@"^01[0125]\d{8}$", RegexOptions.Compiled);

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
        await RechargeRequestExpiryService.ResolveExpiredPendingRequests(_db, ct);

        var senderPhoneNumber = NormalizePhone(request.SenderPhoneNumber);

        if (string.IsNullOrWhiteSpace(senderPhoneNumber))
            return ApiResponse<SubmitRechargeDto>.Fail("رقم الهاتف المرسل مطلوب");

        if (!EgyptianMobileRegex.IsMatch(senderPhoneNumber))
            return ApiResponse<SubmitRechargeDto>.Fail("رقم الهاتف المحول منه يجب أن يكون 11 رقم ويبدأ بـ 010 أو 011 أو 012 أو 015.");

        var hasNewScreenshot = request.ScreenshotBytes is { Length: > 0 };
        if (hasNewScreenshot)
        {
            try
            {
                UploadFileSafety.Validate(
                    request.ScreenshotBytes,
                    request.ScreenshotFileName,
                    request.ScreenshotContentType,
                    SafeUploadKind.PublicImage);
            }
            catch (InvalidUploadContentException)
            {
                return ApiResponse<SubmitRechargeDto>.Fail("صورة إثبات التحويل يجب أن تكون صورة JPG أو PNG أو WEBP صالحة.");
            }
        }

        var rechargeRequest = await _db.RechargeRequests
            .Include(r => r.Wallet)
            .FirstOrDefaultAsync(r => r.Id == request.RechargeRequestId && r.UserId == request.UserId, ct);

        if (rechargeRequest == null)
            return ApiResponse<SubmitRechargeDto>.Fail("طلب الشحن هذا غير موجود");

        if (rechargeRequest.Status != RechargeRequestStatus.Pending)
            return ExistingRequestResponse(rechargeRequest);

        if (!rechargeRequest.TeacherId.HasValue)
            return ApiResponse<SubmitRechargeDto>.Fail("لا يمكن استكمال طلب شحن عام. ألغِ الطلب وأنشئ طلباً جديداً لرصيد مدرس.");

        if (!hasNewScreenshot && string.IsNullOrWhiteSpace(rechargeRequest.ScreenshotUrl))
            return ApiResponse<SubmitRechargeDto>.Fail("صورة إثبات التحويل مطلوبة");

        if (rechargeRequest.ReservationExpiresAt.HasValue && rechargeRequest.ReservationExpiresAt.Value < DateTime.UtcNow)
        {
            return ApiResponse<SubmitRechargeDto>.Fail("انتهت صلاحية حجز المعاملة (ساعة واحدة)، يرجى البدء بطلب جديد.");
        }

        if (hasNewScreenshot)
        {
            try
            {
                using var stream = new MemoryStream(request.ScreenshotBytes);
                rechargeRequest.ScreenshotUrl = await _imageStorage.SaveAsWebpAsync(stream, "recharges", ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return ApiResponse<SubmitRechargeDto>.Fail("فشل في حفظ الصورة. يرجى التأكد من أنها صورة صالحة.");
            }
        }

        rechargeRequest.OriginalSenderPhoneNumber ??= senderPhoneNumber;
        var senderPhoneChanged = !string.Equals(
            rechargeRequest.SenderPhoneNumber,
            senderPhoneNumber,
            StringComparison.Ordinal);
        rechargeRequest.SenderPhoneNumber = senderPhoneNumber;
        if (request.ConfirmSenderPhone)
            rechargeRequest.SenderPhoneConfirmedAt = DateTime.UtcNow;
        else if (senderPhoneChanged)
            rechargeRequest.SenderPhoneConfirmedAt = null;
        rechargeRequest.ReservationExpiresAt = null; // Clear expiration since it is now submitted

        // The uniqueness query runs against persisted rows, so the proof and
        // normalized sender phone must be visible before selecting a match.
        await _db.SaveChangesAsync(ct);

        // 5. Try to find a matching, unmatched SMS that was already received
        // A pending row may be reused for a later reservation. Match against the
        // latest reservation time instead of the row's original creation time.
        var matchingAnchor = RechargeMatchRules.Anchor(rechargeRequest);
        var startTime = RechargeMatchRules.WindowStart(matchingAnchor);
        var endTime = RechargeMatchRules.WindowEnd(matchingAnchor);

        var exactRows = await _db.IncomingSmsLogs
            .Include(l => l.Wallet)
            .Where(l =>
                l.ParsedAmount == rechargeRequest.Amount &&
                l.ParsedSenderPhone == rechargeRequest.SenderPhoneNumber &&
                !l.IsMatched &&
                l.ReceivedAt >= startTime &&
                l.ReceivedAt <= endTime)
            .OrderBy(l => l.ReceivedAt)
            .ToListAsync(ct);

        // Direction is still derived from the legacy SMS body. Filter it before
        // limiting to two so outgoing transfers cannot hide or become evidence.
        var exactMatches = exactRows
            .Where(log => !SmsParser.IsOutgoingTransfer(log.Body))
            .Take(2)
            .ToList();

        var matchedSms = exactMatches.Count == 1 ? exactMatches[0] : null;
        if (matchedSms is not null)
        {
            var uniqueRequest = await RechargeMatchCandidateSelector.UniquePendingRequestAsync(
                _db.RechargeRequests,
                new RechargeMatchKey(rechargeRequest.Amount, senderPhoneNumber, matchedSms.ReceivedAt),
                ct);
            if (uniqueRequest?.Id != rechargeRequest.Id)
                matchedSms = null;
        }

        bool isMatched = false;
        string message;

        if (matchedSms != null)
        {
            var hasActiveTransaction = _db is DbContext efDb && efDb.Database.CurrentTransaction != null;
            var transaction = hasActiveTransaction ? null : await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            
            try
            {
                // The transfer may have reached a wallet from an earlier reservation.
                // The SMS is the authoritative evidence of which platform wallet received the money.
                rechargeRequest.WalletId = matchedSms.WalletId;
                rechargeRequest.Wallet = matchedSms.Wallet;

                // Update request
                rechargeRequest.Status = RechargeRequestStatus.Matched;
                rechargeRequest.ResolvedAt = DateTime.UtcNow;
                rechargeRequest.MatchedSmsLogId = matchedSms.Id;
                rechargeRequest.RequiresSenderPhoneConfirmation = false;

                // Update SMS log
                matchedSms.IsMatched = true;
                matchedSms.MatchedRechargeRequestId = rechargeRequest.Id;

                await _db.ApplyIfLatestAsync(rechargeRequest.Wallet, matchedSms, rechargeRequest.Amount, ct);

                await _db.SaveChangesAsync(ct);

                await CreditRechargeAsync(rechargeRequest, rechargeRequest.UserId, "تلقائي", ct);

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
            var nearbyRows = await _db.IncomingSmsLogs
                .AsNoTracking()
                .Where(l =>
                    l.ParsedAmount == rechargeRequest.Amount &&
                    l.ParsedSenderPhone != null &&
                    !l.IsMatched &&
                    l.ReceivedAt >= startTime &&
                    l.ReceivedAt <= endTime)
                .Select(l => new { l.Body, SenderPhoneNumber = l.ParsedSenderPhone! })
                .ToListAsync(ct);

            rechargeRequest.RequiresSenderPhoneConfirmation =
                !request.ConfirmSenderPhone
                && !rechargeRequest.SenderPhoneConfirmedAt.HasValue
                && nearbyRows.Any(row => !SmsParser.IsOutgoingTransfer(row.Body)
                    && RechargePhoneSimilarity.RequiresConfirmation(senderPhoneNumber, row.SenderPhoneNumber));
            await _db.SaveChangesAsync(ct);
            message = rechargeRequest.RequiresSenderPhoneConfirmation
                ? $"وجدنا تحويلًا قريبًا من الرقم الذي كتبته ({rechargeRequest.OriginalSenderPhoneNumber}). راجع رقم المحفظة المحول منها واكتبه مرة أخرى، أو أكد أن الرقم المكتوب صحيح."
                : "تم تسجيل طلب الشحن وإثبات الدفع بنجاح. سيقوم النظام بمطابقتها تلقائياً عند وصول الرسالة، أو مراجعتها يدوياً من قبل الإدارة.";
        }

        var dto = new SubmitRechargeDto
        {
            IsMatched = isMatched,
            RequiresSenderPhoneConfirmation = rechargeRequest.RequiresSenderPhoneConfirmation,
            OriginalSenderPhoneNumber = rechargeRequest.OriginalSenderPhoneNumber ?? senderPhoneNumber,
            Message = message,
            ReviewCode = rechargeRequest.Id.ToString("N")[..8].ToUpperInvariant()
        };

        return ApiResponse<SubmitRechargeDto>.Ok(dto, "تم إرسال الطلب بنجاح");
    }

    private static string NormalizePhone(string phone) =>
        new((phone ?? string.Empty).Where(char.IsDigit).ToArray());

    private static ApiResponse<SubmitRechargeDto> ExistingRequestResponse(RechargeRequest rechargeRequest) =>
        rechargeRequest.Status switch
        {
            RechargeRequestStatus.Matched or RechargeRequestStatus.Approved => AcceptedRequestResponse(rechargeRequest),
            RechargeRequestStatus.Rejected => ApiResponse<SubmitRechargeDto>.Fail(
                rechargeRequest.RejectionReason ?? "تم رفض طلب الشحن. راجع البيانات أو أنشئ طلباً جديداً."),
            RechargeRequestStatus.Expired => ApiResponse<SubmitRechargeDto>.Fail(
                "انتهت صلاحية طلب الشحن. ابدأ طلباً جديداً."),
            RechargeRequestStatus.Cancelled => ApiResponse<SubmitRechargeDto>.Fail(
                "تم إلغاء طلب الشحن. ابدأ طلباً جديداً."),
            _ => ApiResponse<SubmitRechargeDto>.Fail("لا يمكن إرسال إثبات لهذا الطلب.")
        };

    private static ApiResponse<SubmitRechargeDto> AcceptedRequestResponse(RechargeRequest rechargeRequest)
    {
        var message = rechargeRequest.Status == RechargeRequestStatus.Matched
            ? "تمت مطابقة التحويل وإضافة الرصيد بالفعل."
            : "تمت الموافقة على طلب الشحن وإضافة الرصيد بالفعل.";

        return ApiResponse<SubmitRechargeDto>.Ok(new SubmitRechargeDto
        {
            IsMatched = true,
            RequiresSenderPhoneConfirmation = false,
            OriginalSenderPhoneNumber = rechargeRequest.OriginalSenderPhoneNumber ?? rechargeRequest.SenderPhoneNumber,
            Message = message,
            ReviewCode = rechargeRequest.Id.ToString("N")[..8].ToUpperInvariant()
        }, message);
    }

    private Task CreditRechargeAsync(RechargeRequest rechargeRequest, Guid issuedByUserId, string source, CancellationToken ct) =>
        rechargeRequest.TeacherId.HasValue
            ? _balanceService.AddTeacherCredit(rechargeRequest.UserId, rechargeRequest.TeacherId.Value, rechargeRequest.Amount,
                $"شحن رصيد للمدرس - مطابقة {source} (محفظة {rechargeRequest.Wallet.Label})", issuedByUserId, rechargeRequest.Id, ct)
            : _balanceService.AddCredit(rechargeRequest.UserId, rechargeRequest.Amount,
                $"شحن رصيد عام - مطابقة {source} (محفظة {rechargeRequest.Wallet.Label})",
                rechargeRequest.Id, "RechargeCredit", ct);
}
