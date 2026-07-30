using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.BackgroundServices;

public sealed class LiveSupportRecoveryBackgroundService(IServiceScopeFactory scopes, ILogger<LiveSupportRecoveryBackgroundService> logger) : BackgroundService
{
    private readonly Guid _ownerToken = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await ClusterLeaseRunner.TryRunAsync(
                    scope.ServiceProvider,
                    "live-support-recovery",
                    _ownerToken,
                    TimeSpan.FromSeconds(30),
                    static async (services, token) =>
                    {
                        var presence = services.GetRequiredService<ILiveSupportPresenceStore>();
                        var support = services.GetRequiredService<ILiveSupportService>();
                        var disconnected = await presence.ClaimExpiredDisconnectsAsync(DateTime.UtcNow);
                        foreach (var staffId in disconnected)
                        {
                            await support.ReleaseStaffAssignmentsAsync(
                                staffId,
                                LiveSupportAssignmentEndReason.DisconnectTimeout,
                                token);
                        }
                    },
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Live support recovery iteration failed"); }
        }
    }
}
