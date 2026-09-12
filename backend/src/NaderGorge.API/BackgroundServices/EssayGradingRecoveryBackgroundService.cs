using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.BackgroundServices;

public sealed class EssayGradingRecoveryBackgroundService(
    IServiceScopeFactory scopes, ILogger<EssayGradingRecoveryBackgroundService> logger) : BackgroundService
{
    private readonly Guid _ownerToken = Guid.NewGuid();
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await ClusterLeaseRunner.TryRunAsync(scope.ServiceProvider, "essay-grading-recovery", _ownerToken,
                    SweepInterval, RecoverBatchAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Essay grading recovery sweep failed."); }
            await Task.Delay(SweepInterval, stoppingToken);
        }
    }

    private async Task RecoverBatchAsync(IServiceProvider services, CancellationToken ct)
    {
        var recovery = new EssayGradingRecoveryService(services.GetRequiredService<IAppDbContext>());
        foreach (var essayId in await recovery.FindDueAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (await recovery.RecoverAsync(essayId, ct))
                    logger.LogInformation("Recovered unfinished essay grading. EssayId={EssayId}", essayId);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            // A corrupt legacy submission must not block recovery of the rest of the batch.
            catch (Exception ex)
            {
                logger.LogError(ex, "Essay grading recovery failed. EssayId={EssayId}", essayId);
                await recovery.DeferFailureAsync(essayId, ct);
            }
        }
    }
}
