using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.BackgroundServices;

public sealed class FacebookMessengerAdminRecoveryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<FacebookMessengerAdminRecoveryBackgroundService> logger) : BackgroundService
{
    private const string LeaseName = "facebook-messenger-admin-recovery";
    private static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(50);
    private readonly Guid _ownerToken = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunRecoveryIterationSafelyAsync(stoppingToken);
            await Task.Delay(RecoveryInterval, stoppingToken);
        }
    }

    private async Task RunRecoveryIterationSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await ClusterLeaseRunner.TryRunAsync(
                scope.ServiceProvider,
                LeaseName,
                _ownerToken,
                LeaseLifetime,
                RecoverStalePageOperationsAsync,
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Facebook Messenger admin operation recovery iteration failed.");
        }
    }

    private static async Task RecoverStalePageOperationsAsync(
        IServiceProvider services,
        CancellationToken leaseToken)
    {
        using var operationTimeout = CancellationTokenSource.CreateLinkedTokenSource(leaseToken);
        operationTimeout.CancelAfter(OperationTimeout);

        try
        {
            var service = services.GetRequiredService<FacebookMessengerAdminService>();
            await service.RecoverStalePageOperationsAsync(DateTime.UtcNow, operationTimeout.Token);
        }
        catch (OperationCanceledException) when (
            operationTimeout.IsCancellationRequested &&
            !leaseToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Facebook Messenger admin operation recovery exceeded its time limit.");
        }
    }
}
