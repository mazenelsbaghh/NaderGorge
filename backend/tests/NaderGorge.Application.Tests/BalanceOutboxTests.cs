using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NaderGorge.Application.Tests;

public class BalanceOutboxTests
{
    [Fact]
    public async Task AddCredit_ShouldEnqueueBalanceChangedOutboxEvent()
    {
        // Arrange
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Student", "01234567890");
        var balanceService = new BalanceService(db, NullLogger<BalanceService>.Instance);

        // Act
        var transaction = await balanceService.AddCredit(user.Id, 150m, "Test Credit");

        // Assert
        var outboxEvents = await db.OutboxEvents.ToListAsync();
        Assert.Single(outboxEvents);
        
        var @event = outboxEvents[0];
        Assert.Equal("BalanceChanged", @event.Type);
        Assert.Equal(user.Id.ToString(), @event.TargetUserId);
        Assert.Contains("150.00", @event.PayloadJson);
        Assert.Null(@event.ProcessedAt);
    }

    [Fact]
    public async Task DeductBalance_ShouldEnqueueBalanceChangedOutboxEvent()
    {
        // Arrange
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Student 2", "01234567891");
        var balanceService = new BalanceService(db, NullLogger<BalanceService>.Instance);
        await balanceService.AddCredit(user.Id, 200m, "Initial Credit");

        // Clear the outbox event created from AddCredit to isolate DeductBalance test
        db.OutboxEvents.RemoveRange(db.OutboxEvents);
        await db.SaveChangesAsync();

        // Act
        var transaction = await balanceService.DeductBalance(user.Id, 50m, "Test Debit");

        // Assert
        var outboxEvents = await db.OutboxEvents.ToListAsync();
        Assert.Single(outboxEvents);
        
        var @event = outboxEvents[0];
        Assert.Equal("BalanceChanged", @event.Type);
        Assert.Equal(user.Id.ToString(), @event.TargetUserId);
        Assert.Contains("150.00", @event.PayloadJson); // Balance after deduction is 150
        Assert.Null(@event.ProcessedAt);
    }
}
