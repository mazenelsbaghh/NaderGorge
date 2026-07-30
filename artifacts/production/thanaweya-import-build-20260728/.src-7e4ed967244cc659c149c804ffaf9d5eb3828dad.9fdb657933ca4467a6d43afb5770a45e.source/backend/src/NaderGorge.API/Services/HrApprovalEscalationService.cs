using NaderGorge.Application.Features.HR.Approvals;

namespace NaderGorge.API.Services;

public sealed class HrApprovalEscalationService(IServiceScopeFactory scopeFactory, ILogger<HrApprovalEscalationService> logger) : BackgroundService
{
    private readonly Guid _ownerToken = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await NaderGorge.API.BackgroundServices.ClusterLeaseRunner.TryRunAsync(
                    scope.ServiceProvider,
                    "hr-approval-escalation",
                    _ownerToken,
                    TimeSpan.FromMinutes(7),
                    static (services, token) =>
                        services.GetRequiredService<ApprovalEngine>()
                            .EscalateDueAsync(DateTime.UtcNow, token),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "HR approval escalation cycle failed"); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
