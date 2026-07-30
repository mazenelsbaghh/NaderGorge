using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.BackgroundServices;

public sealed class LiveSupportAIRecoveryBackgroundService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<LiveSupportAIRecoveryBackgroundService> logger) : BackgroundService
{
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
                var locks = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();
                if (!await locks.TryLockAsync("live-support-ai-recovery", interval + interval)) continue;
                var recovery = scope.ServiceProvider.GetRequiredService<ILiveSupportAIRecoveryService>();
                var result = await recovery.RecoverBatchAsync(DateTime.UtcNow, batchSize, stoppingToken);
                await locks.SaveResultAsync("live-support-ai-recovery", 200, "{\"status\":\"completed\"}", interval);
                if (result.ReconciledConversations > 0)
                    logger.LogInformation("AI live support recovery reconciled {Count} conversations", result.ReconciledConversations);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "AI live support recovery iteration failed with a safe internal error"); }
        }
    }
}
