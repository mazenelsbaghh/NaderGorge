using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

/// <summary>Single transactional refund posting path shared by the admin workflow and replay tests.</summary>
public sealed class RefundPostingService(IAppDbContext db, IFinancialPostingService posting, BalanceService balanceService)
{
    public async Task<PlatformRefund> PostAsync(PlatformRefund refund, string idempotencyKey, Guid actorUserId, CancellationToken ct)
    {
        if (refund.Status != PlatformRefundStatus.Draft) throw new InvalidOperationException("FINANCE_ALREADY_POSTED");
        var creditAccount = refund.Method == PlatformRefundMethod.Cash
            ? await TreasuryCodeAsync(refund.TreasuryAccountId ?? throw new InvalidOperationException("FINANCE_TREASURY_REQUIRED"), ct)
            : "1100";
        var lines = new List<FinancialPostingLine>
        {
            new("4100", refund.PlatformAmount, 0m, StudentId: refund.StudentId),
            new(creditAccount, 0m, refund.TotalAmount, StudentId: refund.StudentId, TreasuryAccountId: refund.TreasuryAccountId)
        };
        if (refund.TeacherAmount > 0m)
            lines.Insert(1, new FinancialPostingLine("2000", refund.TeacherAmount, 0m, StudentId: refund.StudentId, TeacherId: refund.TeacherId));

        var transaction = db is DbContext context && context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL" && context.Database.CurrentTransaction is null
            ? await db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct)
            : null;
        try
        {
            var journal = await posting.PostAsync(new FinancialPostingRequest("PlatformRefund", refund.Id, "RefundPost", idempotencyKey, refund.Reason, DateTime.UtcNow, actorUserId, lines), ct);
            if (refund.Method == PlatformRefundMethod.StudentBalance)
                await balanceService.AddCredit(refund.StudentId, refund.TotalAmount, $"استرداد مالي: {refund.Reason}", refund.Id, "PlatformRefund", ct);
            refund.JournalEntryId = journal.Id;
            refund.Status = PlatformRefundStatus.Posted;
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return refund;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private async Task<string> TreasuryCodeAsync(Guid treasuryId, CancellationToken ct) =>
        await (from treasury in db.TreasuryAccounts
               join account in db.FinancialAccounts on treasury.FinancialAccountId equals account.Id
               where treasury.Id == treasuryId && treasury.IsActive && account.IsActive
               select account.Code).SingleOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("FINANCE_TREASURY_NOT_FOUND");
}
