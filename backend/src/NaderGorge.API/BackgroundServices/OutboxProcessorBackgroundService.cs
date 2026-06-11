using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Hubs;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;

namespace NaderGorge.API.BackgroundServices;

public class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<PlatformHub> _hubContext;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHubContext<PlatformHub> hubContext,
        ILogger<OutboxProcessorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing outbox events.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        _logger.LogInformation("OutboxProcessorBackgroundService stopped.");
    }

    private async Task ProcessOutboxEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        if (db is not DbContext dbContext)
        {
            _logger.LogError("AppDbContext is not a DbContext instance.");
            return;
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var events = await dbContext.Set<OutboxEvent>()
                .FromSqlRaw("SELECT * FROM outbox_events WHERE \"ProcessedAt\" IS NULL AND \"IsDeadLetter\" = FALSE AND \"RetryCount\" < 5 ORDER BY \"CreatedAt\" LIMIT 50 FOR UPDATE SKIP LOCKED")
                .ToListAsync(cancellationToken);

            if (!events.Any())
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var now = DateTime.UtcNow;
            var filteredEvents = events.Where(e =>
            {
                if (e.RetryCount == 0) return true;
                var lastAttempt = e.UpdatedAt ?? e.CreatedAt;
                var delaySeconds = Math.Pow(2, e.RetryCount) * 5; // 10s, 20s, 40s, 80s
                return (now - lastAttempt).TotalSeconds >= delaySeconds;
            }).ToList();

            if (!filteredEvents.Any())
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            _logger.LogInformation("Processing {Count} outbox events after filtering.", filteredEvents.Count);

            foreach (var @event in filteredEvents)
            {
                try
                {
                    if (!string.IsNullOrEmpty(@event.TargetUserId))
                    {
                        await _hubContext.Clients.Group($"User_{@event.TargetUserId}")
                            .SendAsync(@event.Type, @event.PayloadJson, cancellationToken);
                    }
                    else if (!string.IsNullOrEmpty(@event.TargetGroup))
                    {
                        if (@event.TargetGroup.Equals("Public", StringComparison.OrdinalIgnoreCase) || 
                            @event.TargetGroup.Equals("All", StringComparison.OrdinalIgnoreCase))
                        {
                            await _hubContext.Clients.All
                                .SendAsync(@event.Type, @event.PayloadJson, cancellationToken);
                        }
                        else
                        {
                            await _hubContext.Clients.Group(@event.TargetGroup)
                                .SendAsync(@event.Type, @event.PayloadJson, cancellationToken);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Outbox event {Id} has no target specified. Skipping broadcast to prevent unauthorized leak.", @event.Id);
                    }

                    @event.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch outbox event {Id} of type {Type}.", @event.Id, @event.Type);
                    @event.RetryCount++;
                    @event.UpdatedAt = DateTime.UtcNow;
                    @event.LastError = ex.Message + "\n" + ex.StackTrace;

                    if (@event.RetryCount >= 5)
                    {
                        @event.IsDeadLetter = true;
                        _logger.LogCritical("Outbox event {Id} of type {Type} has failed 5 times and is now marked as Dead Letter.", @event.Id, @event.Type);
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error occurred processing outbox events; transaction rolled back.");
            throw;
        }
    }
}
