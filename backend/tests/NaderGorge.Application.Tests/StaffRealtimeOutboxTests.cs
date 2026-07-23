using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Domain.Events;

namespace NaderGorge.Application.Tests;

public class StaffRealtimeOutboxTests
{
    [Fact]
    public void DataChangedEvent_RejectsUnknownScopeOrOperation()
    {
        var value = new DataChangedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Scopes = ["hr", "not-allowlisted"],
            Operation = "purged"
        };

        Assert.False(value.IsValid());
        Assert.True(DataChangedScopes.IsAllowed("hr"));
        Assert.False(DataChangedScopes.IsAllowed("employee-payload"));
        Assert.True(DataChangedOperations.IsAllowed(DataChangedOperations.Updated));
    }

    [Fact]
    public async Task SavingStaffVisibleEntity_EnqueuesScopedStaffEvent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.Subjects.Add(new Subject
        {
            Name = "Physics",
            NormalizedName = "PHYSICS",
            Description = "Physics subject"
        });

        await db.SaveChangesAsync();

        var staffEvent = await db.OutboxEvents.SingleAsync();
        Assert.Equal("StaffDataChanged", staffEvent.Type);
        Assert.Equal("Role_Staff", staffEvent.TargetGroup);

        using var payload = JsonDocument.Parse(staffEvent.PayloadJson);
        Assert.Equal("2", payload.RootElement.GetProperty("schemaVersion").GetString());
        Assert.True(Guid.TryParse(payload.RootElement.GetProperty("eventId").GetString(), out _));
        Assert.True(DateTimeOffset.TryParse(payload.RootElement.GetProperty("occurredAt").GetString(), out _));
        Assert.Equal("created", payload.RootElement.GetProperty("operation").GetString());
        Assert.Equal("Subject", payload.RootElement.GetProperty("entityType").GetString());
        Assert.Single(payload.RootElement.GetProperty("entityIds").EnumerateArray());
        var scopes = payload.RootElement.GetProperty("scopes")
            .EnumerateArray()
            .Select(scope => scope.GetString()!)
            .ToArray();

        Assert.Equal(["content", "subjects"], scopes);
    }

    [Fact]
    public async Task SavingMultipleStaffEntities_EmitsOneDeduplicatedScopeEnvelope()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.Subjects.AddRange(
            new Subject { Name = "Math", NormalizedName = "MATH", Description = "Math" },
            new Subject { Name = "Chemistry", NormalizedName = "CHEMISTRY", Description = "Chemistry" });

        await db.SaveChangesAsync();

        var staffEvents = await db.OutboxEvents.Where(item => item.Type == "StaffDataChanged").ToListAsync();
        Assert.Single(staffEvents);
        using var payload = JsonDocument.Parse(staffEvents[0].PayloadJson);
        var scopes = payload.RootElement.GetProperty("scopes").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Equal(["content", "subjects"], scopes);
        Assert.Equal("created", payload.RootElement.GetProperty("operation").GetString());
        Assert.Equal(2, payload.RootElement.GetProperty("entityIds").GetArrayLength());
    }

    [Fact]
    public async Task SavingDifferentStaffEntityTypes_EmitsOneBulkEnvelopeWithoutThrowing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.Subjects.Add(new Subject
        {
            Name = "Biology",
            NormalizedName = "BIOLOGY",
            Description = "Biology"
        });
        db.PlatformSettings.Add(new PlatformSetting
        {
            Key = "staff-realtime-regression",
            Value = "enabled"
        });

        var exception = await Record.ExceptionAsync(() => db.SaveChangesAsync());

        Assert.Null(exception);
        var staffEvents = await db.OutboxEvents.Where(item => item.Type == "StaffDataChanged").ToListAsync();
        Assert.Single(staffEvents);
        using var payload = JsonDocument.Parse(staffEvents[0].PayloadJson);
        Assert.Equal("bulk", payload.RootElement.GetProperty("operation").GetString());
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("entityType").ValueKind);
        Assert.Equal(2, payload.RootElement.GetProperty("entityIds").GetArrayLength());
        var scopes = payload.RootElement.GetProperty("scopes").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Equal(["content", "settings", "subjects"], scopes);
    }

    [Fact]
    public async Task SavingTelemetryEntity_DoesNotEnqueueStaffEvent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.WebVitalsMetrics.Add(new WebVitalsMetric
        {
            MetricName = "LCP",
            Value = 1200,
            Rating = "good",
            PageUrl = "/student",
            UserAgent = "test"
        });

        await db.SaveChangesAsync();

        Assert.Empty(await db.OutboxEvents.ToListAsync());
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
