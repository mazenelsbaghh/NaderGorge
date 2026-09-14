using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.AutoRepair;
using NaderGorge.API.Controllers;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using Npgsql;
using StackExchange.Redis;
using System.Text.Json;

namespace NaderGorge.Application.Tests;

public sealed class AutoRepairPostgresFactAttribute : FactAttribute
{
    public AutoRepairPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AUTO_REPAIR_TEST_DB") is null)
            Skip = "Requires isolated AUTO_REPAIR_TEST_DB and AUTO_REPAIR_TEST_REDIS.";

    }
}

public sealed class AutoRepairPostgresTests
{
    static AutoRepairPostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    [AutoRepairPostgresFact]
    public async Task Deduplication_lease_recovery_and_hash_bound_approval_use_durable_state()
    {
        var connection = Environment.GetEnvironmentVariable("AUTO_REPAIR_TEST_DB")!;
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("repair_test", parsed.Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1" });
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("AUTO_REPAIR_TEST_REDIS")!);
        await redis.GetDatabase().KeyDeleteAsync("system:logs:v1");
        var store = new RepairStore(db, redis);
        var controller = new AutoRepairRunnerController(db, store);
        var controls = await db.AutoRepairControls.SingleAsync();
        controls.Paused = false;
        controls.AutoDeploy = true;
        await db.SaveChangesAsync();
        var log = JsonSerializer.Serialize(new { id = Guid.NewGuid(), timestamp = DateTimeOffset.UtcNow,
            source = "backend", category = "RepairFixture", level = "error", message = "Null record 123", exception = "" });
        await redis.GetDatabase().ListRightPushAsync("system:logs:v1", new RedisValue[] { log, log });
        Assert.IsType<OkObjectResult>(await controller.Claim(default));
        Assert.All(await db.AutoRepairIncidents.ToListAsync(), x => Assert.Equal(0, x.Attempts));
        var synchronized = new RepairSynchronization.Snapshot("ready", new string('a', 40),
            [new("node-1", "git-" + new string('b', 40)), new("node-2", "git-" + new string('b', 40)), new("node-3", "git-" + new string('b', 40))]);
        Assert.IsType<BadRequestResult>(await controller.Synchronization(synchronized with { Nodes = [synchronized.Nodes[0], synchronized.Nodes[0], synchronized.Nodes[0]] }, default));
        Assert.Null(await RepairSynchronization.Latest(db, default));
        Assert.IsType<OkObjectResult>(await controller.Synchronization(synchronized, default));
        await using var otherDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        var competing = new AutoRepairRunnerController(otherDb, new RepairStore(otherDb, redis));
        var claims = await Task.WhenAll(controller.Claim(default), competing.Claim(default));
        var assigned = claims.Select(x => JsonSerializer.Serialize(((OkObjectResult)x).Value)).Count(x => !x.Contains("\"incident\":null"));
        Assert.Equal(1, assigned);
        db.ChangeTracker.Clear();
        var incident = await db.AutoRepairIncidents.SingleAsync();
        Assert.Equal(1, incident.Occurrences);
        Assert.Equal("diagnosing", incident.Status);
        var token = incident.LeaseToken!.Value;
        Assert.IsType<ConflictResult>(await controller.Report(incident.Id, new(token, "completed", "cannot skip", null, null), default));
        await controller.Claim(default);
        Assert.Equal(1, await db.AutoRepairIncidents.CountAsync());
        await controller.Report(incident.Id, new(token, "repairing", "repair", null, null), default);
        await controller.Report(incident.Id, new(token, "testing", "tested", null, null), default);
        var hash = new string('a', 64);
        await controller.Report(incident.Id, new(token, "awaiting_approval", "finance change", hash, null), default);
        await controller.Claim(default);
        db.ChangeTracker.Clear();
        incident = await db.AutoRepairIncidents.SingleAsync();
        Assert.Null(incident.LeaseToken);
        var admin = new AdminAutoRepairController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<ConflictObjectResult>(await admin.Decision(incident.Id, new("approve", new string('b', 64), "اعتماد bbbbbbbbbbbb"), default));
        Assert.IsType<OkObjectResult>(await admin.Decision(incident.Id, new("approve", hash, "اعتماد aaaaaaaaaaaa"), default));
        await controller.Claim(default);
        db.ChangeTracker.Clear();
        incident = await db.AutoRepairIncidents.SingleAsync();
        Assert.Equal("ready", incident.Status);
        Assert.NotNull(incident.LeaseToken);
        incident.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await controller.Claim(default);
        db.ChangeTracker.Clear();
        Assert.True((await db.AutoRepairControls.SingleAsync()).Paused);
        Assert.Equal("failed", (await db.AutoRepairIncidents.SingleAsync()).Status);
        Assert.True(await db.AutoRepairEvents.CountAsync() >= 7);
        var evidence = "Unhandled exception. CorrelationId: e7ce5f9bb4a2497da9584303ccb815c8";
        var legacy = new AutoRepairIncident { Source = "backend", Category = "Legacy", Evidence = evidence, Fingerprint = new string('1', 64), Occurrences = 2 };
        var duplicate = new AutoRepairIncident { Source = "backend", Category = "Legacy", Evidence = evidence.Replace("e7ce", "a7ce"), Fingerprint = new string('2', 64), Occurrences = 3 };
        db.AutoRepairIncidents.AddRange(legacy, duplicate);
        await db.SaveChangesAsync();
        await controller.Claim(default);
        db.ChangeTracker.Clear();
        var preserved = await db.AutoRepairIncidents.Where(x => x.Category == "Legacy").ToListAsync();
        Assert.Equal(2, preserved.Count);
        Assert.Equal(5, preserved.Single(x => x.Status == "queued").Occurrences);
        Assert.Single(preserved, x => x.Status == "duplicate");
        await controller.Claim(default);
        db.ChangeTracker.Clear();
        Assert.Equal(5, (await db.AutoRepairIncidents.SingleAsync(x => x.Category == "Legacy" && x.Status == "queued")).Occurrences);

        controls = await db.AutoRepairControls.SingleAsync();
        controls.Paused = false;
        await db.SaveChangesAsync();
        var attempts = await db.AutoRepairIncidents.SumAsync(x => x.Attempts);
        await controller.Synchronization(synchronized with { State = "pending_release" }, default);
        await controller.Claim(default);
        Assert.Equal(attempts, await db.AutoRepairIncidents.SumAsync(x => x.Attempts));
        await using var reloaded = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        Assert.Equal("pending_release", (await RepairSynchronization.Latest(reloaded, default))!.Snapshot.State);
        Assert.NotNull(await RepairSynchronization.Latest(reloaded, default, "ready"));
        await controller.Synchronization(synchronized, default);
        var observation = await db.AutoRepairEvents.Where(x => x.Actor == "synchronization").OrderByDescending(x => x.Id).FirstAsync();
        observation.Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4);
        await db.SaveChangesAsync();
        await controller.Claim(default);
        Assert.Equal(attempts, await db.AutoRepairIncidents.SumAsync(x => x.Attempts));
    }

    [AutoRepairPostgresFact]
    public async Task Archived_incidents_stay_out_of_work_and_cannot_interrupt_an_active_lease()
    {
        var connection = Environment.GetEnvironmentVariable("AUTO_REPAIR_TEST_DB")!;
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("repair_test", parsed.Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1" });
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("AUTO_REPAIR_TEST_REDIS")!);
        var admin = new AdminAutoRepairController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var store = new RepairStore(db, redis);
        var stamp = DateTimeOffset.UtcNow.AddMinutes(-1);
        var log = new RepairStore.RepairLog(Guid.NewGuid(), stamp, "gateway", "ArchiveFixture", "error", "fixture failure", null);
        await store.IngestExternal([log], default);
        var incident = await db.AutoRepairIncidents.SingleAsync();
        incident.LeaseToken = Guid.NewGuid();
        incident.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(3);
        await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await admin.Decision(incident.Id, new("dismiss", null, null, "حالة معروفة لا تحتاج إصلاحًا"), default));
        incident.LeaseToken = null;
        incident.LeaseUntil = null;
        await db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await admin.Decision(incident.Id, new("dismiss", null, null, ""), default));
        Assert.IsType<OkObjectResult>(await admin.Decision(incident.Id, new("dismiss", null, null, "حالة معروفة لا تحتاج إصلاحًا"), default));
        await store.IngestExternal([log with { Id = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow }], default);
        Assert.Equal("dismissed", incident.Status);
        Assert.Equal(2, incident.Occurrences);
        static JsonElement Data(IActionResult result) => JsonSerializer.SerializeToElement(((OkObjectResult)result).Value).GetProperty("Data");
        Assert.Empty(Data(await admin.List(null, 1, default)).GetProperty("incidents").EnumerateArray());
        Assert.Single(Data(await admin.List("archive", 1, default)).GetProperty("incidents").EnumerateArray());
        Assert.True(await db.AutoRepairEvents.AnyAsync(x => x.IncidentId == incident.Id && x.Status == "dismissed"));
        Assert.IsType<OkObjectResult>(await admin.Decision(incident.Id, new("retry", null, null), default));
        Assert.Equal("queued", incident.Status);
        Assert.Single(Data(await admin.List(null, 1, default)).GetProperty("incidents").EnumerateArray());

        incident.Status = "completed";
        var completedAt = DateTimeOffset.UtcNow.AddSeconds(-20);
        incident.Events.Add(new AutoRepairEvent { IncidentId = incident.Id, Status = "completed", Timestamp = completedAt, Detail = "Verified", Actor = "node-3" });
        await db.SaveChangesAsync();
        await store.IngestExternal([log with { Id = Guid.NewGuid(), Timestamp = completedAt.AddSeconds(-1) }], default);
        Assert.Equal("completed", incident.Status);
        Assert.Empty(Data(await admin.List(null, 1, default)).GetProperty("incidents").EnumerateArray());
        await store.IngestExternal([log with { Id = Guid.NewGuid(), Timestamp = completedAt.AddSeconds(1) }], default);
        Assert.Equal("queued", incident.Status);
    }

    [AutoRepairPostgresFact]
    public async Task Inconclusive_diagnosis_waits_for_new_evidence_without_reclaiming_repeated_logs()
    {
        var connection = Environment.GetEnvironmentVariable("AUTO_REPAIR_TEST_DB")!;
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("repair_test", parsed.Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1" });
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("AUTO_REPAIR_TEST_REDIS")!);
        await redis.GetDatabase().KeyDeleteAsync("system:logs:v1");
        var store = new RepairStore(db, redis);
        var runner = new AutoRepairRunnerController(db, store);
        var admin = new AdminAutoRepairController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var control = await db.AutoRepairControls.SingleAsync();
        control.Paused = false;
        await db.SaveChangesAsync();
        await runner.Synchronization(new("ready", new string('a', 40),
            [new("node-1", "git-" + new string('b', 40)), new("node-2", "git-" + new string('b', 40)), new("node-3", "git-" + new string('b', 40))]), default);
        var log = new RepairStore.RepairLog(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1),
            "gateway", "NeedsEvidenceFixture", "warning", "Slow request without query or lock timing", null);
        await store.IngestExternal([log], default);
        await runner.Claim(default);
        var incident = await db.AutoRepairIncidents.SingleAsync();
        Assert.NotNull(incident.LeaseToken);
        var lease = incident.LeaseToken!.Value;
        Assert.IsType<ConflictObjectResult>(await admin.Decision(incident.Id,
            new("supply_evidence", null, null, Evidence: "A new lock timing measurement"), default));
        Assert.IsType<OkObjectResult>(await runner.Report(incident.Id,
            new(lease, "repairing", "Investigation", null, null), default));
        const string reason = "Cannot reproduce latency; need query timings and PostgreSQL lock wait evidence";
        Assert.IsType<OkObjectResult>(await runner.Report(incident.Id,
            new(lease, "needs_evidence", reason, null, null), default));
        db.ChangeTracker.Clear();
        incident = await db.AutoRepairIncidents.SingleAsync();
        Assert.Equal("needs_evidence", incident.Status);
        Assert.Equal(reason, incident.Summary);
        Assert.Null(incident.LeaseToken);
        Assert.Null(incident.LeaseUntil);
        Assert.Empty(incident.ProposalHash);
        Assert.Empty(incident.ApprovedHash);
        await store.IngestExternal([log with { Id = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow }], default);
        await runner.Claim(default);
        Assert.Equal("needs_evidence", incident.Status);
        Assert.Equal(1, incident.Attempts);
        Assert.Equal(2, incident.Occurrences);
        Assert.IsType<ConflictObjectResult>(await admin.Decision(incident.Id, new("retry", null, null), default));
        Assert.IsType<BadRequestObjectResult>(await admin.Decision(incident.Id,
            new("supply_evidence", null, null, Evidence: log.Message), default));
        Assert.IsType<BadRequestObjectResult>(await admin.Decision(incident.Id,
            new("supply_evidence", null, null, Evidence: "short"), default));
        Assert.IsType<BadRequestObjectResult>(await admin.Decision(incident.Id,
            new("supply_evidence", null, null, Evidence: new string('x', 3001)), default));
        const string extra = "Isolated reproduction: query 12ms, lock wait 630ms; token=fixture-secret";
        Assert.IsType<OkObjectResult>(await admin.Decision(incident.Id,
            new("supply_evidence", null, null, Evidence: extra), default));
        Assert.Equal("queued", incident.Status);
        Assert.Equal(0, incident.Attempts);
        Assert.Equal(log.Message, incident.Evidence.Trim());
        var claim = Assert.IsType<OkObjectResult>(await runner.Claim(default));
        var payload = JsonSerializer.Serialize(claim.Value);
        Assert.Contains("lock wait 630ms", payload);
        Assert.DoesNotContain("fixture-secret", payload);
        Assert.Equal(1, await db.AutoRepairIncidents.CountAsync());
        Assert.IsType<OkObjectResult>(await runner.Report(incident.Id,
            new(incident.LeaseToken!.Value, "needs_evidence", "Need a second independent measurement", null, null), default));
        Assert.IsType<BadRequestObjectResult>(await admin.Decision(incident.Id,
            new("supply_evidence", null, null, Evidence: extra), default));
        Assert.Equal("needs_evidence", incident.Status);
        Assert.Single(await db.AutoRepairEvents.Where(x => x.IncidentId == incident.Id && x.Status == "evidence").ToListAsync());
        Assert.True(await db.AutoRepairEvents.AnyAsync(x => x.IncidentId == incident.Id && x.Detail == reason));
    }
}
