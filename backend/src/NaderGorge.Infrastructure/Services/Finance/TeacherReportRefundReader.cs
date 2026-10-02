using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.PlatformFinance;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.Finance;

internal sealed class TeacherReportRefundReader(IAppDbContext db)
{
    public async Task<TeacherReportRefund[]> ReadAsync(Guid teacherId, List<StudentAccessGrant> grants,
        DateTime end, CancellationToken ct)
    {
        var entries = new FinancialLedgerQuery(db).Entries;
        var refunds = await (from refund in db.PlatformRefunds.AsNoTracking()
            join journal in entries on refund.JournalEntryId equals (Guid?)journal.Id
            join student in db.Users.AsNoTracking() on refund.StudentId equals student.Id
            where refund.TeacherId == teacherId && (refund.Status == PlatformRefundStatus.Posted || refund.Status == PlatformRefundStatus.Reversed)
                && journal.OccurredAt < end
            select new { JournalId = journal.Id, Item = new TeacherReportRefund(student.Id, student.FullName, refund.AccessGrantId, refund.OriginalSourceId,
                refund.PlatformAmount + refund.TeacherAmount, journal.OccurredAt,
                refund.Method == PlatformRefundMethod.Cash ? "رد نقدي" : "رد لرصيد الطالب", refund.Reason) }).ToListAsync(ct);
        var posted = refunds.Select(x => x.Item).ToList();
        var journalIds = refunds.Select(x => x.JournalId).ToArray();
        var reversals = await entries.Where(x => x.PostingKind == "Reversal" && x.OccurredAt < end
            && journalIds.Contains(x.ReversalOfId ?? x.SourceId ?? Guid.Empty))
            .Select(x => new { JournalId = x.ReversalOfId ?? x.SourceId, x.OccurredAt }).ToListAsync(ct);
        foreach (var reversal in reversals)
        {
            var original = refunds.Single(x => x.JournalId == reversal.JournalId).Item;
            posted.Add(original with { Amount = -original.Amount, At = reversal.OccurredAt, Method = "إلغاء استرداد", Reason = "قيد عكس الاسترداد المسجل" });
        }
        var byGrant = grants.ToDictionary(x => x.Id);
        var ids = byGrant.Keys.ToArray();
        var legacy = await db.AuditLogs.AsNoTracking().Where(x => x.Action == "CANCEL_PACKAGE_GRANT"
            && x.EntityType == "StudentAccessGrant" && x.EntityId.HasValue && ids.Contains(x.EntityId.Value) && x.CreatedAt < end)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var legacyGrants = new HashSet<Guid>();
        foreach (var audit in legacy)
        {
            if (string.IsNullOrWhiteSpace(audit.NewValues)) continue;
            using var values = JsonDocument.Parse(audit.NewValues);
            var grant = byGrant[audit.EntityId!.Value];
            var operation = values.RootElement.TryGetProperty("purchaseOperationId", out var source)
                && source.ValueKind == JsonValueKind.String && source.TryGetGuid(out var purchaseId) ? purchaseId : grant.Id;
            if (!values.RootElement.TryGetProperty("refundedAmount", out var amount) || amount.ValueKind != JsonValueKind.Number
                || !amount.TryGetDecimal(out var refunded) || refunded <= 0) continue;
            if (posted.Any(x => x.StudentId == grant.UserId && (x.GrantId == grant.Id || x.SourceId == operation || x.SourceId == grant.Id))) continue;
            if (!legacyGrants.Add(grant.Id)) continue;
            posted.Add(new(grant.UserId, grant.User.FullName, grant.Id, operation, refunded,
                audit.CreatedAt, "رد لرصيد الطالب", grant.CancellationReason ?? "استرداد اشتراك"));
        }
        return posted.OrderBy(x => x.At).ThenBy(x => x.SourceId).ToArray();
    }
}
