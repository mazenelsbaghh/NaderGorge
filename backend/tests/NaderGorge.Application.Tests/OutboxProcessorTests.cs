using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.BackgroundServices;
using NaderGorge.API.Hubs;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using Xunit;

namespace NaderGorge.Application.Tests;

public class SqliteOutboxCommandInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ReplacePostgresSql(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ReplacePostgresSql(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void ReplacePostgresSql(DbCommand command)
    {
        if (command.CommandText.Contains("FOR UPDATE SKIP LOCKED", StringComparison.OrdinalIgnoreCase))
        {
            // Modify postgres-specific SQL for SQLite compatibility
            command.CommandText = command.CommandText
                .Replace("FOR UPDATE SKIP LOCKED", "")
                .Replace("\"IsDeadLetter\" = FALSE", "\"IsDeadLetter\" = 0")
                .Replace("\"IsDeadLetter\" = false", "\"IsDeadLetter\" = 0")
                .Replace("\"IsDeadLetter\" = False", "\"IsDeadLetter\" = 0");
        }
    }
}

public class FakeHubContext : IHubContext<PlatformHub>
{
    public FakeHubClients FakeClients { get; } = new();
    public IHubClients Clients => FakeClients;
    public IGroupManager Groups => throw new NotImplementedException();
}

public class FakeHubClients : IHubClients
{
    public FakeClientProxy AllProxy { get; } = new();
    public IClientProxy All => AllProxy;

    public Dictionary<string, FakeClientProxy> FakeGroups { get; } = new Dictionary<string, FakeClientProxy>();

    public IClientProxy Group(string groupName)
    {
        if (!FakeGroups.TryGetValue(groupName, out var proxy))
        {
            proxy = new FakeClientProxy();
            FakeGroups[groupName] = proxy;
        }
        return proxy;
    }

    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
    public IClientProxy Caller => throw new NotImplementedException();
    public IClientProxy Others => throw new NotImplementedException();
    public IClientProxy Client(string connectionId) => throw new NotImplementedException();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
    public IClientProxy OthersInGroup(string groupName) => throw new NotImplementedException();
    public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
    public IClientProxy User(string userId) => throw new NotImplementedException();
}

public class FakeClientProxy : IClientProxy
{
    public List<(string Method, object?[] Args)> SentMessages { get; } = new List<(string Method, object?[] Args)>();
    public bool ShouldThrow { get; set; }

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        if (ShouldThrow)
        {
            throw new Exception("SignalR connection lost");
        }
        SentMessages.Add((method, args));
        return Task.CompletedTask;
    }
}

public class FakeServiceScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
{
    private readonly IAppDbContext _db;

    public FakeServiceScopeFactory(IAppDbContext db)
    {
        _db = db;
    }

    public IServiceScope CreateScope() => this;
    public IServiceProvider ServiceProvider => this;

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IAppDbContext))
        {
            return _db;
        }
        return null;
    }

    public void Dispose() { }
}

public class OutboxProcessorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FakeHubContext _hubContext;
    private readonly FakeServiceScopeFactory _scopeFactory;
    private readonly OutboxProcessorBackgroundService _processor;

    public OutboxProcessorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new SqliteOutboxCommandInterceptor())
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _hubContext = new FakeHubContext();
        _scopeFactory = new FakeServiceScopeFactory(_db);
        _processor = new OutboxProcessorBackgroundService(
            _scopeFactory,
            _hubContext,
            NullLogger<OutboxProcessorBackgroundService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    private async Task InvokeProcessOutboxEventsAsync()
    {
        var method = typeof(OutboxProcessorBackgroundService)
            .GetMethod("ProcessOutboxEventsAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (method == null)
        {
            throw new Exception("Method ProcessOutboxEventsAsync not found.");
        }

        var task = (Task)method.Invoke(_processor, new object[] { CancellationToken.None })!;
        await task;
    }

    [Fact]
    public async Task ProcessEvents_SuccessPath_ShouldDispatchAndMarkProcessed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = "TestNotification",
            TargetUserId = userId.ToString(),
            PayloadJson = "{\"message\":\"hello\"}",
            CreatedAt = DateTime.UtcNow
        };
        _db.OutboxEvents.Add(@event);
        await _db.SaveChangesAsync();

        // Act
        await InvokeProcessOutboxEventsAsync();

        // Assert
        var updatedEvent = await _db.OutboxEvents.FindAsync(@event.Id);
        Assert.NotNull(updatedEvent);
        Assert.NotNull(updatedEvent.ProcessedAt);
        Assert.False(updatedEvent.IsDeadLetter);
        Assert.Equal(0, updatedEvent.RetryCount);

        var groupName = $"User_{userId}";
        Assert.True(_hubContext.FakeClients.FakeGroups.ContainsKey(groupName));
        var messages = ((FakeClientProxy)_hubContext.FakeClients.Group(groupName)).SentMessages;
        Assert.Single(messages);
        Assert.Equal("TestNotification", messages[0].Method);
        Assert.Equal("{\"message\":\"hello\"}", messages[0].Args[0]);
    }

    [Fact]
    public async Task ProcessEvents_NoTarget_ShouldSkipWithoutProcessing()
    {
        // Arrange
        var @event = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = "TestNotification",
            PayloadJson = "{\"message\":\"hello\"}",
            CreatedAt = DateTime.UtcNow
        };
        _db.OutboxEvents.Add(@event);
        await _db.SaveChangesAsync();

        // Act
        await InvokeProcessOutboxEventsAsync();

        // Assert
        var updatedEvent = await _db.OutboxEvents.FindAsync(@event.Id);
        Assert.NotNull(updatedEvent);
        Assert.NotNull(updatedEvent.ProcessedAt); // Marked processed to avoid blocking outbox
        Assert.False(updatedEvent.IsDeadLetter);
        Assert.Equal(0, updatedEvent.RetryCount); // No failure/retry incremented
        Assert.Empty(_hubContext.FakeClients.FakeGroups);
        Assert.Empty(((FakeClientProxy)_hubContext.FakeClients.All).SentMessages);
    }

    [Fact]
    public async Task ProcessEvents_Failure_ShouldIncrementRetryCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = "TestNotification",
            TargetUserId = userId.ToString(),
            PayloadJson = "{\"message\":\"hello\"}",
            CreatedAt = DateTime.UtcNow
        };
        _db.OutboxEvents.Add(@event);
        await _db.SaveChangesAsync();

        // Configure fake to throw exception
        var groupName = $"User_{userId}";
        var groupProxy = (FakeClientProxy)_hubContext.FakeClients.Group(groupName);
        groupProxy.ShouldThrow = true;

        // Act
        await InvokeProcessOutboxEventsAsync();

        // Assert
        var updatedEvent = await _db.OutboxEvents.FindAsync(@event.Id);
        Assert.NotNull(updatedEvent);
        Assert.Null(updatedEvent.ProcessedAt);
        Assert.Equal(1, updatedEvent.RetryCount);
        Assert.False(updatedEvent.IsDeadLetter);
        Assert.Contains("SignalR connection lost", updatedEvent.LastError);
    }

    [Fact]
    public async Task ProcessEvents_DeadLetterOnFifthFailure_ShouldMarkIsDeadLetter()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = "TestNotification",
            TargetUserId = userId.ToString(),
            PayloadJson = "{\"message\":\"hello\"}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            RetryCount = 4, // 5th attempt
            UpdatedAt = DateTime.UtcNow.AddMinutes(-20)
        };
        _db.OutboxEvents.Add(@event);
        await _db.SaveChangesAsync();

        var groupName = $"User_{userId}";
        var groupProxy = (FakeClientProxy)_hubContext.FakeClients.Group(groupName);
        groupProxy.ShouldThrow = true;

        // Act
        await InvokeProcessOutboxEventsAsync();

        // Assert
        var updatedEvent = await _db.OutboxEvents.FindAsync(@event.Id);
        Assert.NotNull(updatedEvent);
        Assert.Null(updatedEvent.ProcessedAt);
        Assert.Equal(5, updatedEvent.RetryCount);
        Assert.True(updatedEvent.IsDeadLetter);
    }

    [Fact]
    public async Task ProcessEvents_ExponentialBackoff_ShouldSkipRecentRetries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        // RetryCount = 1 requires delay = 2^1 * 5 = 10 seconds.
        // If we set UpdatedAt to 2 seconds ago, it should be skipped.
        var @event = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = "TestNotification",
            TargetUserId = userId.ToString(),
            PayloadJson = "{\"message\":\"hello\"}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            RetryCount = 1,
            UpdatedAt = DateTime.UtcNow.AddSeconds(-2)
        };
        _db.OutboxEvents.Add(@event);
        await _db.SaveChangesAsync();

        // Act
        await InvokeProcessOutboxEventsAsync();

        // Assert
        var updatedEvent = await _db.OutboxEvents.FindAsync(@event.Id);
        Assert.NotNull(updatedEvent);
        Assert.Null(updatedEvent.ProcessedAt);
        Assert.Equal(1, updatedEvent.RetryCount); // Not incremented, skipped processing
    }
}
