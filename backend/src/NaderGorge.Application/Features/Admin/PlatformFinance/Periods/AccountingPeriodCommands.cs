using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.PlatformFinance.Periods;

public sealed record AccountingPeriodMutationResult(Guid Id, AccountingPeriodStatus Status, DateTime? ClosedAt, string Reason);

public sealed class AccountingPeriodCommands(IAppDbContext db)
{
    public async Task<AccountingPeriodMutationResult> CloseAsync(Guid periodId, Guid actorUserId, string reason, CancellationToken ct)
    {
        var period = await db.AccountingPeriods.SingleOrDefaultAsync(x => x.Id == periodId, ct)
            ?? throw new InvalidOperationException("FINANCE_PERIOD_NOT_FOUND");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("FINANCE_REASON_REQUIRED");
        if (period.Status == AccountingPeriodStatus.Closed) throw new InvalidOperationException("FINANCE_PERIOD_ALREADY_CLOSED");
        period.Status = AccountingPeriodStatus.Closed;
        period.ClosedAt = DateTime.UtcNow;
        period.ClosedByUserId = actorUserId;
        period.CloseReason = reason.Trim();
        db.AuditLogs.Add(new AuditLog { Action = "CloseAccountingPeriod", EntityType = nameof(AccountingPeriod), EntityId = period.Id, PerformedByUserId = actorUserId, Reason = period.CloseReason, NewValues = JsonSerializer.Serialize(new { period.Status, period.ClosedAt }) });
        await db.SaveChangesAsync(ct);
        return new(period.Id, period.Status, period.ClosedAt, period.CloseReason);
    }

    public async Task<AccountingPeriodMutationResult> ReopenAsync(Guid periodId, Guid actorUserId, string reason, CancellationToken ct)
    {
        var period = await db.AccountingPeriods.SingleOrDefaultAsync(x => x.Id == periodId, ct)
            ?? throw new InvalidOperationException("FINANCE_PERIOD_NOT_FOUND");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("FINANCE_REASON_REQUIRED");
        period.Status = AccountingPeriodStatus.Reopened;
        period.ClosedByUserId = actorUserId;
        period.CloseReason = reason.Trim();
        db.AuditLogs.Add(new AuditLog { Action = "ReopenAccountingPeriod", EntityType = nameof(AccountingPeriod), EntityId = period.Id, PerformedByUserId = actorUserId, Reason = period.CloseReason, NewValues = JsonSerializer.Serialize(new { period.Status }) });
        await db.SaveChangesAsync(ct);
        return new(period.Id, period.Status, period.ClosedAt, period.CloseReason);
    }
}
