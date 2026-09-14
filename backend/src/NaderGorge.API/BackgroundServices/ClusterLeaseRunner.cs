using NaderGorge.Application.Interfaces;

namespace NaderGorge.API.BackgroundServices;

internal static class ClusterLeaseRunner
{
    private static readonly TimeSpan MaximumHeartbeatInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinimumHeartbeatInterval = TimeSpan.FromSeconds(1);

    public static async Task<bool> TryRunAsync(
        IServiceProvider services,
        string leaseName,
        Guid ownerToken,
        TimeSpan leaseLifetime,
        Func<IServiceProvider, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        var leaseService = services.GetRequiredService<IClusterLeaseService>();
        var claim = await leaseService.TryAcquireAsync(
            leaseName,
            ownerToken,
            leaseLifetime,
            cancellationToken);
        if (claim is null)
        {
            return false;
        }

        using var leaseLost = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var heartbeatStopped = new CancellationTokenSource();
        var heartbeat = MaintainLeaseAsync(
            services.GetRequiredService<IServiceScopeFactory>(),
            claim,
            leaseLifetime,
            leaseLost,
            heartbeatStopped.Token);

        try
        {
            await operation(services, leaseLost.Token);
            if (leaseLost.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new ClusterLeaseLostException(claim.Name, claim.FencingGeneration);
            }

            heartbeatStopped.Cancel();
            await heartbeat;
            var completed = await leaseService.RenewAsync(
                claim,
                leaseLifetime,
                "completed",
                cancellationToken);
            if (!completed)
            {
                throw new ClusterLeaseLostException(claim.Name, claim.FencingGeneration);
            }
            return true;
        }
        catch
        {
            heartbeatStopped.Cancel();
            await IgnoreExpectedHeartbeatStopAsync(heartbeat);
            await leaseService.ReleaseAsync(
                claim,
                "failed",
                CancellationToken.None);
            throw;
        }
    }

    private static async Task MaintainLeaseAsync(
        IServiceScopeFactory scopeFactory,
        ClusterLeaseClaim claim,
        TimeSpan leaseLifetime,
        CancellationTokenSource leaseLost,
        CancellationToken heartbeatStopped)
    {
        var heartbeatInterval = TimeSpan.FromTicks(Math.Min(
            MaximumHeartbeatInterval.Ticks,
            Math.Max(MinimumHeartbeatInterval.Ticks, leaseLifetime.Ticks / 3)));
        using var timer = new PeriodicTimer(heartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(heartbeatStopped))
            {
                using var scope = scopeFactory.CreateScope();
                var leaseService = scope.ServiceProvider.GetRequiredService<IClusterLeaseService>();
                var renewed = await leaseService.RenewAsync(
                    claim,
                    leaseLifetime,
                    "running",
                    heartbeatStopped);
                if (renewed)
                {
                    continue;
                }

                leaseLost.Cancel();
                return;
            }
        }
        catch (OperationCanceledException) when (heartbeatStopped.IsCancellationRequested)
        {
            // Normal completion: the owning operation stopped its heartbeat.
        }
        catch
        {
            leaseLost.Cancel();
        }
    }

    private static async Task IgnoreExpectedHeartbeatStopAsync(Task heartbeat)
    {
        try
        {
            await heartbeat;
        }
        catch (OperationCanceledException)
        {
            // The operation is already failing and owns the original exception.
        }
    }
}

internal sealed class ClusterLeaseLostException(string leaseName, long fencingGeneration)
    : InvalidOperationException(
        $"Cluster lease '{leaseName}' fencing generation {fencingGeneration} was lost before completion.");
