using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class StaffRealtimeOutboxTests
{
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
        var scopes = payload.RootElement.GetProperty("scopes")
            .EnumerateArray()
            .Select(scope => scope.GetString()!)
            .ToArray();

        Assert.Equal(["content", "subjects"], scopes);
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
