using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class FacebookMessengerAdminConfigurationTests
{
    [Fact]
    public void ProtectedSecret_DifferentEntityOrPurposeCannotDecryptCiphertext()
    {
        var protector = new FacebookMessengerSecretProtector(
            new EphemeralDataProtectionProvider());
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var ciphertext = protector.Protect(ownerId, "app-secret", "top-secret");

        Assert.Equal("top-secret", protector.Unprotect(ownerId, "app-secret", ciphertext));
        Assert.Throws<CryptographicException>(() =>
            protector.Unprotect(otherOwnerId, "app-secret", ciphertext));
        Assert.Throws<CryptographicException>(() =>
            protector.Unprotect(ownerId, "verify-token", ciphertext));
    }

    [Fact]
    public async Task AdminSettingsGet_ReturnsFlagsWithoutApplicationOrPageSecrets()
    {
        await using var harness = await AdminHarness.CreateAsync(new RejectingHandler());
        var settings = Configuration(
            harness.Protector,
            version: 4,
            appSecret: "app-secret-value",
            verifyToken: "verify-token-value");
        var page = Page(
            harness.Protector,
            "101",
            "الصفحة الأولى",
            "page-token-value",
            "Connected");
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(page);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();
        var controller = new AdminFacebookMessengerController(harness.Service);

        var response = Assert.IsType<OkObjectResult>(
            await controller.GetSettings(CancellationToken.None));
        var json = JsonSerializer.Serialize(
            response.Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("app-secret-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("verify-token-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("page-token-value", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("appSecretConfigured").GetBoolean());
        Assert.True(data.GetProperty("verifyTokenConfigured").GetBoolean());
        Assert.False(data.TryGetProperty("appSecret", out _));
        Assert.False(data.TryGetProperty("verifyToken", out _));
        var pageData = Assert.Single(data.GetProperty("pages").EnumerateArray().ToArray());
        Assert.True(pageData.GetProperty("accessTokenConfigured").GetBoolean());
        Assert.Equal("Connected", pageData.GetProperty("connectionStatus").GetString());
        Assert.False(pageData.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task SettingsUpdateWithoutNewSecret_PreservesSecretAndAdvancesRotationRevision()
    {
        await using var harness = await AdminHarness.CreateAsync(new RejectingHandler());
        var initial = await harness.Service.UpdateSettingsAsync(
            new FacebookMessengerSettingsUpdate("123456", "v26.0", "app-secret-value", 0),
            harness.ActorUserId,
            CancellationToken.None);
        var initialCiphertext = await harness.Db.LiveSupportMessengerConfigurations
            .AsNoTracking()
            .Select(settings => settings.AppSecretCiphertext!)
            .SingleAsync();

        var rotation = await harness.Service.RotateVerifyTokenAsync(
            expectedRevision: 1,
            harness.ActorUserId,
            CancellationToken.None);
        var updated = await harness.Service.UpdateSettingsAsync(
            new FacebookMessengerSettingsUpdate("123456", "v26.0", null, 2),
            harness.ActorUserId,
            CancellationToken.None);
        harness.Db.ChangeTracker.Clear();
        var persisted = await harness.Db.LiveSupportMessengerConfigurations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal("1", initial.Revision);
        Assert.Equal("2", rotation.Revision);
        Assert.Equal("3", updated.Revision);
        Assert.True(initialCiphertext.SequenceEqual(persisted.AppSecretCiphertext!));
        Assert.Equal(
            "app-secret-value",
            harness.Protector.Unprotect(
                persisted.Id,
                "app-secret",
                persisted.AppSecretCiphertext!));
        Assert.Equal(
            rotation.VerifyToken,
            harness.Protector.Unprotect(
                persisted.Id,
                "verify-token",
                persisted.VerifyTokenCiphertext!));
        Assert.DoesNotContain("=", rotation.VerifyToken, StringComparison.Ordinal);
        Assert.True(rotation.VerifyToken.Length >= 64);
    }

    [Fact]
    public async Task CompletePageLink_PersistsEncryptedConnectedRuntimeWithoutExposingSecrets()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Massar Support"}"""),
            TokenInspectionResponse("123456", "101"),
            JsonResponse("""{"success":true}"""),
            JsonResponse("""
                {
                  "data": [{
                    "id": "123456",
                    "name": "Massar App",
                    "subscribed_fields": [
                      "messages",
                      "message_deliveries",
                      "message_reads",
                      "message_echoes"
                    ]
                  }]
                }
                """));
        await using var harness = await AdminHarness.CreateAsync(handler);
        await harness.Service.UpdateSettingsAsync(
            new FacebookMessengerSettingsUpdate("123456", "v26.0", "app-secret-value", 0),
            harness.ActorUserId,
            CancellationToken.None);
        var rotation = await harness.Service.RotateVerifyTokenAsync(
            expectedRevision: 1,
            harness.ActorUserId,
            CancellationToken.None);

        var linked = await harness.Service.LinkPageAsync(
            new FacebookMessengerPageLink("page-token-value", false, null, 2),
            harness.ActorUserId,
            CancellationToken.None);
        harness.Db.ChangeTracker.Clear();
        var persistedSettings = await harness.Db.LiveSupportMessengerConfigurations
            .AsNoTracking()
            .SingleAsync();
        var persistedPage = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();
        var getDto = await harness.Service.GetSettingsAsync(CancellationToken.None);
        var getJson = JsonSerializer.Serialize(
            getDto,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal("101", linked.PageId);
        Assert.True(linked.TokenValid);
        Assert.True(linked.Subscribed);
        Assert.True(persistedSettings.IsEnabled);
        Assert.Equal(4, persistedSettings.Version);
        Assert.Equal("Connected", persistedPage.ConnectionStatus);
        Assert.True(persistedPage.TokenValid);
        Assert.True(persistedPage.IsSubscribed);
        Assert.False(persistedPage.PageAccessTokenCiphertext.SequenceEqual(
            Encoding.UTF8.GetBytes("page-token-value")));
        Assert.Equal(
            "page-token-value",
            harness.Protector.Unprotect(
                persistedPage.Id,
                "page-access-token",
                persistedPage.PageAccessTokenCiphertext));
        Assert.True(Assert.Single(getDto.Pages).AccessTokenConfigured);
        Assert.DoesNotContain("app-secret-value", getJson, StringComparison.Ordinal);
        Assert.DoesNotContain(rotation.VerifyToken, getJson, StringComparison.Ordinal);
        Assert.DoesNotContain("page-token-value", getJson, StringComparison.Ordinal);
        var tokenInspectionRequest = handler.Requests[1];
        Assert.Equal(
            "https://graph.facebook.com/v26.0/debug_token?input_token=page-token-value",
            tokenInspectionRequest.Url);
        Assert.Equal("Bearer", tokenInspectionRequest.AuthorizationScheme);
        Assert.Equal(
            "123456|app-secret-value",
            tokenInspectionRequest.AuthorizationParameter);
        var auditEntityTypes = await harness.Db.AuditLogs.AsNoTracking()
            .Where(log => log.Action.StartsWith("FacebookMessenger."))
            .Select(log => new { log.Action, log.EntityType })
            .ToListAsync();
        Assert.All(
            auditEntityTypes.Where(log => log.Action is
                "FacebookMessenger.PageLinkStarted" or "FacebookMessenger.PageLinked"),
            log => Assert.Equal(nameof(LiveSupportMessengerPage), log.EntityType));
        Assert.Contains(auditEntityTypes, log =>
            log.Action == "FacebookMessenger.PageLinkStarted");
        Assert.Contains(auditEntityTypes, log =>
            log.Action == "FacebookMessenger.PageLinked");
        Assert.All(
            auditEntityTypes.Where(log => log.Action is
                "FacebookMessenger.SettingsUpdated" or "FacebookMessenger.VerifyTokenRotated"),
            log => Assert.Equal(nameof(LiveSupportMessengerConfiguration), log.EntityType));
    }

    [Fact]
    public async Task LinkingFourthDistinctPage_IsRejectedWithoutPersistingIt()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"404","name":"Fourth Page"}"""),
            TokenInspectionResponse("777", "404"));
        await using var harness = await AdminHarness.CreateAsync(handler);
        var settings = Configuration(
            harness.Protector,
            version: 9,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        foreach (var pageId in new[] { "101", "202", "303" })
            harness.Db.LiveSupportMessengerPages.Add(Page(
                harness.Protector,
                pageId,
                $"Page {pageId}",
                $"token-{pageId}",
                "Connected"));
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.LinkPageAsync(
                new FacebookMessengerPageLink("token-404", false, null, 9),
                harness.ActorUserId,
                CancellationToken.None));

        Assert.Equal("MESSENGER_PAGE_LIMIT_EXCEEDED", exception.ErrorCode);
        Assert.Equal(3, await harness.Db.LiveSupportMessengerPages.CountAsync());
        Assert.False(await harness.Db.LiveSupportMessengerPages.AnyAsync(page => page.PageId == "404"));
    }

    [Fact]
    public async Task DatabaseManagedRuntime_OverridesEnvironmentAndIncludesOnlyConnectedPages()
    {
        await using var harness = await AdminHarness.CreateAsync(new RejectingHandler());
        var settings = Configuration(
            harness.Protector,
            version: 2,
            appSecret: "db-app-secret",
            verifyToken: "db-verify-token");
        settings.AppId = "777";
        settings.IsEnabled = true;
        var connected = Page(
            harness.Protector,
            "101",
            "Connected Page",
            "db-page-token",
            "Connected");
        var pending = Page(
            harness.Protector,
            "202",
            "Pending Page",
            "pending-token",
            "TokenValid");
        var disabled = Page(
            harness.Protector,
            "303",
            "Disabled Page",
            "disabled-token",
            "Connected");
        disabled.IsEnabled = false;
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.AddRange(connected, pending, disabled);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();
        var reader = new FacebookMessengerRuntimeConfigurationReader(
            harness.Db,
            harness.Protector,
            EnvironmentConfiguration());

        var runtime = await reader.GetAsync(CancellationToken.None);

        Assert.True(runtime.IsDatabaseManaged);
        Assert.True(runtime.IsEnabled);
        Assert.Equal("777", runtime.AppId);
        Assert.Equal("db-app-secret", runtime.AppSecret);
        Assert.Equal("db-verify-token", runtime.VerifyToken);
        var page = Assert.Single(runtime.Pages.Values);
        Assert.Equal("101", page.PageId);
        Assert.Equal("db-page-token", page.AccessToken);
        Assert.False(runtime.TryGetPage("202", out _));
        Assert.False(runtime.TryGetPage("303", out _));
        Assert.False(runtime.TryGetPage("999", out _));
    }

    [Fact]
    public async Task DatabaseManagedRuntime_WithMissingApplicationSecret_FailsClosed()
    {
        await using var harness = await AdminHarness.CreateAsync(new RejectingHandler());
        var settings = Configuration(
            harness.Protector,
            version: 2,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        settings.AppSecretCiphertext = null;
        settings.IsEnabled = true;
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(Page(
            harness.Protector,
            "101",
            "Connected Page",
            "page-token",
            "Connected"));
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();
        var reader = new FacebookMessengerRuntimeConfigurationReader(
            harness.Db,
            harness.Protector,
            EnvironmentConfiguration());

        var runtime = await reader.GetAsync(CancellationToken.None);

        Assert.True(runtime.IsDatabaseManaged);
        Assert.False(runtime.IsEnabled);
        Assert.Empty(runtime.AppSecret);
        Assert.False(runtime.TryGetPage("101", out _));
    }

    [Fact]
    public async Task DeletePage_WhenMetaDeleteTimesOut_KeepsFenceUntilRecoveryRetriesDelete()
    {
        var handler = new DeleteTimeoutThenUnsubscribedHandler();
        await using var harness = await AdminHarness.CreateAsync(handler);
        var settings = Configuration(
            harness.Protector,
            version: 9,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        var page = Page(
            harness.Protector,
            "101",
            "Connected Page",
            "page-token",
            "Connected");
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(page);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.DeletePageAsync(
                page.Id,
                expectedRevision: 9,
                harness.ActorUserId,
                CancellationToken.None));
        harness.Db.ChangeTracker.Clear();
        var fenced = await harness.Db.LiveSupportMessengerPages.SingleAsync();
        var settingsAfterClaim = await harness.Db.LiveSupportMessengerConfigurations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal("MESSENGER_UNSUBSCRIBE_UNCERTAIN", exception.ErrorCode);
        Assert.Equal("UnlinkUncertain", fenced.ConnectionStatus);
        Assert.False(fenced.IsEnabled);
        Assert.Null(fenced.IsSubscribed);
        Assert.Equal(3, fenced.Version);
        Assert.Equal(10, settingsAfterClaim.Version);
        Assert.Equal([HttpMethod.Delete], handler.Methods);

        var repeatedDelete = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.DeletePageAsync(
                page.Id,
                expectedRevision: 10,
                harness.ActorUserId,
                CancellationToken.None));
        Assert.Equal("MESSENGER_PAGE_OPERATION_IN_PROGRESS", repeatedDelete.ErrorCode);
        Assert.Equal([HttpMethod.Delete], handler.Methods);

        var prematureRecovery = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow,
            CancellationToken.None);
        Assert.Equal(0, prematureRecovery);
        Assert.Equal([HttpMethod.Delete], handler.Methods);

        harness.Db.ChangeTracker.Clear();

        var recovered = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow +
            FacebookMessengerAdminService.OperationRecoveryDelay +
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var settlingPage = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();
        var settlingSettings = await harness.Service.GetSettingsAsync(CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal("UnlinkSettling", settlingPage.ConnectionStatus);
        Assert.False(settlingPage.IsEnabled);
        Assert.True(settlingPage.IsSubscribed);
        Assert.Equal("10", settlingSettings.Revision);
        Assert.Equal(
            [
                HttpMethod.Delete,
                HttpMethod.Delete,
                HttpMethod.Get
            ],
            handler.Methods);

        handler.ReleaseOriginalMutation();
        var relinkDuringSettlement = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.LinkPageAsync(
                new FacebookMessengerPageLink(
                    "replacement-token",
                    false,
                    page.Id,
                    10),
                harness.ActorUserId,
                CancellationToken.None));
        Assert.Equal("MESSENGER_PAGE_OPERATION_IN_PROGRESS", relinkDuringSettlement.ErrorCode);

        var deleteDuringSettlement = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.DeletePageAsync(
                page.Id,
                expectedRevision: 10,
                harness.ActorUserId,
                CancellationToken.None));
        Assert.Equal("MESSENGER_PAGE_OPERATION_IN_PROGRESS", deleteDuringSettlement.ErrorCode);
        Assert.Equal(5, handler.Methods.Count);

        var prematureSettlement = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow +
            FacebookMessengerAdminService.OperationRecoveryDelay +
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        Assert.Equal(0, prematureSettlement);
        Assert.Equal(5, handler.Methods.Count);

        var settled = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow +
            FacebookMessengerAdminService.OperationSettlementDelay +
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var finalSettings = await harness.Service.GetSettingsAsync(CancellationToken.None);

        Assert.Equal(1, settled);
        Assert.True(handler.OriginalMutationReleased);
        Assert.Empty(finalSettings.Pages);
        Assert.Equal("11", finalSettings.Revision);
        Assert.Empty(await harness.Db.LiveSupportMessengerPages.AsNoTracking().ToListAsync());
        Assert.Equal(
            [
                HttpMethod.Delete,
                HttpMethod.Delete,
                HttpMethod.Get,
                HttpMethod.Get,
                HttpMethod.Get,
                HttpMethod.Get
            ],
            handler.Methods);
    }

    [Fact]
    public async Task DeletePage_AfterRemoteConfirmation_RetriesLocalConcurrencyWithoutRepeatingMeta()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"success":true}"""),
            JsonResponse("""{"data":[]}"""));
        var interceptor = new FailFirstMessengerPageDeleteInterceptor();
        await using var harness = await AdminHarness.CreateAsync(handler, interceptor);
        var settings = Configuration(
            harness.Protector,
            version: 9,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        var page = Page(
            harness.Protector,
            "101",
            "Connected Page",
            "page-token",
            "Connected");
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(page);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var result = await harness.Service.DeletePageAsync(
            page.Id,
            expectedRevision: 9,
            harness.ActorUserId,
            CancellationToken.None);

        Assert.Empty(result.Pages);
        Assert.Equal("11", result.Revision);
        Assert.Equal(2, interceptor.DeleteSaveAttempts);
        Assert.Equal("RemoteUnsubscribeConfirmed", interceptor.StatusObservedBeforeFirstDelete);
        Assert.Equal(
            [HttpMethod.Delete, HttpMethod.Get],
            handler.Requests.Select(request => request.Method).ToArray());
    }

    [Fact]
    public async Task Recovery_FinalizesDurableRemoteConfirmationWithoutCallingMetaOrOldRevision()
    {
        await using var harness = await AdminHarness.CreateAsync(new RejectingHandler());
        var settings = Configuration(
            harness.Protector,
            version: 10,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        var page = Page(
            harness.Protector,
            "101",
            "Connected Page",
            "page-token",
            "RemoteUnsubscribeConfirmed");
        page.IsEnabled = false;
        page.IsSubscribed = false;
        page.Version = 37;
        page.UpdatedAt = DateTime.UtcNow;
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(page);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var recovered = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow,
            CancellationToken.None);
        var result = await harness.Service.GetSettingsAsync(CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Empty(result.Pages);
        Assert.Equal("11", result.Revision);
        Assert.Empty(await harness.Db.LiveSupportMessengerPages.AsNoTracking().ToListAsync());
        Assert.True(await harness.Db.AuditLogs.AsNoTracking().AnyAsync(log =>
            log.Action == "FacebookMessenger.PageUnlinked" &&
            log.EntityId == page.Id));
    }

    [Fact]
    public async Task AppIdChange_WithLinkedPages_IsRejectedBeforeLocalMutation()
    {
        await using var harness = await AdminHarness.CreateAsync(new RejectingHandler());
        var settings = Configuration(
            harness.Protector,
            version: 9,
            appSecret: "old-app-secret",
            verifyToken: "verify-token");
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(Page(
            harness.Protector,
            "101",
            "Connected Page",
            "page-token",
            "Connected"));
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.UpdateSettingsAsync(
                new FacebookMessengerSettingsUpdate("999", "v26.0", "new-app-secret", 9),
                harness.ActorUserId,
                CancellationToken.None));
        harness.Db.ChangeTracker.Clear();
        var persisted = await harness.Db.LiveSupportMessengerConfigurations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal("MESSENGER_UNLINK_PAGES_BEFORE_APP_CHANGE", exception.ErrorCode);
        Assert.Equal("777", persisted.AppId);
        Assert.Equal(9, persisted.Version);
        Assert.Equal(
            "old-app-secret",
            harness.Protector.Unprotect(
                persisted.Id,
                "app-secret",
                persisted.AppSecretCiphertext!));
    }

    [Fact]
    public async Task PageUnlinkClaim_BlocksConcurrentRelinkBeforeMetaMutation()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Connected Page"}"""),
            TokenInspectionResponse("777", "101"));
        await using var harness = await AdminHarness.CreateAsync(handler);
        var settings = Configuration(
            harness.Protector,
            version: 10,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        var page = Page(
            harness.Protector,
            "101",
            "Connected Page",
            "page-token",
            "Unlinking");
        page.IsEnabled = false;
        page.IsSubscribed = null;
        page.UpdatedAt = DateTime.UtcNow;
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(page);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.LinkPageAsync(
                new FacebookMessengerPageLink("new-page-token", false, page.Id, 10),
                harness.ActorUserId,
                CancellationToken.None));

        Assert.Equal("MESSENGER_PAGE_OPERATION_IN_PROGRESS", exception.ErrorCode);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Equal(
            "Unlinking",
            (await harness.Db.LiveSupportMessengerPages.AsNoTracking().SingleAsync())
                .ConnectionStatus);
    }

    [Fact]
    public async Task PageLinkClaim_IsDurableBeforeMetaPost_AndBlocksConcurrentDelete()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Massar Page"}"""),
            TokenInspectionResponse("777", "101"),
            JsonResponse("""{"success":true}"""),
            JsonResponse("""
                {
                  "data": [{
                    "id": "777",
                    "subscribed_fields": [
                      "messages",
                      "message_deliveries",
                      "message_reads",
                      "message_echoes"
                    ]
                  }]
                }
                """));
        await using var harness = await AdminHarness.CreateAsync(handler);
        harness.Db.LiveSupportMessengerConfigurations.Add(Configuration(
            harness.Protector,
            version: 9,
            appSecret: "app-secret",
            verifyToken: "verify-token"));
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        string? observedStatusAtPost = null;
        long? observedPageVersionAtPost = null;
        FacebookMessengerAdminException? blockedDelete = null;
        handler.OnRequestAsync = async (request, _) =>
        {
            if (request.Method != HttpMethod.Post) return;
            harness.Db.ChangeTracker.Clear();
            var claimedPage = await harness.Db.LiveSupportMessengerPages
                .AsNoTracking()
                .SingleAsync();
            observedStatusAtPost = claimedPage.ConnectionStatus;
            observedPageVersionAtPost = claimedPage.Version;
            try
            {
                await harness.Service.DeletePageAsync(
                    claimedPage.Id,
                    expectedRevision: 10,
                    harness.ActorUserId,
                    CancellationToken.None);
            }
            catch (FacebookMessengerAdminException exception)
            {
                blockedDelete = exception;
            }
        };

        var linked = await harness.Service.LinkPageAsync(
            new FacebookMessengerPageLink("page-token", false, null, 9),
            harness.ActorUserId,
            CancellationToken.None);
        var persistedSettings = await harness.Db.LiveSupportMessengerConfigurations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal("Linking", observedStatusAtPost);
        Assert.Equal(1, observedPageVersionAtPost);
        Assert.NotNull(blockedDelete);
        Assert.Equal("MESSENGER_PAGE_OPERATION_IN_PROGRESS", blockedDelete!.ErrorCode);
        Assert.Equal("101", linked.PageId);
        Assert.True(linked.Subscribed);
        Assert.Equal(11, persistedSettings.Version);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task LinkPage_WithReplacementTokenDuringLinkUncertain_ReachesLinkSettling()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Recovered Page"}"""),
            TokenInspectionResponse("777", "101"),
            JsonResponse("""{"success":true}"""),
            JsonResponse(CompleteSubscriptionResponse()));
        await using var harness = await AdminHarness.CreateAsync(handler);
        var settings = Configuration(
            harness.Protector,
            version: 11,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        var page = Page(
            harness.Protector,
            "101",
            "Old Page",
            "revoked-token",
            "LinkUncertain");
        page.IsEnabled = false;
        page.IsSubscribed = null;
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(page);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var result = await harness.Service.LinkPageAsync(
            new FacebookMessengerPageLink(
                "replacement-token",
                true,
                page.Id,
                11),
            harness.ActorUserId,
            CancellationToken.None);
        var persisted = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();
        var finalSettings = await harness.Service.GetSettingsAsync(CancellationToken.None);

        Assert.Equal("LinkSettling", result.ConnectionStatus);
        Assert.Equal("LinkSettling", persisted.ConnectionStatus);
        Assert.False(persisted.IsEnabled);
        Assert.True(persisted.IsSubscribed);
        Assert.Equal("Recovered Page", persisted.DisplayName);
        Assert.Equal(
            "replacement-token",
            harness.Protector.Unprotect(
                persisted.Id,
                "page-access-token",
                persisted.PageAccessTokenCiphertext));
        Assert.Equal("13", finalSettings.Revision);
        Assert.Equal(
            [HttpMethod.Get, HttpMethod.Get, HttpMethod.Post, HttpMethod.Get],
            handler.Requests.Select(request => request.Method).ToArray());
        Assert.Equal("replacement-token", handler.Requests[2].AuthorizationParameter);
    }

    [Fact]
    public async Task LinkPage_WithReplacementTokenDuringUnlinkUncertain_RefreshesOnlyThenWorkerDeletes()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Recovered Page"}"""),
            TokenInspectionResponse("777", "101"),
            JsonResponse("""{"success":true}"""),
            JsonResponse("""{"data":[]}"""));
        await using var harness = await AdminHarness.CreateAsync(handler);
        var settings = Configuration(
            harness.Protector,
            version: 10,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        var page = Page(
            harness.Protector,
            "101",
            "Old Page",
            "revoked-token",
            "UnlinkUncertain");
        page.IsEnabled = false;
        page.IsSubscribed = null;
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(page);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var refreshed = await harness.Service.LinkPageAsync(
            new FacebookMessengerPageLink(
                "replacement-token",
                false,
                page.Id,
                10),
            harness.ActorUserId,
            CancellationToken.None);
        var refreshedPage = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();
        var settingsAfterRefresh = await harness.Db.LiveSupportMessengerConfigurations
            .AsNoTracking()
            .SingleAsync();
        var refreshAudit = await harness.Db.AuditLogs
            .AsNoTracking()
            .SingleAsync(log =>
                log.Action == "FacebookMessenger.PageUnlinkCredentialRefreshed");

        Assert.Equal("UnlinkUncertain", refreshed.ConnectionStatus);
        Assert.Equal("UnlinkUncertain", refreshedPage.ConnectionStatus);
        Assert.False(refreshedPage.IsEnabled);
        Assert.Equal(
            "replacement-token",
            harness.Protector.Unprotect(
                refreshedPage.Id,
                "page-access-token",
                refreshedPage.PageAccessTokenCiphertext));
        Assert.Equal(11, settingsAfterRefresh.Version);
        Assert.DoesNotContain(
            "replacement-token",
            refreshAudit.NewValues ?? string.Empty,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "revoked-token",
            refreshAudit.NewValues ?? string.Empty,
            StringComparison.Ordinal);
        Assert.Equal([HttpMethod.Get, HttpMethod.Get],
            handler.Requests.Select(request => request.Method).ToArray());

        var recovered = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow +
            FacebookMessengerAdminService.OperationRecoveryDelay +
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var settling = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();
        var finalSettings = await harness.Service.GetSettingsAsync(CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal("UnlinkSettling", settling.ConnectionStatus);
        Assert.False(settling.IsEnabled);
        Assert.Equal("11", finalSettings.Revision);
        Assert.Equal(
            [HttpMethod.Get, HttpMethod.Get, HttpMethod.Delete, HttpMethod.Get],
            handler.Requests.Select(request => request.Method).ToArray());
        Assert.Equal("replacement-token", handler.Requests[2].AuthorizationParameter);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData("Linking")]
    [InlineData("LinkSettling")]
    [InlineData("Unlinking")]
    [InlineData("UnlinkSettling")]
    [InlineData("RemoteUnsubscribeConfirmed")]
    public async Task LinkPage_WithReplacementTokenDuringActiveOperation_RemainsBlocked(
        string connectionStatus)
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Recovered Page"}"""),
            TokenInspectionResponse("777", "101"));
        await using var harness = await AdminHarness.CreateAsync(handler);
        var settings = Configuration(
            harness.Protector,
            version: 9,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        var page = Page(
            harness.Protector,
            "101",
            "Old Page",
            "original-token",
            connectionStatus);
        page.IsEnabled = false;
        page.IsSubscribed = connectionStatus == "RemoteUnsubscribeConfirmed" ? false : null;
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(page);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.LinkPageAsync(
                new FacebookMessengerPageLink(
                    "replacement-token",
                    false,
                    page.Id,
                    9),
                harness.ActorUserId,
                CancellationToken.None));
        var persisted = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();
        var persistedSettings = await harness.Db.LiveSupportMessengerConfigurations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal("MESSENGER_PAGE_OPERATION_IN_PROGRESS", exception.ErrorCode);
        Assert.Equal(connectionStatus, persisted.ConnectionStatus);
        Assert.Equal(
            "original-token",
            harness.Protector.Unprotect(
                persisted.Id,
                "page-access-token",
                persisted.PageAccessTokenCiphertext));
        Assert.Equal(9, persistedSettings.Version);
        Assert.Equal([HttpMethod.Get, HttpMethod.Get],
            handler.Requests.Select(request => request.Method).ToArray());
    }

    [Fact]
    public async Task GraphPageLink_UsesBearerTokenAndVerifiesRequiredSubscriptionFields()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Massar Page"}"""),
            JsonResponse("""{"success":true}"""),
            JsonResponse("""
                {
                  "data": [{
                    "id": "777",
                    "name": "Massar App",
                    "subscribed_fields": [
                      "message_reads",
                      "messages",
                      "message_echoes",
                      "message_deliveries"
                    ]
                  }]
                }
                """));
        using var downloader = new FacebookMessengerSafeMediaDownloader(new RejectingHandler());
        using var httpClient = new HttpClient(handler);
        var graph = new FacebookMessengerGraphClient(
            httpClient,
            FixedFacebookMessengerRuntimeConfigurationReader.FromEnvironment(
                EnvironmentConfiguration()),
            downloader,
            NullLogger<FacebookMessengerGraphClient>.Instance);

        var identity = await graph.InspectPageTokenAsync(
            "v26.0", "page-token", CancellationToken.None);
        var subscription = await graph.SubscribePageAsync(
            "v26.0", "101", "777", "page-token", CancellationToken.None);

        Assert.Equal(new FacebookMessengerPageIdentity("101", "Massar Page"), identity);
        Assert.True(subscription.IsSubscribed);
        Assert.Equal(
            ["message_deliveries", "message_echoes", "message_reads", "messages"],
            subscription.SubscribedFields);
        Assert.Collection(
            handler.Requests,
            inspect =>
            {
                Assert.Equal(HttpMethod.Get, inspect.Method);
                Assert.Equal(
                    "https://graph.facebook.com/v26.0/me?fields=id,name",
                    inspect.Url);
                Assert.Equal("Bearer", inspect.AuthorizationScheme);
                Assert.Equal("page-token", inspect.AuthorizationParameter);
            },
            subscribe =>
            {
                Assert.Equal(HttpMethod.Post, subscribe.Method);
                Assert.Equal(
                    "https://graph.facebook.com/v26.0/101/subscribed_apps?subscribed_fields=messages,message_deliveries,message_reads,message_echoes",
                    subscribe.Url);
                Assert.Equal("Bearer", subscribe.AuthorizationScheme);
                Assert.Equal("page-token", subscribe.AuthorizationParameter);
            },
            check =>
            {
                Assert.Equal(HttpMethod.Get, check.Method);
                Assert.Equal(
                    "https://graph.facebook.com/v26.0/101/subscribed_apps?fields=id,name,subscribed_fields&limit=100",
                    check.Url);
                Assert.Equal("Bearer", check.AuthorizationScheme);
                Assert.Equal("page-token", check.AuthorizationParameter);
            });
    }

    [Fact]
    public async Task TokenInspection_RequiredScopesTargetedToDifferentPageAreRejected()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Massar Page"}"""),
            GranularTokenInspectionResponse("777", "101", "202"));
        using var downloader = new FacebookMessengerSafeMediaDownloader(new RejectingHandler());
        using var httpClient = new HttpClient(handler);
        var graph = new FacebookMessengerGraphClient(
            httpClient,
            FixedFacebookMessengerRuntimeConfigurationReader.FromEnvironment(
                EnvironmentConfiguration()),
            downloader,
            NullLogger<FacebookMessengerGraphClient>.Instance);

        var exception = await Assert.ThrowsAsync<FacebookMessengerProviderException>(() =>
            graph.InspectPageTokenForAppAsync(
                "v26.0",
                "777",
                "app-secret",
                "page-token",
                CancellationToken.None));

        Assert.Equal("MESSENGER_PAGE_TOKEN_PERMISSIONS_MISSING", exception.ErrorCode);
    }

    [Fact]
    public async Task TokenInspection_RequiredScopesTargetedToCurrentPageAreAccepted()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Massar Page"}"""),
            GranularTokenInspectionResponse("777", "101", "101"));
        using var downloader = new FacebookMessengerSafeMediaDownloader(new RejectingHandler());
        using var httpClient = new HttpClient(handler);
        var graph = new FacebookMessengerGraphClient(
            httpClient,
            FixedFacebookMessengerRuntimeConfigurationReader.FromEnvironment(
                EnvironmentConfiguration()),
            downloader,
            NullLogger<FacebookMessengerGraphClient>.Instance);

        var identity = await graph.InspectPageTokenForAppAsync(
            "v26.0",
            "777",
            "app-secret",
            "page-token",
            CancellationToken.None);

        Assert.Equal(new FacebookMessengerPageIdentity("101", "Massar Page"), identity);
    }

    [Fact]
    public async Task LinkRecovery_QuarantinesDelayedOriginalMutationUntilReadOnlySettlement()
    {
        var handler = new DelayedLinkMutationHandler();
        await using var harness = await AdminHarness.CreateAsync(handler);
        harness.Db.LiveSupportMessengerConfigurations.Add(Configuration(
            harness.Protector,
            version: 9,
            appSecret: "app-secret",
            verifyToken: "verify-token"));
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.LinkPageAsync(
                new FacebookMessengerPageLink("page-token", false, null, 9),
                harness.ActorUserId,
                CancellationToken.None));
        harness.Db.ChangeTracker.Clear();
        var fenced = await harness.Db.LiveSupportMessengerPages.SingleAsync();

        Assert.Equal("MESSENGER_SUBSCRIPTION_UNCERTAIN", exception.ErrorCode);
        Assert.Equal("LinkUncertain", fenced.ConnectionStatus);
        Assert.False(fenced.IsEnabled);
        Assert.Null(fenced.IsSubscribed);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal([HttpMethod.Get, HttpMethod.Get, HttpMethod.Post],
            handler.Requests.Select(request => request.Method).ToArray());

        var deleteWhileFenced = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.DeletePageAsync(
                fenced.Id,
                expectedRevision: 11,
                harness.ActorUserId,
                CancellationToken.None));
        Assert.Equal("MESSENGER_PAGE_OPERATION_IN_PROGRESS", deleteWhileFenced.ErrorCode);
        Assert.Equal(3, handler.Requests.Count);

        harness.Db.ChangeTracker.Clear();

        var recovered = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow +
            FacebookMessengerAdminService.OperationRecoveryDelay +
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var settling = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();
        var settlingSettings = await harness.Service.GetSettingsAsync(CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal("LinkSettling", settling.ConnectionStatus);
        Assert.False(settling.IsEnabled);
        Assert.False(settling.IsSubscribed);
        Assert.Equal("12", settlingSettings.Revision);
        Assert.Equal(
            [HttpMethod.Get, HttpMethod.Get, HttpMethod.Post, HttpMethod.Post, HttpMethod.Get],
            handler.Requests.Select(request => request.Method).ToArray());

        handler.ReleaseOriginalMutation();
        var deleteDuringSettlement = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.DeletePageAsync(
                settling.Id,
                expectedRevision: 12,
                harness.ActorUserId,
                CancellationToken.None));
        Assert.Equal("MESSENGER_PAGE_OPERATION_IN_PROGRESS", deleteDuringSettlement.ErrorCode);
        Assert.Equal(5, handler.Requests.Count);

        var prematureSettlement = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow +
            FacebookMessengerAdminService.OperationRecoveryDelay +
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        Assert.Equal(0, prematureSettlement);
        Assert.Equal(5, handler.Requests.Count);

        var settled = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow +
            FacebookMessengerAdminService.OperationSettlementDelay +
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var connected = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();
        var finalSettings = await harness.Service.GetSettingsAsync(CancellationToken.None);

        Assert.Equal(1, settled);
        Assert.True(handler.OriginalMutationReleased);
        Assert.Equal("Connected", connected.ConnectionStatus);
        Assert.True(connected.IsEnabled);
        Assert.True(connected.IsSubscribed);
        Assert.Equal("13", finalSettings.Revision);
        Assert.Equal(
            [
                HttpMethod.Get,
                HttpMethod.Get,
                HttpMethod.Post,
                HttpMethod.Post,
                HttpMethod.Get,
                HttpMethod.Get
            ],
            handler.Requests.Select(request => request.Method).ToArray());
    }

    [Fact]
    public async Task LinkPage_WhenAcceptedMutationCheckIsIncomplete_KeepsLinkFence()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Massar Page"}"""),
            TokenInspectionResponse("777", "101"),
            JsonResponse("""{"success":true}"""),
            JsonResponse("""
                {
                  "data": [{
                    "id": "777",
                    "subscribed_fields": ["messages"]
                  }]
                }
                """));
        await using var harness = await AdminHarness.CreateAsync(handler);
        harness.Db.LiveSupportMessengerConfigurations.Add(Configuration(
            harness.Protector,
            version: 9,
            appSecret: "app-secret",
            verifyToken: "verify-token"));
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.LinkPageAsync(
                new FacebookMessengerPageLink("page-token", false, null, 9),
                harness.ActorUserId,
                CancellationToken.None));
        var page = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal("MESSENGER_SUBSCRIPTION_UNCERTAIN", exception.ErrorCode);
        Assert.Equal("LinkUncertain", page.ConnectionStatus);
        Assert.True(page.IsSubscribed);
        Assert.False(page.IsEnabled);
        Assert.Equal("MESSENGER_SUBSCRIPTION_FIELDS_MISSING", page.LastErrorCode);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task LinkSettlement_WhenReadIsIncomplete_ReturnsToUncertainWithoutMutation()
    {
        var handler = new SequenceHandler(JsonResponse("""{"data":[]}"""));
        await using var harness = await AdminHarness.CreateAsync(handler);
        var settings = Configuration(
            harness.Protector,
            version: 12,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        var page = Page(
            harness.Protector,
            "101",
            "Massar Page",
            "page-token",
            "LinkSettling");
        page.IsEnabled = false;
        page.IsSubscribed = false;
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(page);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var recovered = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow +
            FacebookMessengerAdminService.OperationSettlementDelay +
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var persisted = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();
        var finalSettings = await harness.Service.GetSettingsAsync(CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal("LinkUncertain", persisted.ConnectionStatus);
        Assert.False(persisted.IsEnabled);
        Assert.False(persisted.IsSubscribed);
        Assert.Equal("MESSENGER_PAGE_NOT_SUBSCRIBED", persisted.LastErrorCode);
        Assert.Equal("13", finalSettings.Revision);
        Assert.Equal([HttpMethod.Get], handler.Requests.Select(request => request.Method).ToArray());
    }

    [Fact]
    public async Task UnlinkSettlement_WhenReadIsStillSubscribed_ReturnsToUncertainWithoutMutation()
    {
        var handler = new SequenceHandler(JsonResponse(CompleteSubscriptionResponse()));
        await using var harness = await AdminHarness.CreateAsync(handler);
        var settings = Configuration(
            harness.Protector,
            version: 10,
            appSecret: "app-secret",
            verifyToken: "verify-token");
        var page = Page(
            harness.Protector,
            "101",
            "Massar Page",
            "page-token",
            "UnlinkSettling");
        page.IsEnabled = false;
        page.IsSubscribed = true;
        harness.Db.LiveSupportMessengerConfigurations.Add(settings);
        harness.Db.LiveSupportMessengerPages.Add(page);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var recovered = await harness.Service.RecoverStalePageOperationsAsync(
            DateTime.UtcNow +
            FacebookMessengerAdminService.OperationSettlementDelay +
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var persisted = await harness.Db.LiveSupportMessengerPages
            .AsNoTracking()
            .SingleAsync();
        var finalSettings = await harness.Service.GetSettingsAsync(CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal("UnlinkUncertain", persisted.ConnectionStatus);
        Assert.False(persisted.IsEnabled);
        Assert.True(persisted.IsSubscribed);
        Assert.Equal("MESSENGER_UNSUBSCRIBE_NOT_CONFIRMED", persisted.LastErrorCode);
        Assert.Equal("10", finalSettings.Revision);
        Assert.Equal([HttpMethod.Get], handler.Requests.Select(request => request.Method).ToArray());
    }

    [Fact]
    public async Task LinkPage_WhenMetaMarksErrorTransient_ReturnsServiceUnavailableWithoutSavingPage()
    {
        var handler = new SequenceHandler(JsonResponse(
            """{"error":{"code":200,"is_transient":true}}""",
            HttpStatusCode.BadRequest));
        await using var harness = await AdminHarness.CreateAsync(handler);
        harness.Db.LiveSupportMessengerConfigurations.Add(Configuration(
            harness.Protector,
            version: 9,
            appSecret: "app-secret",
            verifyToken: "verify-token"));
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.LinkPageAsync(
                new FacebookMessengerPageLink("page-token", false, null, 9),
                harness.ActorUserId,
                CancellationToken.None));

        Assert.Equal("MESSENGER_GRAPH_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(503, exception.StatusCode);
        Assert.Empty(await harness.Db.LiveSupportMessengerPages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task LinkPage_WhenTokenBelongsToDifferentApp_IsRejectedBeforeSavingPage()
    {
        var handler = new SequenceHandler(
            JsonResponse("""{"id":"101","name":"Massar Page"}"""),
            TokenInspectionResponse("999", "101"));
        await using var harness = await AdminHarness.CreateAsync(handler);
        harness.Db.LiveSupportMessengerConfigurations.Add(Configuration(
            harness.Protector,
            version: 9,
            appSecret: "app-secret",
            verifyToken: "verify-token"));
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<FacebookMessengerAdminException>(() =>
            harness.Service.LinkPageAsync(
                new FacebookMessengerPageLink("page-token", false, null, 9),
                harness.ActorUserId,
                CancellationToken.None));

        Assert.Equal("MESSENGER_PAGE_TOKEN_APP_MISMATCH", exception.ErrorCode);
        Assert.Empty(await harness.Db.LiveSupportMessengerPages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public void SensitiveAdminEndpoints_RequireAdminRoleAndDisableHttpLogging()
    {
        var controllerType = typeof(AdminFacebookMessengerController);
        var authorization = Assert.IsType<AuthorizeAttribute>(Assert.Single(
            controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)));

        Assert.Equal("Admin", authorization.Roles);
        foreach (var methodName in new[]
                 {
                     nameof(AdminFacebookMessengerController.UpdateSettings),
                     nameof(AdminFacebookMessengerController.LinkPage),
                     nameof(AdminFacebookMessengerController.RotateVerifyToken)
                 })
        {
            var method = controllerType.GetMethod(methodName);
            Assert.NotNull(method);
            var logging = Assert.IsType<HttpLoggingAttribute>(Assert.Single(
                method!.GetCustomAttributes(typeof(HttpLoggingAttribute), inherit: true)));
            Assert.Equal(HttpLoggingFields.None, logging.LoggingFields);
        }
    }

    private static LiveSupportMessengerConfiguration Configuration(
        IFacebookMessengerSecretProtector protector,
        long version,
        string appSecret,
        string verifyToken)
    {
        var settings = new LiveSupportMessengerConfiguration
        {
            AppId = "777",
            ApiVersion = "v26.0",
            Version = version
        };
        settings.AppSecretCiphertext = protector.Protect(settings.Id, "app-secret", appSecret);
        settings.VerifyTokenCiphertext = protector.Protect(settings.Id, "verify-token", verifyToken);
        return settings;
    }

    private static LiveSupportMessengerPage Page(
        IFacebookMessengerSecretProtector protector,
        string pageId,
        string displayName,
        string accessToken,
        string status)
    {
        var page = new LiveSupportMessengerPage
        {
            PageId = pageId,
            DisplayName = displayName,
            ConnectionStatus = status,
            IsEnabled = true,
            TokenValid = true,
            IsSubscribed = status == "Connected",
            Version = 1
        };
        page.PageAccessTokenCiphertext = protector.Protect(
            page.Id,
            "page-access-token",
            accessToken);
        return page;
    }

    private static FacebookMessengerConfiguration EnvironmentConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["FacebookMessenger:VerifyToken"] = "env-verify-token",
            ["FacebookMessenger:AppSecret"] = "env-app-secret",
            ["FacebookMessenger:ApiVersion"] = "v25.0",
            ["FacebookMessenger:Pages:0:PageId"] = "999",
            ["FacebookMessenger:Pages:0:DisplayName"] = "Environment Page",
            ["FacebookMessenger:Pages:0:AccessToken"] = "env-page-token"
        };
        return new FacebookMessengerConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage TokenInspectionResponse(string appId, string pageId) =>
        JsonResponse($$"""
            {
              "data": {
                "is_valid": true,
                "app_id": "{{appId}}",
                "type": "PAGE",
                "profile_id": "{{pageId}}",
                "scopes": ["pages_messaging", "pages_manage_metadata"]
              }
            }
            """);

    private static HttpResponseMessage GranularTokenInspectionResponse(
        string appId,
        string pageId,
        string targetPageId) =>
        JsonResponse($$"""
            {
              "data": {
                "is_valid": true,
                "app_id": "{{appId}}",
                "type": "PAGE",
                "profile_id": "{{pageId}}",
                "scopes": ["public_profile"],
                "granular_scopes": [
                  {
                    "scope": "pages_messaging",
                    "target_ids": ["{{targetPageId}}"]
                  },
                  {
                    "scope": "pages_manage_metadata",
                    "target_ids": ["{{targetPageId}}"]
                  }
                ]
              }
            }
            """);

    private static string CompleteSubscriptionResponse() =>
        """
        {
          "data": [{
            "id": "777",
            "subscribed_fields": [
              "messages",
              "message_deliveries",
              "message_reads",
              "message_echoes"
            ]
          }]
        }
        """;

    private sealed class AdminHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly HttpClient _httpClient;
        private readonly FacebookMessengerSafeMediaDownloader _downloader;

        private AdminHarness(
            SqliteConnection connection,
            HttpClient httpClient,
            FacebookMessengerSafeMediaDownloader downloader,
            AppDbContext db,
            IFacebookMessengerSecretProtector protector,
            FacebookMessengerAdminService service,
            Guid actorUserId)
        {
            _connection = connection;
            _httpClient = httpClient;
            _downloader = downloader;
            Db = db;
            Protector = protector;
            Service = service;
            ActorUserId = actorUserId;
        }

        public AppDbContext Db { get; }
        public IFacebookMessengerSecretProtector Protector { get; }
        public FacebookMessengerAdminService Service { get; }
        public Guid ActorUserId { get; }

        public static async Task<AdminHarness> CreateAsync(
            HttpMessageHandler handler,
            SaveChangesInterceptor? interceptor = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            var db = new AppDbContext(
                options.Options);
            await db.Database.EnsureCreatedAsync();
            var actor = new User
            {
                FullName = "Messenger Admin",
                PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}",
                PasswordHash = "test-only"
            };
            db.Users.Add(actor);
            await db.SaveChangesAsync();
            var protector = new FacebookMessengerSecretProtector(
                new EphemeralDataProtectionProvider());
            var environment = EnvironmentConfiguration();
            var runtimeReader =
                FixedFacebookMessengerRuntimeConfigurationReader.FromEnvironment(environment);
            var downloader = new FacebookMessengerSafeMediaDownloader(new RejectingHandler());
            var httpClient = new HttpClient(handler);
            var graph = new FacebookMessengerGraphClient(
                httpClient,
                runtimeReader,
                downloader,
                NullLogger<FacebookMessengerGraphClient>.Instance);
            var applicationConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FacebookMessenger:WebhookPublicUrl"] =
                        "https://api.example.test/api/live-support/messenger/webhook"
                })
                .Build();
            var service = new FacebookMessengerAdminService(
                db,
                protector,
                graph,
                applicationConfiguration,
                NullLogger<FacebookMessengerAdminService>.Instance);
            return new AdminHarness(
                connection,
                httpClient,
                downloader,
                db,
                protector,
                service,
                actor.Id);
        }

        public async ValueTask DisposeAsync()
        {
            _httpClient.Dispose();
            _downloader.Dispose();
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        string Url,
        string? AuthorizationScheme,
        string? AuthorizationParameter);

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<RecordedRequest> Requests { get; } = [];
        public Func<RecordedRequest, CancellationToken, Task>? OnRequestAsync { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var recorded = new RecordedRequest(
                request.Method,
                request.RequestUri!.AbsoluteUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter);
            Requests.Add(recorded);
            if (OnRequestAsync is not null)
                await OnRequestAsync(recorded, cancellationToken);
            if (_responses.Count == 0)
                throw new InvalidOperationException("Unexpected Meta Graph request.");
            return _responses.Dequeue();
        }
    }

    private sealed class RejectingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No network request expected.");
    }

    private sealed class DelayedLinkMutationHandler : HttpMessageHandler
    {
        private int _postAttempts;
        private bool _remoteSubscribed;
        private bool _originalMutationPending;

        public List<RecordedRequest> Requests { get; } = [];
        public bool OriginalMutationReleased { get; private set; }

        public void ReleaseOriginalMutation()
        {
            Assert.True(_originalMutationPending);
            _remoteSubscribed = true;
            _originalMutationPending = false;
            OriginalMutationReleased = true;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.AbsoluteUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter));
            if (request.Method == HttpMethod.Post)
            {
                _postAttempts++;
                if (_postAttempts == 1)
                {
                    _originalMutationPending = true;
                    return Task.FromResult(JsonResponse("""{"unexpected":true}"""));
                }
                if (_postAttempts == 2)
                    return Task.FromResult(JsonResponse("""{"success":true}"""));
                throw new InvalidOperationException("Unexpected additional Messenger subscribe.");
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/me", StringComparison.Ordinal))
                return Task.FromResult(JsonResponse(
                    """{"id":"101","name":"Massar Page"}"""));
            if (request.RequestUri.AbsolutePath.EndsWith("/debug_token", StringComparison.Ordinal))
                return Task.FromResult(TokenInspectionResponse("777", "101"));
            return Task.FromResult(_remoteSubscribed
                ? JsonResponse(CompleteSubscriptionResponse())
                : JsonResponse("""{"data":[]}"""));
        }
    }

    private sealed class DeleteTimeoutThenUnsubscribedHandler : HttpMessageHandler
    {
        private int _deleteAttempts;
        private bool _remoteSubscribed = true;
        private bool _originalMutationPending;

        public List<HttpMethod> Methods { get; } = [];
        public bool OriginalMutationReleased { get; private set; }

        public void ReleaseOriginalMutation()
        {
            Assert.True(_originalMutationPending);
            _remoteSubscribed = false;
            _originalMutationPending = false;
            OriginalMutationReleased = true;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            if (request.Method == HttpMethod.Delete && ++_deleteAttempts == 1)
            {
                _originalMutationPending = true;
                throw new HttpRequestException("Simulated uncertain Meta DELETE.");
            }
            if (request.Method == HttpMethod.Delete)
                return Task.FromResult(JsonResponse("""{"success":true}"""));
            if (request.RequestUri!.AbsolutePath.EndsWith("/me", StringComparison.Ordinal))
                return Task.FromResult(JsonResponse(
                    """{"id":"101","name":"Connected Page"}"""));
            if (request.RequestUri.AbsolutePath.EndsWith("/debug_token", StringComparison.Ordinal))
                return Task.FromResult(TokenInspectionResponse("777", "101"));
            return Task.FromResult(_remoteSubscribed
                ? JsonResponse(CompleteSubscriptionResponse())
                : JsonResponse("""{"data":[]}"""));
        }
    }

    private sealed class FailFirstMessengerPageDeleteInterceptor : SaveChangesInterceptor
    {
        public int DeleteSaveAttempts { get; private set; }
        public string? StatusObservedBeforeFirstDelete { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var deletedPage = eventData.Context?.ChangeTracker
                .Entries<LiveSupportMessengerPage>()
                .SingleOrDefault(entry => entry.State == EntityState.Deleted);
            if (deletedPage is not null)
            {
                DeleteSaveAttempts++;
                if (DeleteSaveAttempts == 1)
                {
                    StatusObservedBeforeFirstDelete = deletedPage.Entity.ConnectionStatus;
                    throw new DbUpdateConcurrencyException(
                        "Simulated settings concurrency after Meta unsubscribe confirmation.");
                }
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
