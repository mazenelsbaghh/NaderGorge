using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.BackgroundServices;

public sealed class LiveSupportRecoveryBackgroundService(IServiceScopeFactory scopes, ILogger<LiveSupportRecoveryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var presence = scope.ServiceProvider.GetRequiredService<ILiveSupportPresenceStore>();
                var support = scope.ServiceProvider.GetRequiredService<ILiveSupportService>();
                foreach (var staffId in await presence.ClaimExpiredDisconnectsAsync(DateTime.UtcNow))
                    await support.ReleaseStaffAssignmentsAsync(staffId, LiveSupportAssignmentEndReason.DisconnectTimeout, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Live support recovery iteration failed"); }
        }
    }
}
