using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Migration;

public sealed record HrMigrationRow(string SourceType, string SourceId, Guid TargetId, decimal Amount, string SourceHash);

public sealed class HrMigrationService(IAppDbContext db)
{
    private static readonly string[] ModuleOrder = ["people", "attendance", "leave", "payroll", "remaining"];

    public async Task<ApiResponse<Guid>> DryRunAsync(string module, string sourceSystem, IReadOnlyCollection<HrMigrationRow> rows, Guid actorUserId, CancellationToken ct)
    {
        module = NormalizeModule(module); var requestHash = HashRows(rows); var existing = await db.HrMigrationBatches.SingleOrDefaultAsync(item => item.Module == module && item.RequestHash == requestHash, ct);
        if (existing is not null) return ApiResponse<Guid>.Ok(existing.Id);
        var duplicates = rows.GroupBy(item => new { item.SourceType, item.SourceId }).Where(group => group.Count() > 1).ToList();
        var batch = new HrMigrationBatch { Module = module, SourceSystem = sourceSystem.Trim(), RequestHash = requestHash, SourceCount = rows.Count,
            SourceTotal = rows.Sum(item => item.Amount), SourceHash = requestHash, CreatedByUserId = actorUserId,
            ReportJson = JsonSerializer.Serialize(new { dryRun = true, sourceCount = rows.Count, sourceTotal = rows.Sum(item => item.Amount), duplicateKeys = duplicates.Select(item => item.Key) }) };
        db.HrMigrationBatches.Add(batch);
        foreach (var duplicate in duplicates) db.HrMigrationConflicts.Add(new HrMigrationConflict { MigrationBatchId = batch.Id, SourceType = duplicate.Key.SourceType,
            SourceId = duplicate.Key.SourceId, Code = "DUPLICATE_SOURCE_KEY", DetailsJson = JsonSerializer.Serialize(duplicate) });
        await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(batch.Id);
    }

    public async Task<ApiResponse<bool>> ApplyAndReconcileAsync(Guid batchId, IReadOnlyCollection<HrMigrationRow> rows, Guid actorUserId, CancellationToken ct)
    {
        var batch = await db.HrMigrationBatches.Include(item => item.RecordMaps).Include(item => item.Conflicts).SingleOrDefaultAsync(item => item.Id == batchId, ct);
        if (batch is null) return ApiResponse<bool>.Fail("دفعة الترحيل غير موجودة", ["MIGRATION_BATCH_NOT_FOUND"]);
        foreach (var row in rows)
        {
            var existing = await db.HrMigrationRecordMaps.SingleOrDefaultAsync(item => item.SourceType == row.SourceType && item.SourceId == row.SourceId, ct);
            if (existing is not null)
            {
                if (existing.SourceHash != row.SourceHash && !await db.HrMigrationConflicts.AnyAsync(item => item.MigrationBatchId == batchId && item.SourceType == row.SourceType && item.SourceId == row.SourceId && item.Code == "SOURCE_CHANGED", ct))
                    db.HrMigrationConflicts.Add(new HrMigrationConflict { MigrationBatchId = batchId, SourceType = row.SourceType, SourceId = row.SourceId,
                        Code = "SOURCE_CHANGED", DetailsJson = JsonSerializer.Serialize(new { previous = existing.SourceHash, current = row.SourceHash, actorUserId }) });
                continue;
            }
            db.HrMigrationRecordMaps.Add(new HrMigrationRecordMap { MigrationBatchId = batchId, SourceType = row.SourceType, SourceId = row.SourceId,
                SourceHash = row.SourceHash, TargetType = batch.Module, TargetId = row.TargetId, Amount = row.Amount });
        }
        await db.SaveChangesAsync(ct);
        batch.TargetCount = await db.HrMigrationRecordMaps.CountAsync(item => item.MigrationBatchId == batchId, ct);
        batch.TargetTotal = await db.HrMigrationRecordMaps.Where(item => item.MigrationBatchId == batchId).SumAsync(item => item.Amount, ct);
        batch.TargetHash = HashRows(await db.HrMigrationRecordMaps.Where(item => item.MigrationBatchId == batchId).OrderBy(item => item.SourceType).ThenBy(item => item.SourceId)
            .Select(item => new HrMigrationRow(item.SourceType, item.SourceId, item.TargetId, item.Amount, item.SourceHash)).ToListAsync(ct));
        var openConflicts = await db.HrMigrationConflicts.CountAsync(item => item.MigrationBatchId == batchId && item.State == HrMigrationConflictState.Open, ct);
        var reconciled = openConflicts == 0 && batch.SourceCount == batch.TargetCount && batch.SourceTotal == batch.TargetTotal;
        batch.State = reconciled ? HrMigrationBatchState.Reconciled : HrMigrationBatchState.Failed; batch.ReconciledAt = DateTime.UtcNow;
        batch.ReportJson = JsonSerializer.Serialize(new { batch.SourceCount, batch.TargetCount, batch.SourceTotal, batch.TargetTotal, batch.SourceHash, batch.TargetHash, openConflicts, reconciled });
        await db.SaveChangesAsync(ct); return reconciled ? ApiResponse<bool>.Ok(true) : ApiResponse<bool>.Fail("فشل التطابق", ["MIGRATION_RECONCILIATION_FAILED"]);
    }

    public async Task<ApiResponse<bool>> ActivateAsync(string module, Guid batchId, Guid actorUserId, string reason, CancellationToken ct)
    {
        module = NormalizeModule(module); var batch = await db.HrMigrationBatches.SingleOrDefaultAsync(item => item.Id == batchId && item.Module == module, ct);
        var activationReady = batch?.State is HrMigrationBatchState.Reconciled or HrMigrationBatchState.RolledBack;
        if (!activationReady || batch!.SourceCount != batch.TargetCount || batch.SourceTotal != batch.TargetTotal || batch.SourceHash != batch.TargetHash ||
            await db.HrMigrationConflicts.AnyAsync(item => item.MigrationBatchId == batchId && item.State == HrMigrationConflictState.Open, ct))
            return ApiResponse<bool>.Fail("الدفعة غير جاهزة للتفعيل", ["MIGRATION_NOT_RECONCILED"]);
        var index = Array.IndexOf(ModuleOrder, module);
        if (index > 0)
        {
            var required = ModuleOrder[index - 1]; if (!await db.HrModuleRollouts.AnyAsync(item => item.Module == required && item.State == HrModuleRolloutState.NewActive, ct))
                return ApiResponse<bool>.Fail("الوحدة السابقة لم تُفعّل", ["MIGRATION_DEPENDENCY_NOT_ACTIVE"]);
        }
        var rollout = await db.HrModuleRollouts.SingleOrDefaultAsync(item => item.Module == module, ct) ?? new HrModuleRollout { Module = module };
        if (db.Entry(rollout).State == EntityState.Detached) db.HrModuleRollouts.Add(rollout);
        rollout.State = HrModuleRolloutState.NewActive; rollout.ReadTarget = "new"; rollout.WriteTarget = "new"; rollout.ChangedByUserId = actorUserId;
        rollout.ChangedAt = DateTime.UtcNow; rollout.ReconciliationBatchId = batchId; rollout.Reason = reason.Trim(); batch.State = HrMigrationBatchState.Activated;
        await db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> RollbackAsync(string module, Guid actorUserId, string reason, CancellationToken ct)
    {
        module = NormalizeModule(module); var rollout = await db.HrModuleRollouts.SingleOrDefaultAsync(item => item.Module == module, ct);
        if (rollout?.State != HrModuleRolloutState.NewActive) return ApiResponse<bool>.Fail("الوحدة ليست فعالة", ["ROLLOUT_NOT_ACTIVE"]);
        rollout.State = HrModuleRolloutState.RollingBack; rollout.ReadTarget = "legacy"; rollout.WriteTarget = "legacy"; rollout.ChangedByUserId = actorUserId; rollout.ChangedAt = DateTime.UtcNow; rollout.Reason = reason.Trim();
        await db.SaveChangesAsync(ct); rollout.State = HrModuleRolloutState.Legacy; if (rollout.ReconciliationBatchId.HasValue)
        { var batch = await db.HrMigrationBatches.SingleAsync(item => item.Id == rollout.ReconciliationBatchId, ct); batch.State = HrMigrationBatchState.RolledBack; }
        await db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    private static string NormalizeModule(string module)
    {
        module = module.Trim().ToLowerInvariant(); if (!ModuleOrder.Contains(module)) throw new ArgumentOutOfRangeException(nameof(module), "Unknown HR module"); return module;
    }
    private static string HashRows(IEnumerable<HrMigrationRow> rows)
    {
        var canonical = string.Join('\n', rows.OrderBy(item => item.SourceType).ThenBy(item => item.SourceId).Select(item => $"{item.SourceType}|{item.SourceId}|{item.TargetId:N}|{item.Amount:0.00}|{item.SourceHash}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
