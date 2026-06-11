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

        var events = await db.OutboxEvents
            .Where(e => e.ProcessedAt == null && e.RetryCount < 5)
            .OrderBy(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (!events.Any())
        {
            return;
        }

        _logger.LogInformation("Processing {Count} outbox events.", events.Count);

        foreach (var @event in events)
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
                    await _hubContext.Clients.Group(@event.TargetGroup)
                        .SendAsync(@event.Type, @event.PayloadJson, cancellationToken);
                }
                else
                {
                    // Default to sending to everyone if no target user/group is specified
                    await _hubContext.Clients.All
                        .SendAsync(@event.Type, @event.PayloadJson, cancellationToken);
                }

                @event.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch outbox event {Id} of type {Type}.", @event.Id, @event.Type);
                @event.RetryCount++;
                @event.LastError = ex.Message + "\n" + ex.StackTrace;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
