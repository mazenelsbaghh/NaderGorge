using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;
using Npgsql;

namespace NaderGorge.Infrastructure.Services;

public sealed record FacebookMessengerAdminPageDto(
    string Id,
    string PageId,
    string DisplayName,
    bool AccessTokenConfigured,
    bool HumanAgentEnabled,
    string ConnectionStatus,
    bool? TokenValid,
    bool? Subscribed,
    DateTime? LastCheckedAtUtc,
    DateTime? LastInboundAtUtc,
    DateTime? LastOutboundAtUtc,
    string? LastErrorCode);

public sealed record FacebookMessengerAdminSettingsDto(
    string Revision,
    string AppId,
    bool AppSecretConfigured,
    bool VerifyTokenConfigured,
    string ApiVersion,
    IReadOnlyList<string> SupportedApiVersions,
    string WebhookUrl,
    IReadOnlyList<FacebookMessengerAdminPageDto> Pages);

public sealed record FacebookMessengerVerifyTokenRotationDto(
    string VerifyToken,
    DateTime RotatedAtUtc,
    string Revision);

public sealed record FacebookMessengerPageCheckDto(
    FacebookMessengerAdminPageDto Page,
    DateTime CheckedAtUtc,
    string Revision);

public sealed record FacebookMessengerSettingsUpdate(
    string AppId,
    string ApiVersion,
    string? AppSecret,
    long ExpectedRevision);

public sealed record FacebookMessengerPageLink(
    string AccessToken,
    bool HumanAgentEnabled,
    Guid? ExistingPageRecordId,
    long ExpectedRevision);

public sealed class FacebookMessengerAdminService(
    IAppDbContext db,
    IFacebookMessengerSecretProtector protector,
    FacebookMessengerGraphClient graph,
    IConfiguration applicationConfiguration,
    ILogger<FacebookMessengerAdminService> logger)
{
    private const int MaximumPageCount = 3;
    private const string CurrentApiVersion = "v26.0";
    private const string ConnectedStatus = "Connected";
    private const string LinkingStatus = "Linking";
    private const string LinkUncertainStatus = "LinkUncertain";
    private const string LinkSettlingStatus = "LinkSettling";
    private const string UnlinkingStatus = "Unlinking";
    private const string UnlinkUncertainStatus = "UnlinkUncertain";
    private const string UnlinkSettlingStatus = "UnlinkSettling";
    private const string RemoteUnsubscribeConfirmedStatus = "RemoteUnsubscribeConfirmed";
    private const int PersistenceRetryAttempts = 3;
    internal static readonly TimeSpan OperationRecoveryDelay = TimeSpan.FromMinutes(2);
    // Keeps the opposite mutation fenced until a later GET-only reconciliation after quarantine.
    internal static readonly TimeSpan OperationSettlementDelay = TimeSpan.FromMinutes(5);
    private static readonly string[] SupportedApiVersions = [CurrentApiVersion];

    public async Task<FacebookMessengerAdminSettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        var settings = await SettingsQuery().SingleOrDefaultAsync(ct);
        if (settings is null)
        {
            return new FacebookMessengerAdminSettingsDto(
                "0",
                string.Empty,
                false,
                false,
                CurrentApiVersion,
                SupportedApiVersions,
                WebhookUrl(),
                []);
        }
        return await ToSettingsDtoAsync(settings, ct);
    }

    public async Task<FacebookMessengerAdminSettingsDto> UpdateSettingsAsync(
        FacebookMessengerSettingsUpdate update,
        Guid actorUserId,
        CancellationToken ct)
    {
        var normalizedAppId = NormalizeAppId(update.AppId);
        var normalizedApiVersion = NormalizeApiVersion(update.ApiVersion);
        var normalizedAppSecret = OptionalSecret(update.AppSecret, 512, "MESSENGER_APP_SECRET_INVALID");
        var settings = await db.LiveSupportMessengerConfigurations.SingleOrDefaultAsync(candidate =>
            candidate.ConfigurationKey == LiveSupportMessengerConfiguration.DefaultConfigurationKey, ct);
        var appIdentityChanged = settings is not null &&
            settings.AppId.Length > 0 &&
            !string.Equals(settings.AppId, normalizedAppId, StringComparison.Ordinal);
        if (appIdentityChanged && await db.LiveSupportMessengerPages.AnyAsync(ct))
            throw new FacebookMessengerAdminException(
                "MESSENGER_UNLINK_PAGES_BEFORE_APP_CHANGE",
                "ألغِ ربط صفحات Messenger أولًا قبل تغيير App ID.",
                StatusCodes.Status409Conflict);
        if (appIdentityChanged && normalizedAppSecret is null)
            throw new FacebookMessengerAdminException(
                "MESSENGER_APP_SECRET_REQUIRED_FOR_APP_CHANGE",
                "أدخل App Secret الخاص بالتطبيق الجديد عند تغيير App ID.",
                StatusCodes.Status400BadRequest);

        if (settings is null)
        {
            if (update.ExpectedRevision != 0) throw Conflict();
            settings = new LiveSupportMessengerConfiguration
            {
                ConfigurationKey = LiveSupportMessengerConfiguration.DefaultConfigurationKey,
                Version = 1,
                CreatedAt = DateTime.UtcNow
            };
            db.LiveSupportMessengerConfigurations.Add(settings);
        }
        else
        {
            RequireRevision(settings.Version, update.ExpectedRevision);
            settings.Version++;
            settings.UpdatedAt = DateTime.UtcNow;
        }

        settings.AppId = normalizedAppId;
        settings.ApiVersion = normalizedApiVersion;
        settings.UpdatedByUserId = actorUserId;
        if (normalizedAppSecret is not null)
            settings.AppSecretCiphertext = protector.Protect(settings.Id, "app-secret", normalizedAppSecret);
        settings.IsEnabled = await CanEnableAsync(settings, ct);
        AddConfigurationAudit("SettingsUpdated", settings.Id, actorUserId, new
        {
            settings.AppId,
            settings.ApiVersion,
            AppSecretChanged = normalizedAppSecret is not null,
            AppIdentityChanged = appIdentityChanged
        });
        await db.SaveChangesAsync(ct);
        return await ToSettingsDtoAsync(settings, ct);
    }

    public async Task<FacebookMessengerVerifyTokenRotationDto> RotateVerifyTokenAsync(
        long expectedRevision,
        Guid actorUserId,
        CancellationToken ct)
    {
        var settings = await RequireSettingsAsync(ct);
        RequireRevision(settings.Version, expectedRevision);
        var token = Base64Url(RandomNumberGenerator.GetBytes(48));
        var now = DateTime.UtcNow;
        settings.VerifyTokenCiphertext = protector.Protect(settings.Id, "verify-token", token);
        settings.VerifyTokenRotatedAt = now;
        settings.UpdatedAt = now;
        settings.UpdatedByUserId = actorUserId;
        settings.Version++;
        settings.IsEnabled = await CanEnableAsync(settings, ct);
        AddConfigurationAudit("VerifyTokenRotated", settings.Id, actorUserId, new { RotatedAtUtc = now });
        await db.SaveChangesAsync(ct);
        return new FacebookMessengerVerifyTokenRotationDto(
            token,
            now,
            Revision(settings.Version));
    }

    public async Task<FacebookMessengerAdminPageDto> LinkPageAsync(
        FacebookMessengerPageLink link,
        Guid actorUserId,
        CancellationToken ct)
    {
        var normalizedToken = RequiredSecret(link.AccessToken, 4096, "MESSENGER_PAGE_ACCESS_TOKEN_INVALID");
        var settingsSnapshot = await RequireSettingsAsync(ct);
        RequireRevision(settingsSnapshot.Version, link.ExpectedRevision);
        RequireApplicationReady(settingsSnapshot);

        FacebookMessengerPageIdentity identity;
        try
        {
            identity = await graph.InspectPageTokenForAppAsync(
                settingsSnapshot.ApiVersion,
                settingsSnapshot.AppId,
                UnprotectApplicationSecret(settingsSnapshot),
                normalizedToken,
                ct);
        }
        catch (FacebookMessengerProviderException exception) when (exception.IsRetryable)
        {
            throw MetaUnavailable();
        }
        catch (FacebookMessengerProviderException exception)
        {
            throw ProviderError(exception.ErrorCode);
        }
        catch (HttpRequestException)
        {
            throw MetaUnavailable();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw MetaUnavailable();
        }
        ValidatePageIdentity(identity);
        var start = await BeginPageLinkAsync(
            identity,
            normalizedToken,
            link,
            actorUserId,
            ct);
        if (start.Claim is not null)
            await SubscribeClaimedPageAsync(start.Claim, ct);
        return await PageDtoAsync(start.PageRecordId, ct);
    }

    private async Task<MessengerPageLinkStart> BeginPageLinkAsync(
        FacebookMessengerPageIdentity identity,
        string token,
        FacebookMessengerPageLink link,
        Guid actorUserId,
        CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var settings = await RequireSettingsAsync(ct);
        RequireRevision(settings.Version, link.ExpectedRevision);
        RequireApplicationReady(settings);
        var page = link.ExistingPageRecordId.HasValue
            ? await RequirePageAsync(link.ExistingPageRecordId.Value, ct)
            : await db.LiveSupportMessengerPages.SingleOrDefaultAsync(
                candidate => candidate.PageId == identity.PageId,
                ct);
        if (page is not null &&
            !string.Equals(page.PageId, identity.PageId, StringComparison.Ordinal))
            throw new FacebookMessengerAdminException(
                "MESSENGER_PAGE_TOKEN_MISMATCH",
                "Page Access Token لا يخص الصفحة التي يتم تحديثها.",
                StatusCodes.Status422UnprocessableEntity);

        var now = DateTime.UtcNow;
        var previousStatus = page?.ConnectionStatus;
        var wasUncertainLink = previousStatus == LinkUncertainStatus;
        var refreshPendingUnlinkToken = previousStatus == UnlinkUncertainStatus;
        if (page is null)
        {
            if (await db.LiveSupportMessengerPages.CountAsync(ct) >= MaximumPageCount)
                throw new FacebookMessengerAdminException(
                    "MESSENGER_PAGE_LIMIT_EXCEEDED",
                    "يمكن ربط 3 صفحات Messenger كحد أقصى.",
                    StatusCodes.Status409Conflict);
            page = new LiveSupportMessengerPage
            {
                PageId = identity.PageId,
                CreatedAt = now,
                Version = 1
            };
            db.LiveSupportMessengerPages.Add(page);
        }
        else
        {
            if (!wasUncertainLink && !refreshPendingUnlinkToken)
                EnsurePageOperationIsIdle(page);
            page.Version++;
        }

        page.DisplayName = NormalizeDisplayName(identity.DisplayName);
        page.PageAccessTokenCiphertext = protector.Protect(page.Id, "page-access-token", token);
        page.IsEnabled = false;
        page.TokenValid = true;
        page.IsSubscribed = null;
        page.LastCredentialCheckAt = now;
        page.UpdatedAt = now;
        page.UpdatedByUserId = actorUserId;
        if (refreshPendingUnlinkToken)
        {
            page.ConnectionStatus = UnlinkUncertainStatus;
            page.LastErrorCode = "MESSENGER_UNSUBSCRIBE_UNCERTAIN";
        }
        else
        {
            page.HumanAgentEnabled = link.HumanAgentEnabled;
            page.ConnectionStatus = LinkingStatus;
            page.LastSubscriptionCheckAt = null;
            page.LastErrorCode = null;
        }
        settings.Version++;
        settings.UpdatedAt = now;
        settings.UpdatedByUserId = actorUserId;
        settings.IsEnabled = HasApplicationCredentials(settings) &&
            await HasOtherConnectedPageAsync(page.Id, ct);
        if (refreshPendingUnlinkToken)
            AddPageAudit("PageUnlinkCredentialRefreshed", page.Id, actorUserId, new
            {
                page.PageId,
                page.DisplayName,
                PreviousStatus = previousStatus,
                RefreshedAtUtc = now,
                AccessTokenChanged = true
            });
        else
            AddPageAudit("PageLinkStarted", page.Id, actorUserId, new
            {
                page.PageId,
                page.DisplayName,
                page.HumanAgentEnabled,
                PreviousStatus = previousStatus,
                WasUncertain = wasUncertainLink,
                ClaimedAtUtc = now,
                AccessTokenChanged = true
            });
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw PageConcurrencyConflict();
        }
        catch (DbUpdateException exception) when (IsUniqueOrSerializationFailure(exception))
        {
            throw PageConcurrencyConflict();
        }

        return new MessengerPageLinkStart(
            page.Id,
            refreshPendingUnlinkToken
                ? null
                : new MessengerPageLinkClaim(
                    page.Id,
                    page.PageId,
                    page.Version,
                    settings.ApiVersion,
                    settings.AppId,
                    token,
                    actorUserId,
                    wasUncertainLink,
                    LinkingStatus));
    }

    public async Task<FacebookMessengerPageCheckDto> CheckPageAsync(
        Guid pageId,
        Guid actorUserId,
        CancellationToken ct)
    {
        var page = await RequirePageAsync(pageId, ct);
        EnsurePageOperationIsIdle(page);
        var now = DateTime.UtcNow;
        var settings = await RequireSettingsAsync(ct);
        var operation = new MessengerPageOperation(
            page,
            settings,
            UnprotectPageToken(page),
            UnprotectApplicationSecret(settings),
            actorUserId,
            now);
        await RefreshPageConnectionAsync(operation, ct);
        return new FacebookMessengerPageCheckDto(
            await PageDtoAsync(page.Id, ct),
            now,
            Revision(settings.Version));
    }

    private async Task RefreshPageConnectionAsync(
        MessengerPageOperation operation,
        CancellationToken ct)
    {
        try
        {
            await VerifyPageConnectionAsync(operation, ct);
        }
        catch (FacebookMessengerProviderException exception)
        {
            var invalidToken = exception.ErrorCode == "MESSENGER_GRAPH_190";
            await PersistPageStateAsync(new MessengerPageStateChange(
                operation.Page,
                operation.Settings,
                invalidToken ? false : operation.Page.TokenValid,
                null,
                invalidToken ? "TokenInvalid" : "Unknown",
                exception.ErrorCode,
                operation.ActorUserId,
                operation.CheckedAt,
                null), ct);
        }
        catch (HttpRequestException)
        {
            await PersistPageUnavailableAsync(operation, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await PersistPageUnavailableAsync(operation, ct);
        }
    }

    private async Task VerifyPageConnectionAsync(
        MessengerPageOperation operation,
        CancellationToken ct)
    {
        var identity = await graph.InspectPageTokenForAppAsync(
            operation.Settings.ApiVersion,
            operation.Settings.AppId,
            operation.AppSecret,
            operation.Token,
            ct);
        if (!string.Equals(identity.PageId, operation.Page.PageId, StringComparison.Ordinal))
        {
            await PersistPageStateAsync(new MessengerPageStateChange(
                operation.Page,
                operation.Settings,
                false,
                null,
                "PageMismatch",
                "MESSENGER_PAGE_TOKEN_MISMATCH",
                operation.ActorUserId,
                operation.CheckedAt,
                null), ct);
            return;
        }

        operation.Page.DisplayName = NormalizeDisplayName(identity.DisplayName);
        var subscription = await graph.GetSubscriptionAsync(
            operation.Settings.ApiVersion,
            operation.Page.PageId,
            operation.Settings.AppId,
            operation.Token,
            ct);
        var complete = CompleteSubscription(subscription);
        await PersistPageStateAsync(new MessengerPageStateChange(
            operation.Page,
            operation.Settings,
            true,
            complete,
            complete ? "Connected" : "NotSubscribed",
            complete ? null : SubscriptionFailureCode(subscription),
            operation.ActorUserId,
            operation.CheckedAt,
            operation.CheckedAt), ct);
    }

    private Task PersistPageUnavailableAsync(
        MessengerPageOperation operation,
        CancellationToken ct) =>
        PersistPageStateAsync(new MessengerPageStateChange(
            operation.Page,
            operation.Settings,
            operation.Page.TokenValid,
            null,
            "Unknown",
            "MESSENGER_GRAPH_UNAVAILABLE",
            operation.ActorUserId,
            operation.CheckedAt,
            null), ct);

    public async Task<FacebookMessengerAdminSettingsDto> DeletePageAsync(
        Guid pageId,
        long expectedRevision,
        Guid actorUserId,
        CancellationToken ct)
    {
        var claim = await BeginPageUnlinkAsync(pageId, expectedRevision, actorUserId, ct);
        await UnsubscribeClaimedPageAsync(claim, ct);
        return await GetSettingsAsync(ct);
    }

    private async Task<MessengerPageUnlinkClaim> BeginPageUnlinkAsync(
        Guid pageId,
        long expectedRevision,
        Guid actorUserId,
        CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var settings = await RequireSettingsAsync(ct);
        RequireRevision(settings.Version, expectedRevision);
        var page = await RequirePageAsync(pageId, ct);
        EnsurePageOperationIsIdle(page);
        var now = DateTime.UtcNow;
        var token = UnprotectPageToken(page);
        page.IsEnabled = false;
        page.IsSubscribed = null;
        page.ConnectionStatus = UnlinkingStatus;
        page.LastErrorCode = null;
        page.UpdatedAt = now;
        page.UpdatedByUserId = actorUserId;
        page.Version++;
        settings.Version++;
        settings.UpdatedAt = now;
        settings.UpdatedByUserId = actorUserId;
        settings.IsEnabled = HasApplicationCredentials(settings) &&
            await HasOtherConnectedPageAsync(page.Id, ct);
        AddPageAudit("PageUnlinkStarted", page.Id, actorUserId, new
        {
            page.PageId,
            page.DisplayName,
            ClaimedAtUtc = now
        });
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw PageConcurrencyConflict();
        }
        catch (DbUpdateException exception) when (IsUniqueOrSerializationFailure(exception))
        {
            throw PageConcurrencyConflict();
        }
        return new MessengerPageUnlinkClaim(
            page.Id,
            page.PageId,
            page.DisplayName,
            page.Version,
            settings.ApiVersion,
            settings.AppId,
            token,
            actorUserId,
            false,
            UnlinkingStatus);
    }

    private async Task UnsubscribeClaimedPageAsync(
        MessengerPageUnlinkClaim claim,
        CancellationToken ct)
    {
        FacebookMessengerSubscriptionState state;
        try
        {
            state = await graph.UnsubscribePageAsync(
                claim.ApiVersion,
                claim.PageId,
                claim.AppId,
                claim.Token,
                ct);
        }
        catch (FacebookMessengerMutationUncertainException exception)
        {
            await MarkClaimedUnlinkStateAsync(
                claim,
                UnlinkUncertainStatus,
                exception.ErrorCode,
                null,
                "PageUnlinkUncertain",
                ct);
            throw UnsubscribeUncertain();
        }
        catch (FacebookMessengerProviderException exception) when (exception.IsRetryable)
        {
            await MarkClaimedUnlinkStateAsync(
                claim,
                UnlinkUncertainStatus,
                exception.ErrorCode,
                null,
                "PageUnlinkUncertain",
                ct);
            throw UnsubscribeUncertain();
        }
        catch (FacebookMessengerProviderException exception)
        {
            var tokenInvalid = exception.ErrorCode == "MESSENGER_GRAPH_190";
            if (claim.WasUncertain)
            {
                await MarkClaimedUnlinkStateAsync(
                    claim,
                    UnlinkUncertainStatus,
                    exception.ErrorCode,
                    null,
                    "PageUnlinkUncertain",
                    ct,
                    tokenInvalid ? false : null);
                throw UnsubscribeUncertain();
            }
            await MarkClaimedUnlinkStateAsync(
                claim,
                tokenInvalid ? "TokenInvalid" : "UnlinkFailed",
                exception.ErrorCode,
                null,
                "PageUnlinkFailed",
                ct,
                tokenInvalid ? false : null);
            throw ProviderError(exception.ErrorCode);
        }
        catch (HttpRequestException)
        {
            await MarkClaimedUnlinkStateAsync(
                claim,
                UnlinkUncertainStatus,
                "MESSENGER_GRAPH_UNAVAILABLE",
                null,
                "PageUnlinkUncertain",
                ct);
            throw UnsubscribeUncertain();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await MarkClaimedUnlinkStateAsync(
                claim,
                UnlinkUncertainStatus,
                "MESSENGER_GRAPH_UNAVAILABLE",
                null,
                "PageUnlinkUncertain",
                ct);
            throw UnsubscribeUncertain();
        }

        if (claim.WasUncertain)
        {
            await MarkClaimedUnlinkStateAsync(
                claim,
                UnlinkSettlingStatus,
                "MESSENGER_UNSUBSCRIBE_UNCERTAIN",
                state.IsSubscribed,
                "PageUnlinkSettling",
                ct);
            return;
        }

        if (state.IsSubscribed)
        {
            await MarkClaimedUnlinkStateAsync(
                claim,
                UnlinkUncertainStatus,
                "MESSENGER_UNSUBSCRIBE_NOT_CONFIRMED",
                true,
                "PageUnlinkUncertain",
                ct);
            throw new FacebookMessengerAdminException(
                "MESSENGER_UNSUBSCRIBE_NOT_CONFIRMED",
                "قبل Meta طلب الإلغاء لكن لم يؤكد اكتماله؛ سيواصل النظام المصالحة تلقائيًا.",
                StatusCodes.Status503ServiceUnavailable);
        }

        var remoteConfirmation = await PersistRemoteUnsubscribeConfirmedAsync(claim, ct);
        if (remoteConfirmation is null) throw PageOperationInProgress();
        await FinalizeRemoteUnsubscribeAsync(remoteConfirmation, ct);
    }

    private async Task SubscribeClaimedPageAsync(
        MessengerPageLinkClaim claim,
        CancellationToken ct)
    {
        FacebookMessengerSubscriptionState subscription;
        try
        {
            subscription = await graph.SubscribePageAsync(
                claim.ApiVersion,
                claim.PageId,
                claim.AppId,
                claim.Token,
                ct);
        }
        catch (FacebookMessengerMutationUncertainException)
        {
            await PersistClaimedLinkStateAsync(
                claim,
                MessengerPageLinkOutcome.Uncertain,
                ct);
            throw SubscriptionUncertain();
        }
        catch (FacebookMessengerProviderException exception) when (exception.IsRetryable)
        {
            await PersistClaimedLinkStateAsync(
                claim,
                MessengerPageLinkOutcome.Uncertain,
                ct);
            throw SubscriptionUncertain();
        }
        catch (FacebookMessengerProviderException exception)
        {
            var tokenInvalid = exception.ErrorCode == "MESSENGER_GRAPH_190";
            if (claim.WasUncertain)
            {
                await PersistClaimedLinkStateAsync(
                    claim,
                    new MessengerPageLinkOutcome(
                        tokenInvalid ? false : true,
                        null,
                        LinkUncertainStatus,
                        exception.ErrorCode,
                        false,
                        "PageLinkUncertain",
                        null),
                    ct);
                throw SubscriptionUncertain();
            }
            await PersistClaimedLinkStateAsync(
                claim,
                new MessengerPageLinkOutcome(
                    tokenInvalid ? false : true,
                    false,
                    tokenInvalid ? "TokenInvalid" : "SubscriptionFailed",
                    exception.ErrorCode,
                    false,
                    "PageLinkFailed",
                    DateTime.UtcNow),
                ct);
            throw ProviderError(exception.ErrorCode);
        }
        catch (HttpRequestException)
        {
            await PersistClaimedLinkStateAsync(
                claim,
                MessengerPageLinkOutcome.Uncertain,
                ct);
            throw SubscriptionUncertain();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await PersistClaimedLinkStateAsync(
                claim,
                MessengerPageLinkOutcome.Uncertain,
                ct);
            throw SubscriptionUncertain();
        }

        var now = DateTime.UtcNow;
        var complete = CompleteSubscription(subscription);
        if (claim.WasUncertain)
        {
            await PersistClaimedLinkStateAsync(
                claim,
                new MessengerPageLinkOutcome(
                    true,
                    subscription.IsSubscribed,
                    LinkSettlingStatus,
                    "MESSENGER_SUBSCRIPTION_UNCERTAIN",
                    false,
                    "PageLinkSettling",
                    now),
                ct);
            return;
        }
        await PersistClaimedLinkStateAsync(
            claim,
            new MessengerPageLinkOutcome(
                true,
                subscription.IsSubscribed,
                complete ? ConnectedStatus : LinkUncertainStatus,
                complete ? null : SubscriptionFailureCode(subscription),
                complete,
                complete ? "PageLinked" : "PageLinkUncertain",
                now),
            ct);
        if (!complete)
            throw SubscriptionUncertain();
    }

    private async Task PersistClaimedLinkStateAsync(
        MessengerPageLinkClaim claim,
        MessengerPageLinkOutcome outcome,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= PersistenceRetryAttempts; attempt++)
        {
            db.ClearTrackedChanges();
            try
            {
                await using var transaction = await db.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    ct);
                var page = await db.LiveSupportMessengerPages.SingleOrDefaultAsync(
                    candidate => candidate.Id == claim.PageRecordId,
                    ct);
                if (page is null ||
                    page.ConnectionStatus != claim.ExpectedStatus ||
                    page.Version != claim.PageVersion)
                    throw PageOperationInProgress();

                var settings = await RequireSettingsAsync(ct);
                page.TokenValid = outcome.TokenValid;
                page.IsSubscribed = outcome.Subscribed;
                page.ConnectionStatus = outcome.Status;
                page.IsEnabled = outcome.IsEnabled;
                page.LastErrorCode = SafeErrorCode(outcome.ErrorCode);
                page.LastSubscriptionCheckAt = outcome.SubscriptionCheckedAt;
                page.UpdatedAt = DateTime.UtcNow;
                if (claim.ActorUserId.HasValue)
                    page.UpdatedByUserId = claim.ActorUserId.Value;
                page.Version++;
                settings.IsEnabled = HasApplicationCredentials(settings) &&
                    (outcome.IsEnabled || await HasOtherConnectedPageAsync(page.Id, ct));
                settings.UpdatedAt = DateTime.UtcNow;
                if (claim.ActorUserId.HasValue)
                    settings.UpdatedByUserId = claim.ActorUserId.Value;
                settings.Version++;
                AddPageAudit(outcome.AuditAction, page.Id, claim.ActorUserId, new
                {
                    page.PageId,
                    outcome.Status,
                    outcome.TokenValid,
                    outcome.Subscribed,
                    ErrorCode = page.LastErrorCode
                });
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }
            catch (Exception exception) when (IsRetryablePersistenceConflict(exception))
            {
                if (attempt == PersistenceRetryAttempts) break;
                logger.LogWarning(
                    exception,
                    "Retrying Messenger page link completion {Attempt}/{MaximumAttempts} for {PageRecordId}",
                    attempt,
                    PersistenceRetryAttempts,
                    claim.PageRecordId);
            }
        }

        throw Conflict();
    }

    private async Task MarkClaimedUnlinkStateAsync(
        MessengerPageUnlinkClaim claim,
        string status,
        string errorCode,
        bool? subscribed,
        string auditAction,
        CancellationToken ct,
        bool? tokenValid = null)
    {
        db.ClearTrackedChanges();
        var page = await db.LiveSupportMessengerPages.SingleOrDefaultAsync(
            candidate => candidate.Id == claim.PageRecordId,
            ct);
        if (page is null ||
            page.ConnectionStatus != claim.ExpectedStatus ||
            page.Version != claim.PageVersion)
            return;

        if (tokenValid.HasValue) page.TokenValid = tokenValid.Value;
        page.IsEnabled = false;
        page.IsSubscribed = subscribed;
        page.ConnectionStatus = status;
        page.LastErrorCode = SafeErrorCode(errorCode);
        if (subscribed.HasValue) page.LastSubscriptionCheckAt = DateTime.UtcNow;
        page.UpdatedAt = DateTime.UtcNow;
        if (claim.ActorUserId.HasValue)
            page.UpdatedByUserId = claim.ActorUserId.Value;
        page.Version++;
        AddPageAudit(auditAction, page.Id, claim.ActorUserId, new
        {
            page.PageId,
            Status = status,
            ErrorCode = page.LastErrorCode,
            Subscribed = subscribed
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ClearTrackedChanges();
        }
    }

    private async Task<MessengerRemoteUnsubscribeConfirmation?> PersistRemoteUnsubscribeConfirmedAsync(
        MessengerPageUnlinkClaim claim,
        CancellationToken ct)
    {
        db.ClearTrackedChanges();
        var page = await db.LiveSupportMessengerPages.SingleOrDefaultAsync(
            candidate => candidate.Id == claim.PageRecordId,
            ct);
        if (page is null ||
            page.ConnectionStatus != claim.ExpectedStatus ||
            page.Version != claim.PageVersion)
            return null;

        var now = DateTime.UtcNow;
        page.IsEnabled = false;
        page.IsSubscribed = false;
        page.ConnectionStatus = RemoteUnsubscribeConfirmedStatus;
        page.LastErrorCode = null;
        page.LastSubscriptionCheckAt = now;
        page.UpdatedAt = now;
        if (claim.ActorUserId.HasValue)
            page.UpdatedByUserId = claim.ActorUserId.Value;
        page.Version++;
        AddPageAudit("PageRemoteUnsubscribeConfirmed", page.Id, claim.ActorUserId, new
        {
            page.PageId,
            ConfirmedAtUtc = now
        });
        try
        {
            await db.SaveChangesAsync(ct);
            return new MessengerRemoteUnsubscribeConfirmation(
                page.Id,
                claim.ActorUserId);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ClearTrackedChanges();
            return null;
        }
    }

    private async Task FinalizeRemoteUnsubscribeAsync(
        MessengerRemoteUnsubscribeConfirmation confirmation,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= PersistenceRetryAttempts; attempt++)
        {
            db.ClearTrackedChanges();
            try
            {
                await using var transaction = await db.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    ct);
                var page = await db.LiveSupportMessengerPages.SingleOrDefaultAsync(
                    candidate => candidate.Id == confirmation.PageRecordId,
                    ct);
                if (page is null)
                {
                    await transaction.CommitAsync(ct);
                    return;
                }
                if (page.ConnectionStatus != RemoteUnsubscribeConfirmedStatus)
                    throw PageOperationInProgress();

                var settings = await RequireSettingsAsync(ct);
                AddPageAudit("PageUnlinked", page.Id, confirmation.ActorUserId, new
                {
                    page.PageId,
                    page.DisplayName
                });
                db.LiveSupportMessengerPages.Remove(page);
                settings.IsEnabled = HasApplicationCredentials(settings) &&
                    await HasOtherConnectedPageAsync(page.Id, ct);
                settings.UpdatedAt = DateTime.UtcNow;
                if (confirmation.ActorUserId.HasValue)
                    settings.UpdatedByUserId = confirmation.ActorUserId.Value;
                settings.Version++;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }
            catch (Exception exception) when (IsRetryablePersistenceConflict(exception))
            {
                if (attempt == PersistenceRetryAttempts) break;
                logger.LogWarning(
                    exception,
                    "Retrying Messenger remote unsubscribe finalization {Attempt}/{MaximumAttempts} for {PageRecordId}",
                    attempt,
                    PersistenceRetryAttempts,
                    confirmation.PageRecordId);
            }
        }

        throw Conflict();
    }

    public async Task<int> RecoverStalePageOperationsAsync(
        DateTime utcNow,
        CancellationToken ct)
    {
        var recoveryCutoff = utcNow - OperationRecoveryDelay;
        var settlementCutoff = utcNow - OperationSettlementDelay;
        var candidates = await db.LiveSupportMessengerPages.AsNoTracking()
            .Where(page =>
                page.ConnectionStatus == RemoteUnsubscribeConfirmedStatus ||
                ((page.UpdatedAt ?? page.CreatedAt) <= recoveryCutoff &&
                 (page.ConnectionStatus == LinkingStatus ||
                  page.ConnectionStatus == LinkUncertainStatus ||
                  page.ConnectionStatus == UnlinkingStatus ||
                  page.ConnectionStatus == UnlinkUncertainStatus)) ||
                ((page.UpdatedAt ?? page.CreatedAt) <= settlementCutoff &&
                 (page.ConnectionStatus == LinkSettlingStatus ||
                  page.ConnectionStatus == UnlinkSettlingStatus)))
            .OrderBy(page => page.UpdatedAt ?? page.CreatedAt)
            .Select(page => new { page.Id, page.ConnectionStatus })
            .Take(20)
            .ToListAsync(ct);
        var recovered = 0;
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (candidate.ConnectionStatus == RemoteUnsubscribeConfirmedStatus)
                {
                    await FinalizeRemoteUnsubscribeAsync(
                        new MessengerRemoteUnsubscribeConfirmation(candidate.Id, null),
                        ct);
                    recovered++;
                    continue;
                }

                if (candidate.ConnectionStatus is LinkSettlingStatus or UnlinkSettlingStatus)
                {
                    var settlement = await TryBeginPageSettlementAsync(
                        candidate.Id,
                        settlementCutoff,
                        utcNow,
                        ct);
                    if (settlement is null) continue;
                    recovered++;
                    await SettlePageAsync(settlement, ct);
                    continue;
                }

                var claim = await TryBeginPageRecoveryAsync(
                    candidate.Id,
                    recoveryCutoff,
                    utcNow,
                    ct);
                if (claim is null) continue;
                recovered++;
                if (claim.IsLink)
                {
                    await SubscribeClaimedPageAsync(
                        new MessengerPageLinkClaim(
                            claim.PageRecordId,
                            claim.PageId,
                            claim.PageVersion,
                            claim.ApiVersion,
                            claim.AppId,
                            claim.Token,
                            null,
                            true,
                            LinkingStatus),
                        ct);
                }
                else
                {
                    await UnsubscribeClaimedPageAsync(
                        new MessengerPageUnlinkClaim(
                            claim.PageRecordId,
                            claim.PageId,
                            claim.DisplayName,
                            claim.PageVersion,
                            claim.ApiVersion,
                            claim.AppId,
                            claim.Token,
                            null,
                            true,
                            UnlinkingStatus),
                        ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (FacebookMessengerAdminException exception)
            {
                logger.LogWarning(
                    "Messenger recovery kept page {PageRecordId} fenced with result {ErrorCode}",
                    candidate.Id,
                    exception.ErrorCode);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Messenger recovery failed for page {PageRecordId}",
                    candidate.Id);
            }
        }
        return recovered;
    }

    private async Task SettlePageAsync(
        MessengerPageSettlementClaim settlement,
        CancellationToken ct)
    {
        FacebookMessengerSubscriptionState state;
        try
        {
            state = await graph.GetSubscriptionAsync(
                settlement.ApiVersion,
                settlement.PageId,
                settlement.AppId,
                settlement.Token,
                ct);
        }
        catch (FacebookMessengerProviderException exception)
        {
            await ReturnSettlementToUncertainAsync(
                settlement,
                exception.ErrorCode,
                exception.ErrorCode == "MESSENGER_GRAPH_190" ? false : null,
                ct);
            return;
        }
        catch (HttpRequestException)
        {
            await ReturnSettlementToUncertainAsync(
                settlement,
                "MESSENGER_GRAPH_UNAVAILABLE",
                null,
                ct);
            return;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await ReturnSettlementToUncertainAsync(
                settlement,
                "MESSENGER_GRAPH_UNAVAILABLE",
                null,
                ct);
            return;
        }

        var checkedAt = DateTime.UtcNow;
        if (settlement.IsLink)
        {
            var complete = CompleteSubscription(state);
            await PersistClaimedLinkStateAsync(
                SettlementLinkClaim(settlement),
                new MessengerPageLinkOutcome(
                    true,
                    state.IsSubscribed,
                    complete ? ConnectedStatus : LinkUncertainStatus,
                    complete ? null : SubscriptionFailureCode(state),
                    complete,
                    complete ? "PageLinkSettled" : "PageLinkUncertain",
                    checkedAt),
                ct);
            return;
        }

        var unlinkClaim = SettlementUnlinkClaim(settlement);
        if (state.IsSubscribed)
        {
            await MarkClaimedUnlinkStateAsync(
                unlinkClaim,
                UnlinkUncertainStatus,
                "MESSENGER_UNSUBSCRIBE_NOT_CONFIRMED",
                true,
                "PageUnlinkUncertain",
                ct);
            return;
        }

        var confirmation = await PersistRemoteUnsubscribeConfirmedAsync(unlinkClaim, ct);
        if (confirmation is null) throw PageOperationInProgress();
        await FinalizeRemoteUnsubscribeAsync(confirmation, ct);
    }

    private async Task ReturnSettlementToUncertainAsync(
        MessengerPageSettlementClaim settlement,
        string errorCode,
        bool? tokenValid,
        CancellationToken ct)
    {
        if (settlement.IsLink)
        {
            await PersistClaimedLinkStateAsync(
                SettlementLinkClaim(settlement),
                new MessengerPageLinkOutcome(
                    tokenValid ?? true,
                    null,
                    LinkUncertainStatus,
                    errorCode,
                    false,
                    "PageLinkUncertain",
                    null),
                ct);
            return;
        }

        await MarkClaimedUnlinkStateAsync(
            SettlementUnlinkClaim(settlement),
            UnlinkUncertainStatus,
            errorCode,
            null,
            "PageUnlinkUncertain",
            ct,
            tokenValid);
    }

    private async Task<MessengerPageSettlementClaim?> TryBeginPageSettlementAsync(
        Guid pageRecordId,
        DateTime cutoff,
        DateTime claimedAtUtc,
        CancellationToken ct)
    {
        db.ClearTrackedChanges();
        try
        {
            await using var transaction = await db.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);
            var page = await db.LiveSupportMessengerPages.SingleOrDefaultAsync(
                candidate => candidate.Id == pageRecordId,
                ct);
            if (page is null || (page.UpdatedAt ?? page.CreatedAt) > cutoff)
                return null;
            var isLink = page.ConnectionStatus == LinkSettlingStatus;
            if (!isLink && page.ConnectionStatus != UnlinkSettlingStatus)
                return null;

            var settings = await RequireSettingsAsync(ct);
            var token = UnprotectPageToken(page);
            page.UpdatedAt = claimedAtUtc;
            page.Version++;
            AddPageAudit("PageSettlementClaimed", page.Id, null, new
            {
                page.PageId,
                Direction = isLink ? "Link" : "Unlink",
                ClaimedAtUtc = claimedAtUtc
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new MessengerPageSettlementClaim(
                page.Id,
                page.PageId,
                page.DisplayName,
                page.Version,
                settings.ApiVersion,
                settings.AppId,
                token,
                isLink);
        }
        catch (Exception exception) when (IsRetryablePersistenceConflict(exception))
        {
            db.ClearTrackedChanges();
            return null;
        }
    }

    private static MessengerPageLinkClaim SettlementLinkClaim(
        MessengerPageSettlementClaim settlement) =>
        new(
            settlement.PageRecordId,
            settlement.PageId,
            settlement.PageVersion,
            settlement.ApiVersion,
            settlement.AppId,
            settlement.Token,
            null,
            true,
            LinkSettlingStatus);

    private static MessengerPageUnlinkClaim SettlementUnlinkClaim(
        MessengerPageSettlementClaim settlement) =>
        new(
            settlement.PageRecordId,
            settlement.PageId,
            settlement.DisplayName,
            settlement.PageVersion,
            settlement.ApiVersion,
            settlement.AppId,
            settlement.Token,
            null,
            true,
            UnlinkSettlingStatus);

    private async Task<MessengerPageRecoveryClaim?> TryBeginPageRecoveryAsync(
        Guid pageRecordId,
        DateTime cutoff,
        DateTime claimedAtUtc,
        CancellationToken ct)
    {
        db.ClearTrackedChanges();
        try
        {
            await using var transaction = await db.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);
            var page = await db.LiveSupportMessengerPages.SingleOrDefaultAsync(
                candidate => candidate.Id == pageRecordId,
                ct);
            if (page is null || (page.UpdatedAt ?? page.CreatedAt) > cutoff)
                return null;
            var isLink = page.ConnectionStatus is LinkingStatus or LinkUncertainStatus;
            var isUnlink = page.ConnectionStatus is UnlinkingStatus or UnlinkUncertainStatus;
            if (!isLink && !isUnlink) return null;

            var settings = await RequireSettingsAsync(ct);
            var token = UnprotectPageToken(page);
            page.ConnectionStatus = isLink ? LinkingStatus : UnlinkingStatus;
            page.IsEnabled = false;
            page.IsSubscribed = null;
            page.LastErrorCode = null;
            page.UpdatedAt = claimedAtUtc;
            page.Version++;
            AddPageAudit("PageRecoveryClaimed", page.Id, null, new
            {
                page.PageId,
                Direction = isLink ? "Link" : "Unlink",
                ClaimedAtUtc = claimedAtUtc
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new MessengerPageRecoveryClaim(
                page.Id,
                page.PageId,
                page.DisplayName,
                page.Version,
                settings.ApiVersion,
                settings.AppId,
                token,
                isLink);
        }
        catch (Exception exception) when (IsRetryablePersistenceConflict(exception))
        {
            db.ClearTrackedChanges();
            return null;
        }
    }

    private async Task PersistPageStateAsync(
        MessengerPageStateChange state,
        CancellationToken ct)
    {
        state.Page.TokenValid = state.TokenValid;
        state.Page.IsSubscribed = state.Subscribed;
        state.Page.ConnectionStatus = state.Status;
        if (state.Status == "Connected") state.Page.IsEnabled = true;
        state.Page.LastErrorCode = SafeErrorCode(state.ErrorCode);
        state.Page.LastCredentialCheckAt = state.CredentialCheckedAt;
        state.Page.LastSubscriptionCheckAt = state.SubscriptionCheckedAt;
        state.Page.UpdatedAt = DateTime.UtcNow;
        state.Page.UpdatedByUserId = state.ActorUserId;
        state.Page.Version++;
        state.Settings.IsEnabled = HasApplicationCredentials(state.Settings) &&
            await db.LiveSupportMessengerPages.AnyAsync(candidate =>
                candidate.IsEnabled &&
                (candidate.Id == state.Page.Id
                    ? state.Status == "Connected"
                    : candidate.ConnectionStatus == "Connected"), ct);
        state.Settings.UpdatedAt = DateTime.UtcNow;
        state.Settings.UpdatedByUserId = state.ActorUserId;
        state.Settings.Version++;
        AddPageAudit("PageChecked", state.Page.Id, state.ActorUserId, new
        {
            state.Page.PageId,
            state.Status,
            state.TokenValid,
            state.Subscribed,
            ErrorCode = state.Page.LastErrorCode
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<FacebookMessengerAdminSettingsDto> ToSettingsDtoAsync(
        LiveSupportMessengerConfiguration settings,
        CancellationToken ct)
    {
        var pages = await PageDtosAsync(ct);
        return new FacebookMessengerAdminSettingsDto(
            Revision(settings.Version),
            settings.AppId,
            settings.AppSecretCiphertext is { Length: > 0 },
            settings.VerifyTokenCiphertext is { Length: > 0 },
            settings.ApiVersion,
            SupportedApiVersions,
            WebhookUrl(),
            pages);
    }

    private async Task<IReadOnlyList<FacebookMessengerAdminPageDto>> PageDtosAsync(CancellationToken ct)
    {
        var pages = await db.LiveSupportMessengerPages.AsNoTracking()
            .OrderBy(page => page.CreatedAt)
            .ToListAsync(ct);
        if (pages.Count == 0) return [];
        var pageIds = pages.Select(page => page.PageId).ToArray();
        var lastInbound = await db.LiveSupportMessengerBindings.AsNoTracking()
            .Where(binding => pageIds.Contains(binding.PageId))
            .GroupBy(binding => binding.PageId)
            .Select(group => new { PageId = group.Key, LastAt = group.Max(binding => binding.LastInboundAt) })
            .ToDictionaryAsync(row => row.PageId, row => (DateTime?)row.LastAt, StringComparer.Ordinal, ct);
        var lastOutbound = await db.LiveSupportMessengerMessages.AsNoTracking()
            .Where(message => pageIds.Contains(message.PageId) && message.Direction == "Outbound")
            .GroupBy(message => message.PageId)
            .Select(group => new { PageId = group.Key, LastAt = group.Max(message => message.CreatedAt) })
            .ToDictionaryAsync(row => row.PageId, row => (DateTime?)row.LastAt, StringComparer.Ordinal, ct);
        return pages.Select(page => ToPageDto(
                page,
                lastInbound.GetValueOrDefault(page.PageId),
                lastOutbound.GetValueOrDefault(page.PageId)))
            .ToArray();
    }

    private async Task<FacebookMessengerAdminPageDto> PageDtoAsync(Guid pageId, CancellationToken ct)
    {
        var page = await db.LiveSupportMessengerPages.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == pageId, ct);
        var lastInbound = await db.LiveSupportMessengerBindings.AsNoTracking()
            .Where(binding => binding.PageId == page.PageId)
            .MaxAsync(binding => (DateTime?)binding.LastInboundAt, ct);
        var lastOutbound = await db.LiveSupportMessengerMessages.AsNoTracking()
            .Where(message => message.PageId == page.PageId && message.Direction == "Outbound")
            .MaxAsync(message => (DateTime?)message.CreatedAt, ct);
        return ToPageDto(page, lastInbound, lastOutbound);
    }

    private static FacebookMessengerAdminPageDto ToPageDto(
        LiveSupportMessengerPage page,
        DateTime? lastInbound,
        DateTime? lastOutbound) =>
        new(
            page.Id.ToString(),
            page.PageId,
            page.DisplayName,
            page.PageAccessTokenCiphertext.Length > 0,
            page.HumanAgentEnabled,
            page.ConnectionStatus,
            page.TokenValid,
            page.IsSubscribed,
            Latest(page.LastCredentialCheckAt, page.LastSubscriptionCheckAt),
            lastInbound,
            lastOutbound,
            page.LastErrorCode);

    private IQueryable<LiveSupportMessengerConfiguration> SettingsQuery() =>
        db.LiveSupportMessengerConfigurations.AsNoTracking().Where(candidate =>
            candidate.ConfigurationKey == LiveSupportMessengerConfiguration.DefaultConfigurationKey);

    private async Task<LiveSupportMessengerConfiguration> RequireSettingsAsync(CancellationToken ct) =>
        await db.LiveSupportMessengerConfigurations.SingleOrDefaultAsync(candidate =>
            candidate.ConfigurationKey == LiveSupportMessengerConfiguration.DefaultConfigurationKey, ct)
        ?? throw new FacebookMessengerAdminException(
            "MESSENGER_SETTINGS_REQUIRED",
            "احفظ App ID وApp Secret أولًا.",
            StatusCodes.Status409Conflict);

    private async Task<LiveSupportMessengerPage> RequirePageAsync(Guid pageId, CancellationToken ct) =>
        await db.LiveSupportMessengerPages.SingleOrDefaultAsync(candidate => candidate.Id == pageId, ct)
        ?? throw new FacebookMessengerAdminException(
            "MESSENGER_PAGE_NOT_FOUND",
            "صفحة Messenger غير موجودة.",
            StatusCodes.Status404NotFound);

    private string UnprotectPageToken(LiveSupportMessengerPage page) =>
        UnprotectSecret(page.Id, "page-access-token", page.PageAccessTokenCiphertext);

    private string UnprotectApplicationSecret(LiveSupportMessengerConfiguration settings) =>
        UnprotectSecret(settings.Id, "app-secret", settings.AppSecretCiphertext);

    private string UnprotectSecret(Guid ownerId, string secretKind, byte[]? ciphertext)
    {
        try
        {
            return protector.Unprotect(ownerId, secretKind, ciphertext ?? []);
        }
        catch (CryptographicException)
        {
            throw new FacebookMessengerAdminException(
                "MESSENGER_SECRET_DECRYPTION_FAILED",
                "تعذر قراءة بيانات الربط المشفرة. راجع مفاتيح حماية البيانات.",
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task<bool> CanEnableAsync(
        LiveSupportMessengerConfiguration settings,
        CancellationToken ct) =>
        HasApplicationCredentials(settings) &&
        await db.LiveSupportMessengerPages.AnyAsync(page =>
            page.IsEnabled && page.ConnectionStatus == "Connected", ct);

    private Task<bool> HasOtherConnectedPageAsync(Guid pageId, CancellationToken ct) =>
        db.LiveSupportMessengerPages.AnyAsync(page =>
            page.Id != pageId &&
            page.IsEnabled &&
            page.ConnectionStatus == ConnectedStatus,
            ct);

    private static bool HasApplicationCredentials(LiveSupportMessengerConfiguration settings) =>
        !string.IsNullOrWhiteSpace(settings.AppId) &&
        settings.AppSecretCiphertext is { Length: > 0 } &&
        settings.VerifyTokenCiphertext is { Length: > 0 };

    private static void RequireApplicationReady(LiveSupportMessengerConfiguration settings)
    {
        if (!HasApplicationCredentials(settings))
            throw new FacebookMessengerAdminException(
                "MESSENGER_APPLICATION_CONFIGURATION_INCOMPLETE",
                "أكمل App ID وApp Secret وVerify Token قبل ربط الصفحات.",
                StatusCodes.Status409Conflict);
    }

    private static void EnsurePageOperationIsIdle(LiveSupportMessengerPage page)
    {
        if (page.ConnectionStatus is
            LinkingStatus or
            LinkUncertainStatus or
            LinkSettlingStatus or
            UnlinkingStatus or
            UnlinkUncertainStatus or
            UnlinkSettlingStatus or
            RemoteUnsubscribeConfirmedStatus)
            throw PageOperationInProgress();
    }

    private static void ValidatePageIdentity(FacebookMessengerPageIdentity identity)
    {
        if (identity.PageId.Length is 0 or > 64 || !identity.PageId.All(char.IsAsciiDigit))
            throw new FacebookMessengerAdminException(
                "MESSENGER_PAGE_ID_INVALID",
                "أعاد Meta معرّف صفحة غير صالح.",
                StatusCodes.Status422UnprocessableEntity);
    }

    private static bool CompleteSubscription(FacebookMessengerSubscriptionState state) =>
        state.IsSubscribed && FacebookMessengerSubscriptionContract.RequiredFields.All(required =>
            state.SubscribedFields.Contains(required, StringComparer.Ordinal));

    private static string SubscriptionFailureCode(FacebookMessengerSubscriptionState state) =>
        state.IsSubscribed
            ? "MESSENGER_SUBSCRIPTION_FIELDS_MISSING"
            : "MESSENGER_PAGE_NOT_SUBSCRIBED";

    private static string NormalizeAppId(string appId)
    {
        var normalized = appId?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length > 64 ||
            !normalized.All(char.IsAsciiDigit))
            throw new FacebookMessengerAdminException(
                "MESSENGER_APP_ID_INVALID",
                "App ID مطلوب ويجب أن يكون رقمًا صحيحًا.",
                StatusCodes.Status400BadRequest);
        return normalized!;
    }

    private static string NormalizeApiVersion(string apiVersion)
    {
        var normalized = apiVersion?.Trim();
        if (!SupportedApiVersions.Contains(normalized, StringComparer.Ordinal))
            throw new FacebookMessengerAdminException(
                "MESSENGER_API_VERSION_INVALID",
                "إصدار Graph API المحدد غير مدعوم.",
                StatusCodes.Status400BadRequest);
        return normalized!;
    }

    private static string? OptionalSecret(string? value, int maximumLength, string errorCode)
    {
        if (value is null) return null;
        return RequiredSecret(value, maximumLength, errorCode);
    }

    private static string RequiredSecret(string value, int maximumLength, string errorCode)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
            throw new FacebookMessengerAdminException(
                errorCode,
                "قيمة السر المطلوبة غير صالحة.",
                StatusCodes.Status400BadRequest);
        return normalized;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        var normalized = displayName.Trim();
        if (normalized.Length <= 120) return normalized;
        var safeLength = 120;
        if (char.IsHighSurrogate(normalized[safeLength - 1]) &&
            char.IsLowSurrogate(normalized[safeLength]))
            safeLength--;
        return normalized[..safeLength];
    }

    private static void RequireRevision(long actual, long expected)
    {
        if (expected < 0 || actual != expected) throw Conflict();
    }

    private static FacebookMessengerAdminException Conflict() =>
        new(
            "MESSENGER_CONFIGURATION_CONFLICT",
            "تم تعديل إعدادات Messenger. حدّث الصفحة ثم أعد المحاولة.",
            StatusCodes.Status409Conflict);

    private static FacebookMessengerAdminException PageOperationInProgress() =>
        new(
            "MESSENGER_PAGE_OPERATION_IN_PROGRESS",
            "هناك عملية ربط أو إلغاء ربط جارية لهذه الصفحة. حاول مرة أخرى بعد قليل.",
            StatusCodes.Status409Conflict);

    private static FacebookMessengerAdminException PageConcurrencyConflict() =>
        new(
            "MESSENGER_PAGE_CONCURRENCY_CONFLICT",
            "تغيرت حالة صفحة Messenger أثناء العملية. حدّث الصفحة ثم أعد المحاولة.",
            StatusCodes.Status409Conflict);

    private static FacebookMessengerAdminException ProviderError(string errorCode) =>
        new(
            SafeErrorCode(errorCode) ?? "MESSENGER_GRAPH_REQUEST_FAILED",
            "تعذر إكمال الطلب مع Meta. راجع صلاحيات التوكن وحالة التطبيق.",
            StatusCodes.Status422UnprocessableEntity);

    private static FacebookMessengerAdminException MetaUnavailable() =>
        new(
            "MESSENGER_GRAPH_UNAVAILABLE",
            "تعذر الوصول إلى Meta الآن. حاول مرة أخرى بعد قليل.",
            StatusCodes.Status503ServiceUnavailable);

    private static FacebookMessengerAdminException UnsubscribeUncertain() =>
        new(
            "MESSENGER_UNSUBSCRIBE_UNCERTAIN",
            "حالة إلغاء الاشتراك غير مؤكدة الآن، لذلك احتفظ النظام بإعداد الصفحة.",
            StatusCodes.Status503ServiceUnavailable);

    private static FacebookMessengerAdminException SubscriptionUncertain() =>
        new(
            "MESSENGER_SUBSCRIPTION_UNCERTAIN",
            "حالة ربط الصفحة غير مؤكدة الآن؛ سيواصل النظام المصالحة تلقائيًا.",
            StatusCodes.Status503ServiceUnavailable);

    private static string? SafeErrorCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var safe = new string(value
            .Where(character => char.IsAsciiLetterOrDigit(character) || character == '_')
            .Take(120)
            .ToArray());
        return safe.Length == 0 ? null : safe;
    }

    private void AddConfigurationAudit(
        string action,
        Guid entityId,
        Guid actorUserId,
        object values) =>
        AddAudit(action, nameof(LiveSupportMessengerConfiguration), entityId, actorUserId, values);

    private void AddPageAudit(
        string action,
        Guid entityId,
        Guid? actorUserId,
        object values) =>
        AddAudit(action, nameof(LiveSupportMessengerPage), entityId, actorUserId, values);

    private void AddAudit(
        string action,
        string entityType,
        Guid entityId,
        Guid? actorUserId,
        object values)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = $"FacebookMessenger.{action}",
            EntityType = entityType,
            EntityId = entityId,
            PerformedByUserId = actorUserId,
            ActorType = actorUserId.HasValue ? "User" : "System",
            NewValues = JsonSerializer.Serialize(values)
        });
    }

    private string WebhookUrl() =>
        applicationConfiguration["FacebookMessenger:WebhookPublicUrl"]?.Trim() is { Length: > 0 } configured
            ? configured
            : "/api/live-support/messenger/webhook";

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Revision(long revision) => revision.ToString(CultureInfo.InvariantCulture);

    private static DateTime? Latest(DateTime? first, DateTime? second) =>
        first.HasValue && second.HasValue
            ? first > second ? first : second
            : first ?? second;

    private static bool IsUniqueOrSerializationFailure(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres &&
        postgres.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure;

    private static bool IsRetryablePersistenceConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException) return true;
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException postgres &&
                postgres.SqlState == PostgresErrorCodes.SerializationFailure)
                return true;
        return false;
    }

    private sealed record MessengerPageOperation(
        LiveSupportMessengerPage Page,
        LiveSupportMessengerConfiguration Settings,
        string Token,
        string AppSecret,
        Guid ActorUserId,
        DateTime CheckedAt);

    private sealed record MessengerPageStateChange(
        LiveSupportMessengerPage Page,
        LiveSupportMessengerConfiguration Settings,
        bool? TokenValid,
        bool? Subscribed,
        string Status,
        string? ErrorCode,
        Guid ActorUserId,
        DateTime? CredentialCheckedAt,
        DateTime? SubscriptionCheckedAt);

    private sealed record MessengerPageLinkStart(
        Guid PageRecordId,
        MessengerPageLinkClaim? Claim);

    private sealed record MessengerPageLinkClaim(
        Guid PageRecordId,
        string PageId,
        long PageVersion,
        string ApiVersion,
        string AppId,
        string Token,
        Guid? ActorUserId,
        bool WasUncertain,
        string ExpectedStatus);

    private sealed record MessengerPageLinkOutcome(
        bool? TokenValid,
        bool? Subscribed,
        string Status,
        string? ErrorCode,
        bool IsEnabled,
        string AuditAction,
        DateTime? SubscriptionCheckedAt)
    {
        public static MessengerPageLinkOutcome Uncertain => new(
            true,
            null,
            LinkUncertainStatus,
            "MESSENGER_SUBSCRIPTION_UNCERTAIN",
            false,
            "PageLinkUncertain",
            null);
    }

    private sealed record MessengerPageUnlinkClaim(
        Guid PageRecordId,
        string PageId,
        string DisplayName,
        long PageVersion,
        string ApiVersion,
        string AppId,
        string Token,
        Guid? ActorUserId,
        bool WasUncertain,
        string ExpectedStatus);

    private sealed record MessengerRemoteUnsubscribeConfirmation(
        Guid PageRecordId,
        Guid? ActorUserId);

    private sealed record MessengerPageRecoveryClaim(
        Guid PageRecordId,
        string PageId,
        string DisplayName,
        long PageVersion,
        string ApiVersion,
        string AppId,
        string Token,
        bool IsLink);

    private sealed record MessengerPageSettlementClaim(
        Guid PageRecordId,
        string PageId,
        string DisplayName,
        long PageVersion,
        string ApiVersion,
        string AppId,
        string Token,
        bool IsLink);
}

public sealed class FacebookMessengerAdminException(
    string errorCode,
    string safeMessage,
    int statusCode) : Exception(safeMessage)
{
    public string ErrorCode { get; } = errorCode;
    public string SafeMessage { get; } = safeMessage;
    public int StatusCode { get; } = statusCode;
}
