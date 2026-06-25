using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.BackgroundServices;

public sealed class RechargeRequestExpiryBackgroundService(
    IServiceScopeFactory scopes,
    ILogger<RechargeRequestExpiryBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RechargeRequestExpiryBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                await RechargeRequestExpiryService.RejectPendingOlderThan24Hours(db, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to auto-reject expired recharge requests.");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }

        logger.LogInformation("RechargeRequestExpiryBackgroundService stopped.");
    }
}
