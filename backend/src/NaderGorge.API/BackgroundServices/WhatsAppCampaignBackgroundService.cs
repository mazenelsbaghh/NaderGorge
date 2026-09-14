using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.BackgroundServices;

/// <summary>
/// Holds one renewable cluster lease while dispatching, so all application nodes
/// share a single conservative WABA-wide rate limit.
/// </summary>
public sealed class WhatsAppCampaignBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppCampaignBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StandbyRetry = TimeSpan.FromSeconds(2);
    private readonly Guid _ownerToken = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var acquired = await ClusterLeaseRunner.TryRunAsync(
                    scope.ServiceProvider,
                    "whatsapp-campaign-dispatch",
                    _ownerToken,
                    LeaseLifetime,
                    DispatchWhileLeaseHeldAsync,
                    stoppingToken);
                if (!acquired) await Task.Delay(StandbyRetry, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "WhatsApp campaign cluster dispatcher stopped unexpectedly.");
                await Task.Delay(StandbyRetry, stoppingToken);
            }
        }
    }

    private static async Task DispatchWhileLeaseHeldAsync(
        IServiceProvider services,
        CancellationToken leaseToken)
    {
        var dispatcher = services.GetRequiredService<WhatsAppCampaignDispatcher>();
        while (!leaseToken.IsCancellationRequested)
        {
            var processed = await dispatcher.DispatchBatchAsync(leaseToken);
            await Task.Delay(processed == 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(100),
                leaseToken);
        }
    }
}
