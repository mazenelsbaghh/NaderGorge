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

    }
}
