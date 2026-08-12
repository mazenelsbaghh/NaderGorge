using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.API.BackgroundServices;

public sealed class AdminAIRecoveryBackgroundService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<AdminAIRecoveryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("AdminAI:Enabled", false)) return;
        var interval = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("AdminAI:RecoveryIntervalSeconds", 30), 10, 300));
        var batchSize = Math.Clamp(configuration.GetValue("AdminAI:RecoveryBatchSize", 100), 1, 500);
        var ownerToken = Guid.NewGuid();
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var leases = scope.ServiceProvider.GetRequiredService<IClusterLeaseService>();
                var claim = await leases.TryAcquireAsync("admin-ai-recovery", ownerToken, interval + interval, stoppingToken);
                if (claim is null) continue;
                var outcome = "no_changes";
                try
                {
                    var recovered = await scope.ServiceProvider.GetRequiredService<IAdminAIRecoveryService>().ReconcileAsync(batchSize, stoppingToken);
                    var external = await scope.ServiceProvider.GetRequiredService<IAdminAIExternalOperationReconciler>().ReconcileAsync(Math.Min(batchSize, 100), stoppingToken);
                    outcome = $"recovered:{recovered + external}";
                }
                finally { await leases.ReleaseAsync(claim, outcome, stoppingToken); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Admin AI bounded recovery failed."); }
        }
    }
}
