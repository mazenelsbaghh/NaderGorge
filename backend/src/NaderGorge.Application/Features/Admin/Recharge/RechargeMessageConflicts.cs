using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Recharge;

public sealed record RechargeSmsSuggestionDto(
    Guid SmsLogId,
    Guid WalletId,
    string WalletLabel,
    string WalletPhoneNumber,
    decimal? Amount,
    string SenderPhoneNumber,
    string? TransferReference,
    DateTime ReceivedAt,
    bool IsMatched,
    Guid? MatchedRechargeRequestId,
    string? MatchedStudentName,
    string? MatchedStudentPhoneNumber,
    int MatchScore,
    IReadOnlyList<string> MatchReasons);

public sealed record RechargeMessageConflictDto(
    Guid RechargeRequestId,
    string StudentName,
    string StudentPhoneNumber,
    decimal Amount,
    string SenderPhoneNumber,
    string WalletLabel,
    DateTime CreatedAt,
    string ConflictType,
    string ConflictDescription,
    IReadOnlyList<RechargeSmsSuggestionDto> Candidates);

public sealed record GetRechargeSmsSuggestionsQuery(Guid RechargeRequestId, string? Search = null)
    : IRequest<ApiResponse<IReadOnlyList<RechargeSmsSuggestionDto>>>;

public sealed class GetRechargeSmsSuggestionsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetRechargeSmsSuggestionsQuery, ApiResponse<IReadOnlyList<RechargeSmsSuggestionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<RechargeSmsSuggestionDto>>> Handle(
        GetRechargeSmsSuggestionsQuery request,
        CancellationToken ct)
    {
        var recharge = await db.RechargeRequests.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == request.RechargeRequestId, ct);
        if (recharge is null)
            return ApiResponse<IReadOnlyList<RechargeSmsSuggestionDto>>.Fail("طلب الشحن غير موجود.");

        var search = request.Search?.Trim();
        var from = recharge.CreatedAt.AddDays(-2);
        var to = DateTime.UtcNow.AddDays(1);
        var query = db.IncomingSmsLogs.AsNoTracking()
            .Where(log => log.ReceivedAt >= from && log.ReceivedAt <= to);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(log => log.Body.Contains(search)
                || (log.ParsedSenderPhone != null && log.ParsedSenderPhone.Contains(search))
                || (log.TransferReference != null && log.TransferReference.Contains(search)));

        var logs = await query.OrderByDescending(log => log.ReceivedAt).Take(300)
            .Select(log => new
            {
                Log = log,
                log.Wallet.Label,
                WalletPhone = log.Wallet.PhoneNumber,
                StudentName = log.MatchedRechargeRequest != null ? log.MatchedRechargeRequest.User.FullName : null,
                StudentPhone = log.MatchedRechargeRequest != null ? log.MatchedRechargeRequest.User.PhoneNumber : null
            })
            .ToListAsync(ct);

        var suggestions = logs.Select(row => Suggestion(recharge, row.Log, row.Label, row.WalletPhone, row.StudentName, row.StudentPhone))
            .Where(item => item.MatchScore > 0 || !string.IsNullOrWhiteSpace(search))
            .OrderByDescending(item => item.MatchScore)
            .ThenByDescending(item => item.ReceivedAt)
            .Take(100)
            .ToArray();
        return ApiResponse<IReadOnlyList<RechargeSmsSuggestionDto>>.Ok(suggestions);
    }

    internal static RechargeSmsSuggestionDto Suggestion(
        RechargeRequest recharge,
        IncomingSmsLog sms,
        string walletLabel,
        string walletPhone,
        string? studentName,
        string? studentPhone)
    {
        var reasons = new List<string>();
        var score = 0;
        if (sms.ParsedAmount == recharge.Amount) { score += 35; reasons.Add("نفس المبلغ"); }
        if (Digits(sms.ParsedSenderPhone) == Digits(recharge.SenderPhoneNumber)) { score += 40; reasons.Add("نفس رقم المحول"); }
        if (sms.WalletId == recharge.WalletId) { score += 15; reasons.Add("نفس المحفظة"); }
        if (Math.Abs((sms.ReceivedAt - recharge.CreatedAt).TotalHours) <= 6) { score += 10; reasons.Add("توقيت قريب"); }
        return new RechargeSmsSuggestionDto(
            sms.Id, sms.WalletId, walletLabel, walletPhone, sms.ParsedAmount,
            sms.ParsedSenderPhone ?? string.Empty, sms.TransferReference, sms.ReceivedAt,
            sms.IsMatched, sms.MatchedRechargeRequestId, studentName, studentPhone, score, reasons);
    }

    private static string Digits(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}

public sealed record GetRechargeMessageConflictsQuery : IRequest<ApiResponse<IReadOnlyList<RechargeMessageConflictDto>>>;

public sealed class GetRechargeMessageConflictsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetRechargeMessageConflictsQuery, ApiResponse<IReadOnlyList<RechargeMessageConflictDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<RechargeMessageConflictDto>>> Handle(
        GetRechargeMessageConflictsQuery request,
        CancellationToken ct)
    {
        var pending = await db.RechargeRequests.AsNoTracking()
            .Where(row => row.Status == RechargeRequestStatus.Pending && row.SenderPhoneNumber != string.Empty)
            .OrderByDescending(row => row.CreatedAt).Take(500)
            .Select(row => new { Request = row, row.User.FullName, StudentPhone = row.User.PhoneNumber, WalletLabel = row.Wallet.Label })
            .ToListAsync(ct);
        if (pending.Count == 0)
            return ApiResponse<IReadOnlyList<RechargeMessageConflictDto>>.Ok([]);

        var firstRelevantDate = pending.Min(row => row.Request.CreatedAt).AddDays(-2);
        var logs = await db.IncomingSmsLogs.AsNoTracking()
            .Where(log => log.ReceivedAt >= firstRelevantDate)
            .OrderByDescending(log => log.ReceivedAt)
            .Take(2000)
            .Select(log => new
            {
                Log = log,
                log.Wallet.Label,
                WalletPhone = log.Wallet.PhoneNumber,
                StudentName = log.MatchedRechargeRequest != null ? log.MatchedRechargeRequest.User.FullName : null,
                StudentPhone = log.MatchedRechargeRequest != null ? log.MatchedRechargeRequest.User.PhoneNumber : null
            })
            .ToListAsync(ct);
        var conflicts = new List<RechargeMessageConflictDto>();
        foreach (var row in pending)
        {
            var candidates = logs
                .Where(log => log.Log.ReceivedAt >= row.Request.CreatedAt.AddDays(-2))
                .Select(log => GetRechargeSmsSuggestionsQueryHandler.Suggestion(
                    row.Request, log.Log, log.Label, log.WalletPhone, log.StudentName, log.StudentPhone))
                .Where(candidate => candidate.MatchScore >= 75)
                .OrderByDescending(candidate => candidate.MatchScore)
                .ThenByDescending(candidate => candidate.ReceivedAt)
                .Take(10)
                .ToArray();
            var claimed = candidates.FirstOrDefault(candidate => candidate.IsMatched && candidate.MatchedRechargeRequestId != row.Request.Id);
            var wrongWallet = candidates.FirstOrDefault(candidate => !candidate.IsMatched && candidate.WalletId != row.Request.WalletId);
            if (claimed is null && wrongWallet is null)
                continue;
            var type = claimed is not null ? "ClaimedByAnotherStudent" : "ReceivedOnDifferentWallet";
            var description = claimed is not null
                ? "يوجد تحويل مطابق مرتبط بطالب آخر ويحتاج مراجعة ملكية التحويل."
                : "يوجد تحويل مطابق وصل إلى محفظة مختلفة عن المحفظة المحجوزة للطلب.";
            conflicts.Add(new RechargeMessageConflictDto(
                row.Request.Id, row.FullName, row.StudentPhone, row.Request.Amount,
                row.Request.SenderPhoneNumber, row.WalletLabel, row.Request.CreatedAt,
                type, description, candidates));
        }
        return ApiResponse<IReadOnlyList<RechargeMessageConflictDto>>.Ok(conflicts);
    }
}

public sealed record ReassignRechargeSmsCommand(Guid TargetRechargeRequestId, Guid SmsLogId, Guid ActorUserId, string Reason)
    : IRequest<ApiResponse<bool>>;

public sealed class ReassignRechargeSmsCommandHandler(IAppDbContext db, IMediator mediator)
    : IRequestHandler<ReassignRechargeSmsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ReassignRechargeSmsCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<bool>.Fail("سبب نقل ربط التحويل مطلوب.");
        await using var transaction = await db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var sms = await db.IncomingSmsLogs.Include(row => row.MatchedRechargeRequest)
                .SingleOrDefaultAsync(row => row.Id == request.SmsLogId, ct);
            var target = await db.RechargeRequests.SingleOrDefaultAsync(row => row.Id == request.TargetRechargeRequestId, ct);
            var source = sms?.MatchedRechargeRequest;
            if (sms is null || target is null || source is null || source.Id == target.Id)
                return ApiResponse<bool>.Fail("بيانات التحويل أو الطلبين غير صالحة للنقل.");
            if (source.UserId == target.UserId)
                return ApiResponse<bool>.Fail("الطلبان تابعان لنفس حساب الطالب؛ استخدم تصحيح المحفظة ولا تنقل الرصيد بين طلبين لنفس الحساب.");
            if (target.Status != RechargeRequestStatus.Pending)
                return ApiResponse<bool>.Fail("الطلب المستهدف لم يعد معلقاً.");
            if (sms.ParsedAmount != target.Amount || Digits(sms.ParsedSenderPhone) != Digits(target.SenderPhoneNumber))
                return ApiResponse<bool>.Fail("رقم المحول أو المبلغ لا يطابق الطلب المستهدف.");

            var reversal = await mediator.Send(new ReverseRechargeCreditCommand(
                source.Id, request.ActorUserId, request.Reason.Trim(), PreserveWalletBalance: true), ct);
            if (!reversal.Success)
                return ApiResponse<bool>.Fail(reversal.Message ?? "تعذر عكس الرصيد القديم.");

            source.Status = RechargeRequestStatus.Rejected;
            source.RejectionReason = $"نُقل ربط التحويل إلى طلب صحيح: {request.Reason.Trim()}";
            source.ResolvedAt = DateTime.UtcNow;
            source.ResolvedByUserId = request.ActorUserId;
            source.MatchedSmsLogId = null;
            sms.IsMatched = false;
            sms.MatchedRechargeRequestId = null;
            db.AuditLogs.Add(new AuditLog
            {
                Action = "ReassignRechargeSms",
                EntityType = nameof(IncomingSmsLog),
                EntityId = sms.Id,
                PerformedByUserId = request.ActorUserId,
                Reason = request.Reason.Trim(),
                OldValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    RechargeRequestId = source.Id,
                    source.UserId,
                    source.WalletId
                }),
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    RechargeRequestId = target.Id,
                    target.UserId,
                    WalletId = sms.WalletId
                })
            });
            await db.SaveChangesAsync(ct);

            var approval = await mediator.Send(new ResolveRechargeRequestCommand(
                target.Id, true, request.ActorUserId, SmsLogId: sms.Id, WalletId: sms.WalletId), ct);
            if (!approval.Success)
                return ApiResponse<bool>.Fail(approval.Message ?? "تعذر شحن الحساب الصحيح.");
            await transaction.CommitAsync(ct);
            return ApiResponse<bool>.Ok(true, "تم عكس الربط القديم ونقل التحويل وشحن الطالب الصحيح.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static string Digits(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
