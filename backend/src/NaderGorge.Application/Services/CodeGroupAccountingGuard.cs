using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public static class CodeGroupAccountingGuard
{
    public static async Task<bool> HasStartedAsync(IAppDbContext db, CodeGroup group, CancellationToken ct)
    {
        if (group.AccountingRecordedAt.HasValue) return true;
        if (await HasBatchChargeAsync(db, group.Id, ct)) return true;
        return await HasActivationAsync(db, group.Id, ct);
    }

    public static Task<bool> HasBatchChargeAsync(IAppDbContext db, Guid groupId, CancellationToken ct) =>
        db.TeacherFinancialEvents.AnyAsync(x => x.SourceType == TeacherFinancialSourceType.AccessCodeGeneration && x.SourceId == groupId, ct);

    public static Task<bool> HasActivationAsync(IAppDbContext db, Guid groupId, CancellationToken ct) =>
        db.AccessCodes.AnyAsync(code => code.CodeGroupId == groupId &&
            (code.IsConsumed || db.TeacherFinancialEvents.Any(financial => financial.SourceType == TeacherFinancialSourceType.AccessCodeActivation && financial.SourceId == code.Id)
                || db.AccessCodeActivationLogs.Any(log => log.AccessCodeId == code.Id)), ct);
}
