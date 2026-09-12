using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.BackgroundServices;

public sealed class LiveSupportBlockBackgroundService(IServiceScopeFactory scopes, ILogger<LiveSupportBlockBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var stale = DateTime.UtcNow.AddMinutes(-5);
                await db.LiveSupportBlockDeliveries.Where(item => item.Status == "Processing" && item.ClaimedAt < stale)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, "Failed")
                        .SetProperty(item => item.FailureCode, "WHATSAPP_BLOCK_UNCERTAIN")
                        .SetProperty(item => item.Version, item => item.Version + 1), stoppingToken);
                var ids = await db.LiveSupportBlockDeliveries.AsNoTracking().Where(item => item.Status == "Pending")
                    .OrderBy(item => item.CreatedAt).Select(item => item.Id).Take(20).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    using var deliveryScope = scopes.CreateScope();
                    await deliveryScope.ServiceProvider.GetRequiredService<LiveSupportBlockDispatcher>().DispatchAsync(id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Support block dispatch failed; durable requests remain available for recovery."); }
        }
    }
}
