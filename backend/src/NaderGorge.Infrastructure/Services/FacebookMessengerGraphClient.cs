using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NaderGorge.Infrastructure.Services;

public sealed record FacebookMessengerSendReceipt(
    string RecipientPsid,
    string ProviderMessageId);

public sealed record FacebookMessengerDownloadedMedia(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record FacebookMessengerPageIdentity(string PageId, string DisplayName);

public sealed record FacebookMessengerSubscriptionState(
    bool IsSubscribed,
    IReadOnlyList<string> SubscribedFields);

internal static class FacebookMessengerSubscriptionContract
{
    public static IReadOnlyList<string> RequiredFields { get; } =
        ["messages", "message_deliveries", "message_reads", "message_echoes"];

    public static string RequiredFieldsCsv { get; } = string.Join(',', RequiredFields);

    public static IReadOnlyList<string> RequiredPermissions { get; } =
        ["pages_messaging", "pages_manage_metadata"];
}

internal sealed record FacebookMessengerGraphMessageRequest(
    string PageId,
    string RecipientPsid,
    object Message,
    string MessagingType,
    string? Tag);

public sealed class FacebookMessengerGraphClient(
    HttpClient httpClient,
    IFacebookMessengerRuntimeConfigurationReader configurationReader,
    FacebookMessengerSafeMediaDownloader mediaDownloader,
    ILogger<FacebookMessengerGraphClient> logger)
{
    public Task<FacebookMessengerSendReceipt> SendTextAsync(
        string pageId,
        string recipientPsid,
        string text,
        CancellationToken ct) =>
        SendResponseAsync(pageId, recipientPsid, new { text }, ct);

    public Task<FacebookMessengerSendReceipt> SendHumanAgentTextAsync(
        string pageId,
        string recipientPsid,
        string text,
        CancellationToken ct) =>
        SendHumanAgentAsync(pageId, recipientPsid, new { text }, ct);

    public async Task<string> ProfileDisplayNameAsync(
        string pageId,
        string senderPsid,
        string fallbackDisplayName,
        CancellationToken ct)
    {
        var configuration = await configurationReader.GetAsync(ct);
        var page = configuration.RequirePage(pageId);
        using var request = AuthorizedRequest(
            HttpMethod.Get,
            GraphUri(configuration.ApiVersion, senderPsid, null, "fields=first_name,last_name"),
            page.AccessToken);
        try
        {
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Messenger profile lookup failed for page {PageId} with status {StatusCode}",
                    pageId,
                    (int)response.StatusCode);
                return fallbackDisplayName;
            }
            return await ProfileNameAsync(response.Content, fallbackDisplayName, ct);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Messenger profile lookup could not reach Meta for page {PageId}", pageId);
            return fallbackDisplayName;
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Messenger profile lookup timed out for page {PageId}", pageId);
            return fallbackDisplayName;
        }
    }

    public async Task<FacebookMessengerDownloadedMedia> DownloadInboundMediaAsync(
        string pageId,
        string mediaUrl,
        CancellationToken ct)
    {
        var configuration = await configurationReader.GetAsync(ct);
        configuration.RequirePage(pageId);
        return await mediaDownloader.DownloadAsync(mediaUrl, ct);
    }

    private Task<FacebookMessengerSendReceipt> SendResponseAsync(
        string pageId,
        string recipientPsid,
        object message,
        CancellationToken ct) =>
        SendMessageAsync(new FacebookMessengerGraphMessageRequest(
            pageId, recipientPsid, message, "RESPONSE", null), ct);

    private Task<FacebookMessengerSendReceipt> SendHumanAgentAsync(
        string pageId,
        string recipientPsid,
        object message,
        CancellationToken ct) =>
        SendMessageAsync(new FacebookMessengerGraphMessageRequest(
            pageId, recipientPsid, message, "MESSAGE_TAG", "HUMAN_AGENT"), ct);

    private async Task<FacebookMessengerSendReceipt> SendMessageAsync(
        FacebookMessengerGraphMessageRequest messageRequest,
        CancellationToken ct)
    {
        var configuration = await configurationReader.GetAsync(ct);
        var page = configuration.RequirePage(messageRequest.PageId);
        var messagePayload = new Dictionary<string, object?>
        {
            ["recipient"] = new { id = messageRequest.RecipientPsid },
            ["messaging_type"] = messageRequest.MessagingType,
            ["message"] = messageRequest.Message
        };
        if (messageRequest.Tag is not null) messagePayload["tag"] = messageRequest.Tag;
        using var request = AuthorizedRequest(
            HttpMethod.Post,
            GraphUri(configuration.ApiVersion, messageRequest.PageId, "messages"),
            page.AccessToken);
        request.Content = JsonContent.Create(messagePayload);
        using var response = await httpClient.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw ProviderFailure(response.StatusCode, responseText);
        return ParseSendReceipt(responseText, messageRequest.RecipientPsid);
    }

    public async Task<FacebookMessengerPageIdentity> InspectPageTokenAsync(
        string apiVersion,
        string accessToken,
        CancellationToken ct)
    {
        using var request = AuthorizedRequest(
            HttpMethod.Get,
            GraphUri(apiVersion, "me", null, "fields=id,name"),
            accessToken);
        using var response = await httpClient.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw ProviderFailure(response.StatusCode, responseText);
        try
        {
            using var document = JsonDocument.Parse(responseText);
            var pageId = Text(document.RootElement, "id");
            var displayName = Text(document.RootElement, "name");
            if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(displayName))
                throw new FacebookMessengerProviderException("MESSENGER_PAGE_TOKEN_RESPONSE_INVALID", false);
            return new FacebookMessengerPageIdentity(pageId, displayName);
        }
        catch (JsonException)
        {
            throw new FacebookMessengerProviderException("MESSENGER_PAGE_TOKEN_RESPONSE_INVALID", false);
        }
    }

    public async Task<FacebookMessengerPageIdentity> InspectPageTokenForAppAsync(
        string apiVersion,
        string appId,
        string appSecret,
        string accessToken,
        CancellationToken ct)
    {
        var identity = await InspectPageTokenAsync(apiVersion, accessToken, ct);
        using var request = AuthorizedRequest(
            HttpMethod.Get,
            GraphUri(
                apiVersion,
                "debug_token",
                null,
                $"input_token={Uri.EscapeDataString(accessToken)}"),
            $"{appId}|{appSecret}");
        using var response = await httpClient.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw ProviderFailure(response.StatusCode, responseText);
        ValidateTokenInspection(responseText, appId, identity.PageId);
        return identity;
    }

    public async Task<FacebookMessengerSubscriptionState> SubscribePageAsync(
        string apiVersion,
        string pageId,
        string appId,
        string accessToken,
        CancellationToken ct)
    {
        using var request = AuthorizedRequest(
            HttpMethod.Post,
            GraphUri(
                apiVersion,
                pageId,
                "subscribed_apps",
                $"subscribed_fields={FacebookMessengerSubscriptionContract.RequiredFieldsCsv}"),
            accessToken);
        using var response = await httpClient.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw ProviderFailure(response.StatusCode, responseText);
        if (!SuccessResponse(responseText))
            throw new FacebookMessengerMutationUncertainException(
                "MESSENGER_SUBSCRIBE_RESPONSE_INVALID");
        try
        {
            return await GetSubscriptionAsync(apiVersion, pageId, appId, accessToken, ct);
        }
        catch (FacebookMessengerProviderException exception)
        {
            throw new FacebookMessengerMutationUncertainException(exception.ErrorCode);
        }
    }

    public async Task<FacebookMessengerSubscriptionState> GetSubscriptionAsync(
        string apiVersion,
        string pageId,
        string appId,
        string accessToken,
        CancellationToken ct)
    {
        using var request = AuthorizedRequest(
            HttpMethod.Get,
            GraphUri(apiVersion, pageId, "subscribed_apps", "fields=id,name,subscribed_fields&limit=100"),
            accessToken);
        using var response = await httpClient.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw ProviderFailure(response.StatusCode, responseText);
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
                throw new FacebookMessengerProviderException("MESSENGER_SUBSCRIPTION_RESPONSE_INVALID", false);
            foreach (var app in data.EnumerateArray())
            {
                if (!string.Equals(Text(app, "id"), appId, StringComparison.Ordinal)) continue;
                var subscribedFields = app.TryGetProperty("subscribed_fields", out var fields) &&
                    fields.ValueKind == JsonValueKind.Array
                        ? fields.EnumerateArray()
                            .Where(field => field.ValueKind == JsonValueKind.String)
                            .Select(field => field.GetString()!)
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(field => field, StringComparer.Ordinal)
                            .ToArray()
                        : [];
                return new FacebookMessengerSubscriptionState(true, subscribedFields);
            }
            if (document.RootElement.TryGetProperty("paging", out var paging) &&
                paging.ValueKind == JsonValueKind.Object &&
                paging.TryGetProperty("next", out var next) &&
                next.ValueKind == JsonValueKind.String)
                throw new FacebookMessengerProviderException(
                    "MESSENGER_SUBSCRIPTION_RESULT_TRUNCATED",
                    false);
            return new FacebookMessengerSubscriptionState(false, []);
        }
        catch (JsonException)
        {
            throw new FacebookMessengerProviderException("MESSENGER_SUBSCRIPTION_RESPONSE_INVALID", false);
        }
    }

    public async Task<FacebookMessengerSubscriptionState> UnsubscribePageAsync(
        string apiVersion,
        string pageId,
        string appId,
        string accessToken,
        CancellationToken ct)
    {
        using var request = AuthorizedRequest(
            HttpMethod.Delete,
            GraphUri(apiVersion, pageId, "subscribed_apps"),
            accessToken);
        using var response = await httpClient.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw ProviderFailure(response.StatusCode, responseText);
        if (!SuccessResponse(responseText))
            throw new FacebookMessengerMutationUncertainException(
                "MESSENGER_UNSUBSCRIBE_RESPONSE_INVALID");
        try
        {
            return await GetSubscriptionAsync(apiVersion, pageId, appId, accessToken, ct);
        }
        catch (FacebookMessengerProviderException exception)
        {
            throw new FacebookMessengerMutationUncertainException(exception.ErrorCode);
        }
    }

    private static async Task<string> ProfileNameAsync(
        HttpContent content,
        string fallbackDisplayName,
        CancellationToken ct)
    {
        try
        {
            using var document = JsonDocument.Parse(await content.ReadAsStringAsync(ct));
            var firstName = Text(document.RootElement, "first_name");
            var lastName = Text(document.RootElement, "last_name");
            var fullName = string.Join(' ', new[] { firstName, lastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            return string.IsNullOrWhiteSpace(fullName) ? fallbackDisplayName : fullName;
        }
        catch (JsonException)
        {
            return fallbackDisplayName;
        }
    }

    private static FacebookMessengerSendReceipt ParseSendReceipt(
        string responseText,
        string expectedRecipientPsid)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            var recipientPsid = Text(document.RootElement, "recipient_id");
            var providerMessageId = Text(document.RootElement, "message_id");
            if (recipientPsid != expectedRecipientPsid || string.IsNullOrWhiteSpace(providerMessageId))
                throw new FacebookMessengerDeliveryUncertainException();
            return new FacebookMessengerSendReceipt(recipientPsid, providerMessageId);
        }
        catch (JsonException)
        {
            throw new FacebookMessengerDeliveryUncertainException();
        }
    }

    private static Uri GraphUri(
        string apiVersion,
        string resourceId,
        string? edge,
        string? query = null)
    {
        var path = edge is null
            ? Uri.EscapeDataString(resourceId)
            : $"{Uri.EscapeDataString(resourceId)}/{edge}";
        var suffix = query is null ? string.Empty : $"?{query}";
        return new Uri($"https://graph.facebook.com/{apiVersion}/{path}{suffix}");
    }

    private static bool SuccessResponse(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            return document.RootElement.TryGetProperty("success", out var success) &&
                success.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidateTokenInspection(
        string responseText,
        string expectedAppId,
        string expectedPageId)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("is_valid", out var isValid) ||
                isValid.ValueKind != JsonValueKind.True)
                throw new FacebookMessengerProviderException("MESSENGER_PAGE_TOKEN_INVALID", false);

            if (!string.Equals(ScalarText(data, "app_id"), expectedAppId, StringComparison.Ordinal))
                throw new FacebookMessengerProviderException("MESSENGER_PAGE_TOKEN_APP_MISMATCH", false);
            if (!string.Equals(ScalarText(data, "type"), "PAGE", StringComparison.OrdinalIgnoreCase))
                throw new FacebookMessengerProviderException("MESSENGER_PAGE_TOKEN_TYPE_INVALID", false);
            var profileId = ScalarText(data, "profile_id");
            if (profileId is not null &&
                !string.Equals(profileId, expectedPageId, StringComparison.Ordinal))
                throw new FacebookMessengerProviderException("MESSENGER_PAGE_TOKEN_MISMATCH", false);

            var permissions = TokenPermissions(data, expectedPageId);
            if (!FacebookMessengerSubscriptionContract.RequiredPermissions.All(permissions.Contains))
                throw new FacebookMessengerProviderException(
                    "MESSENGER_PAGE_TOKEN_PERMISSIONS_MISSING",
                    false);
        }
        catch (JsonException)
        {
            throw new FacebookMessengerProviderException(
                "MESSENGER_PAGE_TOKEN_INSPECTION_INVALID",
                false);
        }
    }

    private static HashSet<string> TokenPermissions(JsonElement data, string expectedPageId)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        if (data.TryGetProperty("scopes", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
            foreach (var scope in scopes.EnumerateArray())
                if (scope.ValueKind == JsonValueKind.String && scope.GetString() is { Length: > 0 } value)
                    permissions.Add(value);
        if (data.TryGetProperty("granular_scopes", out var granularScopes) &&
            granularScopes.ValueKind == JsonValueKind.Array)
            foreach (var scope in granularScopes.EnumerateArray())
                if (ScalarText(scope, "scope") is { Length: > 0 } value &&
                    GranularScopeAppliesToPage(scope, expectedPageId))
                    permissions.Add(value);
        return permissions;
    }

    private static bool GranularScopeAppliesToPage(JsonElement scope, string expectedPageId)
    {
        if (!scope.TryGetProperty("target_ids", out var targetIds)) return true;
        return targetIds.ValueKind == JsonValueKind.Array &&
            targetIds.EnumerateArray().Any(targetId =>
                string.Equals(ScalarText(targetId), expectedPageId, StringComparison.Ordinal));
    }

    private static HttpRequestMessage AuthorizedRequest(
        HttpMethod method,
        Uri uri,
        string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static FacebookMessengerProviderException ProviderFailure(
        HttpStatusCode statusCode,
        string responseText)
    {
        var providerError = ParseProviderError(responseText);
        return new FacebookMessengerProviderException(
            providerError.Code is null
                ? "MESSENGER_GRAPH_REQUEST_FAILED"
                : $"MESSENGER_GRAPH_{providerError.Code}",
            IsRetryable(statusCode, providerError));
    }

    private static FacebookMessengerProviderError ParseProviderError(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("error", out var error) ||
                error.ValueKind != JsonValueKind.Object)
                return new FacebookMessengerProviderError(null, false);
            var code = error.TryGetProperty("code", out var codeElement)
                ? new string(codeElement.GetRawText()
                .Where(character => char.IsAsciiLetterOrDigit(character) || character == '_')
                .Take(64)
                    .ToArray())
                : null;
            var isTransient = error.TryGetProperty("is_transient", out var transient) &&
                transient.ValueKind == JsonValueKind.True;
            return new FacebookMessengerProviderError(code, isTransient);
        }
        catch (JsonException)
        {
            return new FacebookMessengerProviderError(null, false);
        }
    }

    private static bool IsRetryable(
        HttpStatusCode statusCode,
        FacebookMessengerProviderError providerError) =>
        providerError.IsTransient ||
        providerError.Code is "1" or "2" or "4" or "17" or "32" ||
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var text) &&
        text.ValueKind == JsonValueKind.String
            ? text.GetString()
            : null;

    private static string? ScalarText(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out var value)) return null;
        return ScalarText(value);
    }

    private static string? ScalarText(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };

    private sealed record FacebookMessengerProviderError(string? Code, bool IsTransient);
}

public sealed class FacebookMessengerDeliveryUncertainException()
    : Exception("Meta accepted the Messenger request but returned an invalid receipt.");

public sealed class FacebookMessengerProviderException(
    string errorCode,
    bool isRetryable)
    : Exception("Facebook Messenger provider operation failed.")
{
    public string ErrorCode { get; } = errorCode;
    public bool IsRetryable { get; } = isRetryable;
}

public sealed class FacebookMessengerMutationUncertainException(string errorCode)
    : Exception("Meta mutation result requires reconciliation.")
{
    public string ErrorCode { get; } = errorCode;
}
