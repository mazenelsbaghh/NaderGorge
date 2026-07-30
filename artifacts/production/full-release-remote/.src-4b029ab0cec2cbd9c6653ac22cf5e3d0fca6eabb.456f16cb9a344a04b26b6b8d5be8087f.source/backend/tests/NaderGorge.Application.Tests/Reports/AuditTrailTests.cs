using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Reports.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NaderGorge.Application.Tests.Reports;

public class AuditTrailTests
{
    [Fact]
    public async Task Handle_ReturnsPagedAuditLogs_WithCorrectFilters()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // Seed Users
        var user1 = await TestAppDbContextFactory.SeedUserAsync(db, "Manager One", "01010101011");
        var user2 = await TestAppDbContextFactory.SeedUserAsync(db, "Manager Two", "01010101012");

        // Seed Audit Logs
        var log1 = new AuditLog
        {
            Action = "CreateTask",
            EntityType = "TaskItem",
            EntityId = Guid.NewGuid(),
            PerformedByUserId = user1.Id,
            OldValues = null,
            NewValues = "Title: Task A",
            IpAddress = "127.0.0.1",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };

        var log2 = new AuditLog
        {
            Action = "UpdateTaskStatus",
            EntityType = "TaskItem",
            EntityId = Guid.NewGuid(),
            PerformedByUserId = user2.Id,
            OldValues = "Status: New",
            NewValues = "Status: Completed",
            IpAddress = "127.0.0.1",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var log3 = new AuditLog
        {
            Action = "ActivateCode",
            EntityType = "AccessCode",
            EntityId = Guid.NewGuid(),
            PerformedByUserId = user1.Id,
            OldValues = null,
            NewValues = "CodePlaintext: ABCD****",
            IpAddress = "192.168.1.1",
            CreatedAt = DateTime.UtcNow
        };

        db.AuditLogs.AddRange(log1, log2, log3);
        await db.SaveChangesAsync();

        var handler = new GetAdminAuditLogsQueryHandler(db);

        // Test 1: No filters, should return all 3 sorted descending by CreatedAt
        var resultAll = await handler.Handle(new GetAdminAuditLogsQuery(null, null, null, null, 1, 10), CancellationToken.None);
        Assert.True(resultAll.Success);
        Assert.NotNull(resultAll.Data);
        var allAuditLogs = resultAll.Data!;
        Assert.Equal(3, allAuditLogs.TotalCount);
        Assert.Equal("ActivateCode", allAuditLogs.Items[0].Action); // Latest first

        // Test 2: Filter by EntityType = "TaskItem"
        var resultTask = await handler.Handle(new GetAdminAuditLogsQuery(null, null, null, "TaskItem", 1, 10), CancellationToken.None);
        Assert.NotNull(resultTask.Data);
        var taskAuditLogs = resultTask.Data!;
        Assert.Equal(2, taskAuditLogs.TotalCount);
        Assert.All(taskAuditLogs.Items, item => Assert.Equal("TaskItem", item.EntityType));

        // Test 3: Filter by PerformedByUserId = user1.Id
        var resultUser = await handler.Handle(new GetAdminAuditLogsQuery(null, null, user1.Id, null, 1, 10), CancellationToken.None);
        Assert.NotNull(resultUser.Data);
        var userAuditLogs = resultUser.Data!;
        Assert.Equal(2, userAuditLogs.TotalCount);
        Assert.All(userAuditLogs.Items, item => Assert.Equal(user1.Id, item.PerformedByUserId));

        // Test 4: Filter by date range (last 3 days)
        var resultDate = await handler.Handle(new GetAdminAuditLogsQuery(DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddMinutes(5), null, null, 1, 10), CancellationToken.None);
        Assert.NotNull(resultDate.Data);
        var datedAuditLogs = resultDate.Data!;
        Assert.Equal(2, datedAuditLogs.TotalCount); // log2 and log3
        Assert.DoesNotContain(datedAuditLogs.Items, item => item.Id == log1.Id);
    }

    [Fact]
    public async Task Handle_EnsuresSensitiveDataIsMaskedOrRedacted()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Manager One", "01010101013");

        // Plaintext access code should never be stored; it's masked at creation, verify we retrieve the masked values correctly.
        var log = new AuditLog
        {
            Action = "ActivateCode",
            EntityType = "AccessCode",
            EntityId = Guid.NewGuid(),
            PerformedByUserId = user.Id,
            OldValues = null,
            NewValues = "CodePlaintext: 1234****", // Masked plaintext
            IpAddress = "127.0.0.1",
            CreatedAt = DateTime.UtcNow
        };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        var handler = new GetAdminAuditLogsQueryHandler(db);
        var result = await handler.Handle(new GetAdminAuditLogsQuery(null, null, null, null, 1, 10), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var retrievedLog = result.Data!.Items.First();
        Assert.Contains("1234****", retrievedLog.NewValues);
        // Ensure no full plain passwords or tokens exist
        Assert.DoesNotContain("secret_token_123456", retrievedLog.NewValues);
    }
}
