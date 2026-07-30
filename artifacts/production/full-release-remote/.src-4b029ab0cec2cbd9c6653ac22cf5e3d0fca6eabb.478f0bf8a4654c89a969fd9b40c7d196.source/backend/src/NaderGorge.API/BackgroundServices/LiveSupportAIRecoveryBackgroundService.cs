using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.BackgroundServices;

public sealed class LiveSupportAIRecoveryBackgroundService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<LiveSupportAIRecoveryBackgroundService> logger) : BackgroundService
{
    private readonly Guid _ownerToken = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("LiveSupportAI:RecoveryIntervalSeconds", 30), 10, 300));
        var batchSize = Math.Clamp(configuration.GetValue("LiveSupportAI:RecoveryBatchSize", 100), 1, 500);
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await ClusterLeaseRunner.TryRunAsync(
                    scope.ServiceProvider,
                    "live-support-ai-recovery",
                    _ownerToken,
                    interval + interval,
                    async (services, token) =>
                    {
                        var recovery = services.GetRequiredService<ILiveSupportAIRecoveryService>();
                        var result = await recovery.RecoverBatchAsync(DateTime.UtcNow, batchSize, token);
                        if (result.ReconciledConversations > 0)
                        {
                            logger.LogInformation(
                                "AI live support recovery reconciled {Count} conversations",
                                result.ReconciledConversations);
                        }
                    },
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "AI live support recovery iteration failed with a safe internal error"); }
        }
    }
}
