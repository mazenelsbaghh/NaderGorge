using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services;
using Npgsql;

namespace NaderGorge.API.BackgroundServices;

public sealed class FacebookMessengerInboundBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<FacebookMessengerInboundBackgroundService> logger) : BackgroundService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan StaleClaimAge = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunBatchSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunBatchSafelyAsync(stoppingToken);
    }

    internal async Task ProcessBatchAsync(CancellationToken ct)
    {
        List<Guid> candidateIds;
        var now = DateTime.UtcNow;
        var staleBefore = now - StaleClaimAge;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            candidateIds = await db.LiveSupportMessengerWebhookInbox.AsNoTracking()
                .Where(inbox =>
                    (inbox.Status == "Pending" &&
                     (inbox.NextAttemptAt == null || inbox.NextAttemptAt <= now)) ||
                    (inbox.Status == "Processing" && inbox.ClaimedAt < staleBefore))
                .OrderBy(inbox => inbox.CreatedAt)
                .ThenBy(inbox => inbox.Id)
                .Select(inbox => inbox.Id)
                .Take(20)
                .ToListAsync(ct);
        }

        foreach (var candidateId in candidateIds)
            await ProcessCandidateSafelyAsync(candidateId, staleBefore, ct);
    }

    private async Task RunBatchSafelyAsync(CancellationToken ct)
    {
        try
        {
            await ProcessBatchAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Facebook Messenger inbound batch failed.");
        }
    }

    private async Task ProcessCandidateSafelyAsync(
        Guid inboxId,
        DateTime staleBefore,
        CancellationToken ct)
    {
        var claimedAt = DateTime.UtcNow;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var claimed = await db.LiveSupportMessengerWebhookInbox
                .Where(inbox => inbox.Id == inboxId &&
                    inbox.AttemptCount < MaxAttempts &&
                    ((inbox.Status == "Pending" &&
                      (inbox.NextAttemptAt == null || inbox.NextAttemptAt <= claimedAt)) ||
                     (inbox.Status == "Processing" && inbox.ClaimedAt < staleBefore)))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(inbox => inbox.Status, "Processing")
                    .SetProperty(inbox => inbox.ClaimedAt, claimedAt)
                    .SetProperty(inbox => inbox.NextAttemptAt, (DateTime?)null)
                    .SetProperty(inbox => inbox.AttemptCount, inbox => inbox.AttemptCount + 1)
                    .SetProperty(inbox => inbox.UpdatedAt, claimedAt)
                    .SetProperty(inbox => inbox.Version, inbox => inbox.Version + 1), ct);
            if (claimed == 0)
            {
                await FinalizeExhaustedAsync(db, inboxId, claimedAt, ct);
                return;
            }

            var service = scope.ServiceProvider.GetRequiredService<FacebookMessengerLiveSupportService>();
            await service.ProcessInboxEventAsync(inboxId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failure = Classify(exception);
            logger.LogWarning(
                exception,
                "Facebook Messenger inbound event {InboxId} failed with {FailureCode}.",
                inboxId,
                failure.Code);
            await FinalizeFailureSafelyAsync(inboxId, claimedAt, failure, ct);
        }
    }

    private async Task FinalizeFailureSafelyAsync(
        Guid inboxId,
        DateTime claimedAt,
        MessengerWorkerFailure failure,
        CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var inbox = await db.LiveSupportMessengerWebhookInbox.SingleOrDefaultAsync(candidate =>
                candidate.Id == inboxId &&
                candidate.Status == "Processing" &&
                candidate.ClaimedAt == claimedAt, ct);
            if (inbox is null) return;

            inbox.ClaimedAt = null;
            inbox.FailureCode = SafeFailureCode(failure.Code, "MESSENGER_INBOUND_FAILED");
            inbox.UpdatedAt = DateTime.UtcNow;
            inbox.Version++;
            if (failure.Retryable && inbox.AttemptCount < MaxAttempts)
            {
                inbox.Status = "Pending";
                inbox.NextAttemptAt = inbox.UpdatedAt.Value.Add(Backoff(inbox.AttemptCount));
            }
            else
            {
                inbox.Status = "Failed";
                inbox.NextAttemptAt = null;
            }
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Facebook Messenger inbound event {InboxId} failure could not be persisted.",
                inboxId);
        }
    }

    private static async Task FinalizeExhaustedAsync(
        IAppDbContext db,
        Guid inboxId,
        DateTime now,
        CancellationToken ct)
    {
        var staleBefore = now - StaleClaimAge;
        await db.LiveSupportMessengerWebhookInbox
            .Where(inbox => inbox.Id == inboxId &&
                inbox.AttemptCount >= MaxAttempts &&
                ((inbox.Status == "Pending" &&
                  (inbox.NextAttemptAt == null || inbox.NextAttemptAt <= now)) ||
                 (inbox.Status == "Processing" &&
                  inbox.ClaimedAt < staleBefore)))
            .ExecuteUpdateAsync(update => update
                .SetProperty(inbox => inbox.Status, "Failed")
                .SetProperty(inbox => inbox.FailureCode, "MESSENGER_INBOUND_MAX_ATTEMPTS")
                .SetProperty(inbox => inbox.ClaimedAt, (DateTime?)null)
                .SetProperty(inbox => inbox.NextAttemptAt, (DateTime?)null)
                .SetProperty(inbox => inbox.UpdatedAt, now)
                .SetProperty(inbox => inbox.Version, inbox => inbox.Version + 1), ct);
    }

    private static MessengerWorkerFailure Classify(Exception exception) => exception switch
    {
        FacebookMessengerWebhookException webhook =>
            new MessengerWorkerFailure(webhook.ErrorCode, webhook.IsRetryable),
        FacebookMessengerProviderException provider =>
            new MessengerWorkerFailure(provider.ErrorCode, provider.IsRetryable),
        FacebookMessengerConfigurationException configuration =>
            new MessengerWorkerFailure(configuration.ErrorCode, false),
        DbUpdateException { InnerException: PostgresException postgres }
            when postgres.SqlState == PostgresErrorCodes.SerializationFailure =>
                new MessengerWorkerFailure("MESSENGER_INBOUND_SERIALIZATION_RETRY", true),
        DbUpdateException => new MessengerWorkerFailure("MESSENGER_INBOUND_PERSISTENCE_FAILED", true),
        HttpRequestException => new MessengerWorkerFailure("MESSENGER_INBOUND_NETWORK_FAILED", true),
        _ => new MessengerWorkerFailure("MESSENGER_INBOUND_FAILED", true)
    };

    private static TimeSpan Backoff(int attemptCount) =>
        TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Max(1, attemptCount))));

    private static string SafeFailureCode(string code, string fallback)
    {
        var safe = new string(code
            .Where(character => char.IsAsciiLetterOrDigit(character) || character == '_')
            .Take(120)
            .ToArray());
        return safe.Length == 0 ? fallback : safe;
    }

    private sealed record MessengerWorkerFailure(string Code, bool Retryable);
}
