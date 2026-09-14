using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

/// <summary>
/// Reconstructs only sources with an authoritative amount and deterministic identity.
/// Existing journal idempotency keys make a replay a no-op; ambiguous legacy rows are
/// reported instead of being guessed into the general ledger.
/// </summary>
public sealed class PlatformFinanceMigrationService(
    IAppDbContext db,
    IFinancialPostingService posting) : IPlatformFinanceMigrationService
{
    private readonly IAppDbContext _db = db;
    private readonly IFinancialPostingService _posting = posting;

    public async Task<FinanceHistoricalMigrationPreview> PreviewAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var range = Normalize(from, to);
        var recharges = await _db.RechargeRequests.AsNoTracking()
            .Where(item => (item.Status == RechargeRequestStatus.Matched || item.Status == RechargeRequestStatus.Approved)
                && item.ResolvedAt >= range.From && item.ResolvedAt < range.To)
            .Select(item => new { item.Id, item.Amount })
            .ToListAsync(ct);
        var rechargeCandidates = await CountMissingAsync("RechargeRequest", recharges.Select(item => item.Id), ct);

        var sales = await _db.SalesFinancialEffects.AsNoTracking()
            .Where(item => item.PaidAmount > 0m
                && item.CreatedAt >= range.From && item.CreatedAt < range.To)
            .Select(item => new { item.PurchaseOperationId, item.PaidAmount })
            .ToListAsync(ct);
        var saleCandidates = await CountMissingAsync("Purchase", sales.Select(item => item.PurchaseOperationId), ct);
        var balanceAdjustments = await _db.BalanceTransactions.AsNoTracking()
            .Where(item => (item.TransactionType == "AdminAdjustment" || item.TransactionType == "CodeRedemption" || item.TransactionType == "Refund")
                && item.Amount != 0m && item.CreatedAt >= range.From && item.CreatedAt < range.To)
            .Select(item => new { item.Id, item.Amount })
            .ToListAsync(ct);
        var balanceCandidates = await CountMissingAsync("BalanceTransaction", balanceAdjustments.Select(item => item.Id), ct);
        var payouts = await _db.TeacherPayouts.AsNoTracking()
            .Where(item => item.Status == PayoutStatus.Paid && item.Amount > 0m
                && item.PaidAt >= range.From && item.PaidAt < range.To)
            .Select(item => new { item.Id, item.Amount })
            .ToListAsync(ct);
        var payoutCandidates = await CountMissingAsync("TeacherPayout", payouts.Select(item => item.Id), ct);
        var payroll = await ApprovedPayrollAsync(range, ct);
        var payrollCandidates = await CountMissingAsync("Payroll", payroll.Select(item => item.Id), ct);
        var teacherEvidenceRows = await _db.TeacherFinancialEvents.AsNoTracking()
            .CountAsync(item => item.OccurredAt >= range.From && item.OccurredAt < range.To, ct);
        var ambiguities = teacherEvidenceRows == 0
            ? Array.Empty<string>()
            : new[] { $"{teacherEvidenceRows} teacher financial events are reconciliation evidence already represented by sales and payout sources." };

        return new FinanceHistoricalMigrationPreview(
            range.From,
            range.To.AddTicks(-1),
            rechargeCandidates.Count,
            recharges.Where(item => rechargeCandidates.Contains(item.Id)).Sum(item => item.Amount),
            saleCandidates.Count,
            sales.Where(item => saleCandidates.Contains(item.PurchaseOperationId)).Sum(item => item.PaidAmount),
            balanceCandidates.Count,
            balanceAdjustments.Where(item => balanceCandidates.Contains(item.Id)).Sum(item => Math.Abs(item.Amount)),
            payoutCandidates.Count,
            payouts.Where(item => payoutCandidates.Contains(item.Id)).Sum(item => item.Amount),
            payrollCandidates.Count,
            payroll.Where(item => payrollCandidates.Contains(item.Id)).Sum(item => item.Amount),
            teacherEvidenceRows,
            ambiguities);
    }

    public async Task<FinanceHistoricalMigrationResult> PostAsync(DateTime from, DateTime to, Guid actorUserId, CancellationToken ct)
    {
        var range = Normalize(from, to);
        var batchId = Guid.NewGuid();
        var batch = new FinancialMigrationBatch
        {
            Id = batchId,
            From = range.From,
            To = range.To.AddTicks(-1),
            Status = FinanceMigrationBatchStatus.Running,
            CreatedByUserId = actorUserId,
            SourceChecksum = string.Empty
        };
        _db.FinancialMigrationBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
        var errors = new List<string>();
        var posted = 0;
        var alreadyPosted = 0;
        var failed = 0;

        var recharges = await _db.RechargeRequests
            .Where(item => (item.Status == RechargeRequestStatus.Matched || item.Status == RechargeRequestStatus.Approved)
                && item.ResolvedAt >= range.From && item.ResolvedAt < range.To)
            .ToListAsync(ct);
        foreach (var recharge in recharges)
        {
            var key = $"recharge:{recharge.Id:N}:approved";
            if (await _db.JournalEntries.AnyAsync(item => item.IdempotencyKey == key, ct))
            {
                alreadyPosted++;
                await AddItemAsync(batch, "RechargeRequest", recharge.Id, recharge.Amount, FinanceMigrationItemStatus.AlreadyPosted, null, ct);
                continue;
            }

            try
            {
                var treasuryCode = await (from treasury in _db.TreasuryAccounts
                                          join account in _db.FinancialAccounts on treasury.FinancialAccountId equals account.Id
                                          where treasury.DigitalWalletId == recharge.WalletId && treasury.IsActive && account.IsActive
                                          select account.Code).SingleOrDefaultAsync(ct) ?? "1000";
                var journal = await _posting.PostAsync(new FinancialPostingRequest(
                    "RechargeRequest", recharge.Id, "HistoricalRecharge", key,
                    recharge.TeacherId.HasValue ? "إعادة بناء شحن رصيد مدرس" : "إعادة بناء شحن رصيد عام",
                    recharge.ResolvedAt ?? recharge.CreatedAt,
                    actorUserId,
                    [new FinancialPostingLine(treasuryCode, recharge.Amount, 0m, StudentId: recharge.UserId),
                     new FinancialPostingLine(recharge.TeacherId.HasValue ? "1110" : "1100", 0m, recharge.Amount, StudentId: recharge.UserId, TeacherId: recharge.TeacherId)]), ct);
                await AddItemAsync(batch, "RechargeRequest", recharge.Id, recharge.Amount, FinanceMigrationItemStatus.Posted, journal.Id, ct);
                posted++;
            }
            catch (Exception exception)
            {
                failed++;
                await AddItemAsync(batch, "RechargeRequest", recharge.Id, recharge.Amount, FinanceMigrationItemStatus.Failed, null, ct, exception.Message);
                errors.Add($"RechargeRequest/{recharge.Id}: {exception.Message}");
            }
        }

        var sales = await _db.SalesFinancialEffects
            .Where(item => item.PaidAmount > 0m
                && item.CreatedAt >= range.From && item.CreatedAt < range.To)
            .ToListAsync(ct);
        foreach (var sale in sales)
        {
            var key = $"purchase:{sale.PurchaseOperationId:N}";
            if (await _db.JournalEntries.AnyAsync(item => item.IdempotencyKey == key, ct))
            {
                alreadyPosted++;
                await AddItemAsync(batch, "Purchase", sale.PurchaseOperationId, sale.PaidAmount, FinanceMigrationItemStatus.AlreadyPosted, null, ct);
                continue;
            }

            try
            {
                var lines = new List<FinancialPostingLine>
                {
                    new("1100", sale.PaidAmount, 0m, StudentId: sale.StudentId)
                };
                AddSignedLine(lines, "4000", sale.PlatformShareImpact, sale.StudentId, null);
                if (sale.TeacherShareImpact != 0m)
                    AddSignedLine(lines, "2000", sale.TeacherShareImpact, sale.StudentId, sale.TeacherId);
                var journal = await _posting.PostAsync(new FinancialPostingRequest(
                    "Purchase", sale.PurchaseOperationId, "HistoricalPurchase", key,
                    "إعادة بناء عملية بيع", sale.CreatedAt, actorUserId, lines), ct);
                await AddItemAsync(batch, "Purchase", sale.PurchaseOperationId, sale.PaidAmount, FinanceMigrationItemStatus.Posted, journal.Id, ct);
                posted++;
            }
            catch (Exception exception)
            {
                failed++;
                await AddItemAsync(batch, "Purchase", sale.PurchaseOperationId, sale.PaidAmount, FinanceMigrationItemStatus.Failed, null, ct, exception.Message);
                errors.Add($"Purchase/{sale.PurchaseOperationId}: {exception.Message}");
            }
        }

        var balanceAdjustments = await _db.BalanceTransactions
            .Include(item => item.StudentBalance)
            .Where(item => (item.TransactionType == "AdminAdjustment" || item.TransactionType == "CodeRedemption" || item.TransactionType == "Refund")
                && item.Amount != 0m && item.CreatedAt >= range.From && item.CreatedAt < range.To)
            .ToListAsync(ct);
        foreach (var adjustment in balanceAdjustments)
        {
            var key = $"balance:{adjustment.Id:N}";
            if (await _db.JournalEntries.AnyAsync(item => item.IdempotencyKey == key, ct))
            {
                alreadyPosted++;
                await AddItemAsync(batch, "BalanceTransaction", adjustment.Id, Math.Abs(adjustment.Amount), FinanceMigrationItemStatus.AlreadyPosted, null, ct);
                continue;
            }

            try
            {
                var lines = BalanceAdjustmentLines(adjustment);
                var journal = await _posting.PostAsync(new FinancialPostingRequest(
                    "BalanceTransaction", adjustment.Id, "HistoricalBalanceAdjustment", key,
                    $"إعادة بناء حركة رصيد: {adjustment.TransactionType}", adjustment.CreatedAt, actorUserId, lines), ct);
                await AddItemAsync(batch, "BalanceTransaction", adjustment.Id, Math.Abs(adjustment.Amount), FinanceMigrationItemStatus.Posted, journal.Id, ct);
                posted++;
            }
            catch (Exception exception)
            {
                failed++;
                await AddItemAsync(batch, "BalanceTransaction", adjustment.Id, Math.Abs(adjustment.Amount), FinanceMigrationItemStatus.Failed, null, ct, exception.Message);
                errors.Add($"BalanceTransaction/{adjustment.Id}: {exception.Message}");
            }
        }

        var payouts = await _db.TeacherPayouts
            .Where(item => item.Status == PayoutStatus.Paid && item.Amount > 0m
                && item.PaidAt >= range.From && item.PaidAt < range.To)
            .ToListAsync(ct);
        foreach (var payout in payouts)
        {
            var key = $"teacher-payout:{payout.Id:N}:paid";
            if (await _db.JournalEntries.AnyAsync(item => item.IdempotencyKey == key, ct))
            {
                alreadyPosted++;
                await AddItemAsync(batch, "TeacherPayout", payout.Id, payout.Amount, FinanceMigrationItemStatus.AlreadyPosted, null, ct);
                continue;
            }

            var payoutPosted = await PostSimpleAsync(new HistoricalPostingContext(batch, actorUserId, errors), new HistoricalPosting(
                "TeacherPayout", payout.Id, payout.Amount, key, "HistoricalTeacherPayout", "إعادة بناء سداد مستحقات مدرس",
                payout.PaidAt ?? payout.CreatedAt,
                [new("2000", payout.Amount, 0m, TeacherId: payout.TeacherId), new("1000", 0m, payout.Amount, TeacherId: payout.TeacherId)]), ct);
            if (payoutPosted) posted++; else failed++;
        }

        var payroll = await ApprovedPayrollAsync(range, ct);
        foreach (var record in payroll)
        {
            var key = $"payroll:{record.Id:N}:approved";
            if (await _db.JournalEntries.AnyAsync(item => item.IdempotencyKey == key, ct))
            {
                alreadyPosted++;
                await AddItemAsync(batch, "Payroll", record.Id, record.Amount, FinanceMigrationItemStatus.AlreadyPosted, null, ct);
                continue;
            }

            var payrollPosted = await PostSimpleAsync(new HistoricalPostingContext(batch, actorUserId, errors), new HistoricalPosting(
                "Payroll", record.Id, record.Amount, key, "HistoricalPayroll", "إعادة بناء مصروف راتب",
                record.OccurredAt, [new("5100", record.Amount, 0m), new("1000", 0m, record.Amount)]), ct);
            if (payrollPosted) posted++; else failed++;
        }

        batch.CandidateCount = posted + alreadyPosted + failed;
        batch.PostedCount = posted;
        batch.AlreadyPostedCount = alreadyPosted;
        batch.FailedCount = failed;
        batch.Status = failed == 0 ? FinanceMigrationBatchStatus.Completed : FinanceMigrationBatchStatus.CompletedWithErrors;
        batch.CompletedAt = DateTime.UtcNow;
        batch.SourceChecksum = ComputeChecksum(batch.Items);
        await _db.SaveChangesAsync(ct);
        return new FinanceHistoricalMigrationResult(batchId, posted, alreadyPosted, failed, errors);
    }

    private async Task AddItemAsync(FinancialMigrationBatch batch, string sourceType, Guid sourceId, decimal amount, FinanceMigrationItemStatus status, Guid? journalId, CancellationToken ct, string? error = null)
    {
        if (await _db.FinancialMigrationItems.AnyAsync(item => item.SourceType == sourceType && item.SourceId == sourceId, ct)) return;
        _db.FinancialMigrationItems.Add(new FinancialMigrationItem { FinancialMigrationBatchId = batch.Id, SourceType = sourceType, SourceId = sourceId, Amount = decimal.Round(amount, 2), Status = status, JournalEntryId = journalId, ErrorMessage = error, SourceChecksum = ComputeChecksum(sourceType, sourceId, amount) });
    }

    private static string ComputeChecksum(IEnumerable<FinancialMigrationItem> items) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', items.OrderBy(item => item.SourceType).ThenBy(item => item.SourceId).Select(item => $"{item.SourceType}:{item.SourceId:N}:{item.Amount:N2}:{item.Status}")))));
    private static string ComputeChecksum(string sourceType, Guid sourceId, decimal amount) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceType}:{sourceId:N}:{amount:N2}")));

    private async Task<HashSet<Guid>> CountMissingAsync(string sourceType, IEnumerable<Guid> sourceIds, CancellationToken ct)
    {
        var ids = sourceIds.Distinct().ToArray();
        var existing = await _db.JournalEntries.AsNoTracking()
            .Where(item => item.SourceType == sourceType && item.SourceId.HasValue && ids.Contains(item.SourceId.Value))
            .Select(item => item.SourceId!.Value)
            .ToListAsync(ct);
        return ids.Where(id => !existing.Contains(id)).ToHashSet();
    }

    private static void AddSignedLine(List<FinancialPostingLine> lines, string code, decimal amount, Guid studentId, Guid? teacherId)
    {
        if (amount >= 0m)
            lines.Add(new FinancialPostingLine(code, 0m, amount, StudentId: studentId, TeacherId: teacherId));
        else
            lines.Add(new FinancialPostingLine(code, -amount, 0m, StudentId: studentId, TeacherId: teacherId));
    }

    private static IReadOnlyList<FinancialPostingLine> BalanceAdjustmentLines(BalanceTransaction adjustment)
    {
        var studentId = adjustment.StudentBalance.UserId;
        var counterpart = adjustment.TransactionType == "Refund" ? "4100" : "9990";
        return adjustment.Amount > 0m
            ? [new(counterpart, adjustment.Amount, 0m, StudentId: studentId), new("1100", 0m, adjustment.Amount, StudentId: studentId)]
            : [new("1100", -adjustment.Amount, 0m, StudentId: studentId), new(counterpart, 0m, -adjustment.Amount, StudentId: studentId)];
    }

    private async Task<List<HistoricalPayroll>> ApprovedPayrollAsync((DateTime From, DateTime To) range, CancellationToken ct)
    {
        var payroll = await _db.PayrollRecords.AsNoTracking()
            .Where(item => item.Status == PayrollStatus.Approved && item.CreatedAt >= range.From && item.CreatedAt < range.To)
            .Select(item => new HistoricalPayroll(
                item.Id,
                item.BasicSalary
                    + item.Adjustments.Where(adjustment => adjustment.Type == PayrollAdjustmentType.Addition).Sum(adjustment => adjustment.Amount)
                    - item.Adjustments.Where(adjustment => adjustment.Type == PayrollAdjustmentType.Deduction).Sum(adjustment => adjustment.Amount),
                item.ApprovedAt ?? item.CreatedAt))
            .ToListAsync(ct);
        return payroll.Where(item => item.Amount > 0m).ToList();
    }

    private async Task<bool> PostSimpleAsync(
        HistoricalPostingContext context,
        HistoricalPosting source,
        CancellationToken ct)
    {
        try
        {
            var journal = await _posting.PostAsync(new FinancialPostingRequest(
                source.SourceType, source.SourceId, source.EntryType, source.Key, source.Description,
                source.OccurredAt, context.ActorUserId, source.Lines), ct);
            await AddItemAsync(context.Batch, source.SourceType, source.SourceId, source.Amount, FinanceMigrationItemStatus.Posted, journal.Id, ct);
            return true;
        }
        catch (Exception exception)
        {
            await AddItemAsync(context.Batch, source.SourceType, source.SourceId, source.Amount, FinanceMigrationItemStatus.Failed, null, ct, exception.Message);
            context.Errors.Add($"{source.SourceType}/{source.SourceId}: {exception.Message}");
            return false;
        }
    }

    private sealed record HistoricalPayroll(Guid Id, decimal Amount, DateTime OccurredAt);
    private sealed record HistoricalPostingContext(FinancialMigrationBatch Batch, Guid ActorUserId, List<string> Errors);
    private sealed record HistoricalPosting(string SourceType, Guid SourceId, decimal Amount, string Key, string EntryType, string Description, DateTime OccurredAt, IReadOnlyList<FinancialPostingLine> Lines);

    private static (DateTime From, DateTime To) Normalize(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);
        if (end <= start) throw new ArgumentException("The migration end date must be after the start date.");
        return (start, end);
    }
}
