using NaderGorge.Application.Features.HR.Migration;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.HR;

public sealed class HrMigrationTests
{
    [Fact]
    public async Task DryRunReplayAndApplyAreIdempotentWithExactReconciliation()
    {
        await using var db = TestAppDbContextFactory.Create(); var service = new HrMigrationService(db); var actor = Guid.NewGuid();
        var rows = new[] { new HrMigrationRow("employee", "1", Guid.NewGuid(), 100, "a"), new HrMigrationRow("employee", "2", Guid.NewGuid(), 200, "b") };
        var first = await service.DryRunAsync("people", "legacy", rows, actor, default); var replay = await service.DryRunAsync("people", "legacy", rows, actor, default);
        Assert.Equal(first.Data, replay.Data); Assert.Single(db.HrMigrationBatches);
        Assert.True((await service.ApplyAndReconcileAsync(first.Data, rows, actor, default)).Success);
        var batch = db.HrMigrationBatches.Single(); Assert.Equal(batch.SourceCount, batch.TargetCount); Assert.Equal(batch.SourceTotal, batch.TargetTotal); Assert.Equal(HrMigrationBatchState.Reconciled, batch.State);
        Assert.True((await service.ApplyAndReconcileAsync(first.Data, rows, actor, default)).Success); Assert.Equal(2, db.HrMigrationRecordMaps.Count());
    }

    [Fact]
    public async Task ChangedReplayCreatesConflictAndBlocksActivation()
    {
        await using var db = TestAppDbContextFactory.Create(); var service = new HrMigrationService(db); var actor = Guid.NewGuid(); var target = Guid.NewGuid();
        var rows = new[] { new HrMigrationRow("employee", "1", target, 100, "a") }; var batch = await service.DryRunAsync("people", "legacy", rows, actor, default); await service.ApplyAndReconcileAsync(batch.Data, rows, actor, default);
        var changed = new[] { new HrMigrationRow("employee", "1", target, 120, "changed") }; var result = await service.ApplyAndReconcileAsync(batch.Data, changed, actor, default);
        Assert.False(result.Success); Assert.Single(db.HrMigrationConflicts); Assert.False((await service.ActivateAsync("people", batch.Data, actor, "conflict", default)).Success);
    }

    [Fact]
    public async Task ModulesActivateAndRollbackIndependentlyInRequiredOrder()
    {
        await using var db = TestAppDbContextFactory.Create(); var service = new HrMigrationService(db); var actor = Guid.NewGuid();
        var peopleBatch = await ReconciledBatch(service, "people", actor); var attendanceBatch = await ReconciledBatch(service, "attendance", actor);
        Assert.False((await service.ActivateAsync("attendance", attendanceBatch, actor, "too early", default)).Success);
        Assert.True((await service.ActivateAsync("people", peopleBatch, actor, "ready", default)).Success); Assert.True((await service.ActivateAsync("attendance", attendanceBatch, actor, "ready", default)).Success);
        Assert.True((await service.RollbackAsync("attendance", actor, "rollback rehearsal", default)).Success);
        Assert.Equal(HrModuleRolloutState.NewActive, db.HrModuleRollouts.Single(item => item.Module == "people").State); Assert.Equal(HrModuleRolloutState.Legacy, db.HrModuleRollouts.Single(item => item.Module == "attendance").State);
    }

    [Fact]
    public async Task EveryModuleCanDryRunReconcileActivateRollbackAndReactivateWithExactEvidence()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = new HrMigrationService(db);
        var actor = Guid.NewGuid();

        var modules = new[] { "people", "attendance", "leave", "payroll", "remaining" };
        for (var moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
        {
            var module = modules[moduleIndex];
            var firstTargetId = Guid.Parse($"00000000-0000-0000-0000-{moduleIndex + 1:000000000000}");
            var secondTargetId = Guid.Parse($"00000000-0000-0000-0000-{moduleIndex + 101:000000000000}");
            var rows = new[]
            {
                new HrMigrationRow($"{module}-record", "1", firstTargetId, 125.50m, $"{module}-hash-1"),
                new HrMigrationRow($"{module}-record", "2", secondTargetId, 74.50m, $"{module}-hash-2")
            };
            var dryRun = await service.DryRunAsync(module, "legacy-verification", rows, actor, default);
            Assert.True(dryRun.Success);
            Assert.True((await service.ApplyAndReconcileAsync(dryRun.Data, rows, actor, default)).Success);

            var batch = db.HrMigrationBatches.Single(item => item.Id == dryRun.Data);
            Assert.Equal(2, batch.SourceCount);
            Assert.Equal(batch.SourceCount, batch.TargetCount);
            Assert.Equal(200m, batch.SourceTotal);
            Assert.Equal(batch.SourceTotal, batch.TargetTotal);
            Assert.Equal(batch.SourceHash, batch.TargetHash);

            Assert.True((await service.ActivateAsync(module, batch.Id, actor, "verification activation", default)).Success);
            Assert.True((await service.RollbackAsync(module, actor, "verification rollback", default)).Success);
            Assert.True((await service.ActivateAsync(module, batch.Id, actor, "verification reactivation", default)).Success);
            Assert.Equal(HrModuleRolloutState.NewActive, db.HrModuleRollouts.Single(item => item.Module == module).State);
        }
    }

    private static async Task<Guid> ReconciledBatch(HrMigrationService service, string module, Guid actor)
    {
        var rows = new[] { new HrMigrationRow(module, "1", Guid.NewGuid(), 1, module) }; var batch = await service.DryRunAsync(module, "legacy", rows, actor, default); await service.ApplyAndReconcileAsync(batch.Data, rows, actor, default); return batch.Data;
    }
}
