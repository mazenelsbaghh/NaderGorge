using Microsoft.EntityFrameworkCore;
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
            .Where(item => item.Status == RechargeRequestStatus.Approved
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
        var balanceEvidenceRows = await _db.BalanceTransactions.AsNoTracking()
            .CountAsync(item => item.CreatedAt >= range.From && item.CreatedAt < range.To, ct);
        var ambiguities = balanceEvidenceRows == 0
            ? Array.Empty<string>()
            : new[] { $"{balanceEvidenceRows} balance transaction rows have no authoritative wallet/source mapping and remain operational evidence." };

        return new FinanceHistoricalMigrationPreview(
            range.From,
            range.To.AddTicks(-1),
            rechargeCandidates.Count,
            recharges.Where(item => rechargeCandidates.Contains(item.Id)).Sum(item => item.Amount),
            saleCandidates.Count,
            sales.Where(item => saleCandidates.Contains(item.PurchaseOperationId)).Sum(item => item.PaidAmount),
            balanceEvidenceRows,
            ambiguities);
    }

    public async Task<FinanceHistoricalMigrationResult> PostAsync(DateTime from, DateTime to, Guid actorUserId, CancellationToken ct)
    {
        var range = Normalize(from, to);
        var batchId = Guid.NewGuid();
        var errors = new List<string>();
        var posted = 0;
        var alreadyPosted = 0;
        var failed = 0;

        var recharges = await _db.RechargeRequests
            .Where(item => item.Status == RechargeRequestStatus.Approved
                && item.ResolvedAt >= range.From && item.ResolvedAt < range.To)
            .ToListAsync(ct);
        foreach (var recharge in recharges)
        {
            var key = $"recharge:{recharge.Id:N}:approved";
            if (await _db.JournalEntries.AnyAsync(item => item.IdempotencyKey == key, ct))
            {
                alreadyPosted++;
                continue;
            }

            try
            {
                var treasuryCode = await (from treasury in _db.TreasuryAccounts
                                          join account in _db.FinancialAccounts on treasury.FinancialAccountId equals account.Id
                                          where treasury.DigitalWalletId == recharge.WalletId && treasury.IsActive && account.IsActive
                                          select account.Code).SingleOrDefaultAsync(ct) ?? "1000";
                await _posting.PostAsync(new FinancialPostingRequest(
                    "RechargeRequest", recharge.Id, "HistoricalRecharge", key,
                    recharge.TeacherId.HasValue ? "إعادة بناء شحن رصيد مدرس" : "إعادة بناء شحن رصيد عام",
                    recharge.ResolvedAt ?? recharge.CreatedAt,
                    actorUserId,
                    [new FinancialPostingLine(treasuryCode, recharge.Amount, 0m, StudentId: recharge.UserId),
                     new FinancialPostingLine(recharge.TeacherId.HasValue ? "1110" : "1100", 0m, recharge.Amount, StudentId: recharge.UserId, TeacherId: recharge.TeacherId)]), ct);
                posted++;
            }
            catch (Exception exception)
            {
                failed++;
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
                await _posting.PostAsync(new FinancialPostingRequest(
                    "Purchase", sale.PurchaseOperationId, "HistoricalPurchase", key,
                    "إعادة بناء عملية بيع", sale.CreatedAt, actorUserId, lines), ct);
                posted++;
            }
            catch (Exception exception)
            {
                failed++;
                errors.Add($"Purchase/{sale.PurchaseOperationId}: {exception.Message}");
            }
        }

        return new FinanceHistoricalMigrationResult(batchId, posted, alreadyPosted, failed, errors);
    }

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

    private static (DateTime From, DateTime To) Normalize(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);
        if (end <= start) throw new ArgumentException("The migration end date must be after the start date.");
        return (start, end);
    }
}
