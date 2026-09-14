using MediatR;
using NaderGorge.Application.Features.Admin.Commands;

namespace NaderGorge.API.BackgroundServices;

public sealed class BunnyVideoStatusBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<BunnyVideoStatusBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshBatchSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshBatchSafelyAsync(stoppingToken);
        }
    }

    private async Task RefreshBatchSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(
                new RefreshPendingBunnyVideosCommand(),
                cancellationToken);

            if (result.Failed > 0)
            {
                logger.LogWarning(
                    "Bunny status refresh completed with {FailedCount} failures out of {AttemptedCount} attempts.",
                    result.Failed,
                    result.Attempted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Bunny status refresh batch failed.");
        }
    }
}
