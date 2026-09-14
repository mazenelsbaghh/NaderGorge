using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Migration;
using NaderGorge.Application.Features.HR.Reporting;
using NaderGorge.Application.Features.HR.Retention;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController, Route("api/hr/governance"), Authorize]
public sealed class HrGovernanceController(IAppDbContext db, HrMigrationService migrationService, HrRetentionService retentionService, WorkforceReportService reportService) : ControllerBase
{
    [HttpGet("migration"), HasPermission(HrPermissions.MigrationRead)]
    public async Task<IActionResult> MigrationStatus(CancellationToken ct) => Ok(new
    {
        rollouts = await db.HrModuleRollouts.AsNoTracking().OrderBy(item => item.Module).ToListAsync(ct),
        batches = await db.HrMigrationBatches.AsNoTracking().OrderByDescending(item => item.CreatedAt).Select(item => new { item.Id, item.Module, item.SourceSystem,
            state = item.State.ToString(), item.SourceCount, item.TargetCount, item.SourceTotal, item.TargetTotal, item.SourceHash, item.TargetHash, item.ReportJson, item.CreatedAt }).Take(100).ToListAsync(ct),
        conflicts = await db.HrMigrationConflicts.AsNoTracking().Where(item => item.State == Domain.Enums.HrMigrationConflictState.Open).Select(item => new { item.Id, item.MigrationBatchId, item.SourceType, item.SourceId, item.Code, item.DetailsJson }).Take(200).ToListAsync(ct)
    });

    [HttpPost("migration/dry-run"), HasPermission(HrPermissions.MigrationManage)]
    public async Task<IActionResult> DryRun(MigrationRowsRequest request, CancellationToken ct)
    {
        var result = await migrationService.DryRunAsync(request.Module, request.SourceSystem, request.Rows, User.RequireUserId(), ct); return Ok(result);
    }
    [HttpPost("migration/{batchId:guid}/reconcile"), HasPermission(HrPermissions.MigrationManage)]
    public async Task<IActionResult> Reconcile(Guid batchId, MigrationRowsRequest request, CancellationToken ct)
    {
        var result = await migrationService.ApplyAndReconcileAsync(batchId, request.Rows, User.RequireUserId(), ct); return result.Success ? Ok(result) : Conflict(result);
    }
    [HttpPost("migration/{batchId:guid}/activate"), HasPermission(HrPermissions.MigrationManage)]
    public async Task<IActionResult> Activate(Guid batchId, RolloutRequest request, CancellationToken ct)
    {
        var result = await migrationService.ActivateAsync(request.Module, batchId, User.RequireUserId(), request.Reason, ct); return result.Success ? Ok(result) : Conflict(result);
    }
    [HttpPost("migration/rollback"), HasPermission(HrPermissions.MigrationManage)]
    public async Task<IActionResult> Rollback(RolloutRequest request, CancellationToken ct)
    {
        var result = await migrationService.RollbackAsync(request.Module, User.RequireUserId(), request.Reason, ct); return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("retention/dry-run"), HasPermission(HrPermissions.MigrationRead)]
    public async Task<IActionResult> RetentionDryRun(RetentionRequest request, CancellationToken ct) => Ok(await retentionService.DryRunAsync(request.Today, request.CandidateRetentionYears, ct));
    [HttpPost("retention/execute"), HasPermission(HrPermissions.MigrationManage)]
    public async Task<IActionResult> RetentionExecute(RetentionRequest request, CancellationToken ct) => Ok(await retentionService.ExecuteAsync(request.Today, request.CandidateRetentionYears, User.RequireUserId(), request.Reason, ct));

    [HttpGet("reports/workforce"), HasPermission(HrPermissions.ReportRead)]
    public async Task<IActionResult> Workforce([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? organizationUnitId,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await reportService.QueryAsync(User.RequireUserId(), new WorkforceReportFilter(from, to, organizationUnitId, search, page, pageSize), ct));

    [HttpGet("reports/workforce/export"), HasPermission(HrPermissions.ReportExport)]
    public async Task<IActionResult> ExportWorkforce([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? organizationUnitId,
        [FromQuery] string? search, [FromQuery] string reason, CancellationToken ct)
    {
        var csv = await reportService.ExportCsvAsync(User.RequireUserId(), new WorkforceReportFilter(from, to, organizationUnitId, search, 1, 200), reason, ct);
        return File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", $"workforce-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}

public sealed record MigrationRowsRequest(string Module, string SourceSystem, IReadOnlyList<HrMigrationRow> Rows);
public sealed record RolloutRequest(string Module, string Reason);
public sealed record RetentionRequest(DateOnly Today, int CandidateRetentionYears, string Reason);
