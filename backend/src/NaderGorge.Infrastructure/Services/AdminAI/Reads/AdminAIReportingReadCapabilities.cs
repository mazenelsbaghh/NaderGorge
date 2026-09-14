using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIReportingSummary(
    int ReportDefinitions, int AuditEvents, int PendingExportEvents, int WebVitalMetrics,
    int AttendanceKpiRows, int TaskKpiRows, int CrmKpiRows, int PayrollKpiRows,
    int MediaPipelines, int SocialPlans, DateTime DataAsOf);
public sealed class AdminAIReportingSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "reporting.summary"; public Type OutputType => typeof(AdminAIReportingSummary);
    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAIReportingSummary(
            await db.ReportDefinitions.AsNoTracking().CountAsync(ct),
            await db.AuditLogs.AsNoTracking().CountAsync(ct),
            await db.OutboxEvents.AsNoTracking().CountAsync(row => row.ProcessedAt == null, ct),
            await db.WebVitalsMetrics.AsNoTracking().CountAsync(ct),
            await db.AttendanceLogs.AsNoTracking().CountAsync(ct),
            await db.TaskItems.AsNoTracking().CountAsync(ct),
            await db.CrmCallLogs.AsNoTracking().CountAsync(ct),
            await db.PayrollRecords.AsNoTracking().CountAsync(ct),
            await db.MediaProductionPipelines.AsNoTracking().CountAsync(ct),
            await db.SocialMediaPlans.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.reports", "admin.audit"]);
    }
}
