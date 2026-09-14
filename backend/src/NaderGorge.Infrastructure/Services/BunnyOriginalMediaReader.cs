using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Infrastructure.Services;

/// <summary>
/// Resolves a Bunny original through the documented play-data endpoint and keeps
/// the CDN URL inside the backend process. It never follows an unvalidated redirect.
/// </summary>
public sealed class BunnyOriginalMediaReader : IBunnyOriginalMediaReader
{
    private const string BunnyApiBaseUrl = "https://video.bunnycdn.com";
    private const int UpstreamTimeoutSeconds = 30;
    private static readonly PerLibrarySecurityKeyConfiguration CdnSecurityKeyConfiguration = new(
        "BunnyAnalysis:CdnTokenSecurityKeysJson",
        "BUNNY_ANALYSIS_CDN_CONFIGURATION_INVALID",
        "إعداد مفاتيح CDN الخاصة بتحليل Bunny غير صالح.");
    private static readonly PerLibrarySecurityKeyConfiguration PlayerTokenSecurityKeyConfiguration = new(
        "BunnyAnalysis:PlayerTokenSecurityKeysJson",
        "BUNNY_ANALYSIS_PLAYER_TOKEN_CONFIGURATION_INVALID",
        "إعداد مفاتيح Player Token الخاصة بتحليل Bunny غير صالح.");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public BunnyOriginalMediaReader(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<BunnyOriginalMediaStream> OpenAsync(
        BunnyStreamLibraryAccess library,
        string videoGuid,
        CancellationToken cancellationToken)
    {
        if (library.ExternalLibraryId <= 0 || string.IsNullOrWhiteSpace(library.ApiKey))
        {
            throw new BunnyOriginalMediaException(
                "BUNNY_ANALYSIS_LIBRARY_UNAVAILABLE",
                StatusCodes.Status422UnprocessableEntity,
                "لا تتوفر بيانات وصول صالحة لمكتبة Bunny الخاصة بالفيديو.");
        }

        if (!Guid.TryParseExact(videoGuid, "D", out var parsedGuid))
        {
            throw new BunnyOriginalMediaException(
                "BUNNY_ANALYSIS_VIDEO_REFERENCE_INVALID",
                StatusCodes.Status422UnprocessableEntity,
                "مرجع فيديو Bunny غير صالح للتحليل.");
        }

        var normalizedGuid = parsedGuid.ToString("D");
        var originalUrl = await ResolveOriginalUrlAsync(library, normalizedGuid, cancellationToken);
        var signedUrl = SignOriginalUrlWhenConfigured(originalUrl, library.ExternalLibraryId);

        return await OpenContentAsync(signedUrl, cancellationToken);
    }

    private async Task<Uri> ResolveOriginalUrlAsync(
        BunnyStreamLibraryAccess library,
        string videoGuid,
        CancellationToken cancellationToken)
    {
        var endpoint = $"{BunnyApiBaseUrl}/library/{library.ExternalLibraryId}/videos/{Uri.EscapeDataString(videoGuid)}/play";
        var endpointUri = new Uri(endpoint);
        var playerTokenSecurityKey = ResolvePlayerTokenSecurityKey(library.ExternalLibraryId);
        if (!string.IsNullOrWhiteSpace(playerTokenSecurityKey))
        {
            // Bunny Player Token Authentication has its own token-security key.
            // It is intentionally not derived from the Stream API AccessKey.
            var expires = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
            var playerToken = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes($"{playerTokenSecurityKey}{videoGuid}{expires}")))
                .ToLowerInvariant();
            endpointUri = new UriBuilder(endpoint)
            {
                Query = $"token={Uri.EscapeDataString(playerToken)}&expires={expires}"
            }.Uri;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, endpointUri);
        request.Headers.Add("AccessKey", library.ApiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(UpstreamTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await _httpClientFactory.CreateClient("BunnyStream")
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BunnyOriginalMediaException(
                "BUNNY_ANALYSIS_PROVIDER_UNAVAILABLE",
                StatusCodes.Status503ServiceUnavailable,
                "انتهت مهلة التحقق من مصدر Bunny. أعد المحاولة لاحقًا.");
        }
        catch (HttpRequestException)
        {
            throw new BunnyOriginalMediaException(
                "BUNNY_ANALYSIS_PROVIDER_UNAVAILABLE",
                StatusCodes.Status503ServiceUnavailable,
                "تعذر الاتصال بمصدر Bunny. أعد المحاولة لاحقًا.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw MapMetadataFailure(response.StatusCode);
            }

            BunnyPlayData? playData;
            try
            {
                playData = await response.Content.ReadFromJsonAsync<BunnyPlayData>(JsonOptions, timeout.Token);
            }
            catch (JsonException)
            {
                throw new BunnyOriginalMediaException(
                    "BUNNY_ANALYSIS_PLAY_DATA_INVALID",
                    StatusCodes.Status502BadGateway,
                    "تعذر التحقق من بيانات تشغيل فيديو Bunny.");
            }

            if (string.IsNullOrWhiteSpace(playData?.OriginalUrl))
            {
                throw new BunnyOriginalMediaException(
                    "BUNNY_ANALYSIS_ORIGINAL_UNAVAILABLE",
                    StatusCodes.Status422UnprocessableEntity,
                    "إعداد Bunny الحالي لا يتيح الملف الأصلي لهذا الفيديو للتحليل الداخلي.");
            }

            return ValidateOriginalUrl(playData.OriginalUrl, videoGuid);
        }
    }

    private Uri SignOriginalUrlWhenConfigured(Uri originalUrl, long libraryId)
    {
        var securityKey = ResolveCdnSecurityKey(libraryId);
        if (string.IsNullOrWhiteSpace(securityKey))
        {
            // A pull zone without token authentication needs no signature. If the
            // zone is protected, the CDN response below is converted to a safe
            // configuration error rather than exposing a URL or disabling security.
            return originalUrl;
        }

        if (!string.IsNullOrEmpty(originalUrl.Query))
        {
            throw new BunnyOriginalMediaException(
                "BUNNY_ANALYSIS_ORIGINAL_URL_UNSUPPORTED",
                StatusCodes.Status502BadGateway,
                "تعذر تحضير مصدر Bunny الآمن للتحليل.");
        }

        var expires = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();
        var signatureInput = $"{originalUrl.AbsolutePath}{expires}";
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(securityKey),
            Encoding.UTF8.GetBytes(signatureInput));
        var token = "HS256-" + Base64UrlEncode(signature);

        return new UriBuilder(originalUrl)
        {
            Query = $"token={Uri.EscapeDataString(token)}&expires={expires}"
        }.Uri;
    }

    private async Task<BunnyOriginalMediaStream> OpenContentAsync(Uri sourceUrl, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(20));

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClientFactory.CreateClient("BunnyAnalysisMedia")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                var failure = MapContentFailure(response.StatusCode);
                response.Dispose();
                throw failure;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (!IsSupportedMediaContentType(contentType))
            {
                response.Dispose();
                throw new BunnyOriginalMediaException(
                    "BUNNY_ANALYSIS_ORIGINAL_MEDIA_INVALID",
                    StatusCodes.Status502BadGateway,
                    "أعاد Bunny مصدرًا غير صالح لتحليل الفيديو.");
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = new BunnyOriginalMediaStream(
                response,
                stream,
                contentType!,
                response.Content.Headers.ContentLength);
            response = null;
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BunnyOriginalMediaException(
                "BUNNY_ANALYSIS_MEDIA_TIMEOUT",
                StatusCodes.Status504GatewayTimeout,
                "انتهت مهلة فتح مصدر Bunny للتحليل. أعد المحاولة لاحقًا.");
        }
        catch (HttpRequestException)
        {
            throw new BunnyOriginalMediaException(
                "BUNNY_ANALYSIS_PROVIDER_UNAVAILABLE",
                StatusCodes.Status503ServiceUnavailable,
                "تعذر الاتصال بمصدر Bunny. أعد المحاولة لاحقًا.");
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static BunnyOriginalMediaException MapMetadataFailure(HttpStatusCode statusCode) => statusCode switch
    {
        _ when (int)statusCode is >= 300 and < 400 => new BunnyOriginalMediaException(
            "BUNNY_ANALYSIS_PLAY_REDIRECT_UNSAFE", StatusCodes.Status502BadGateway,
            "أعاد Bunny إعادة توجيه غير معتمدة لبيانات تشغيل الفيديو."),
        HttpStatusCode.NotFound => new BunnyOriginalMediaException(
            "BUNNY_ANALYSIS_VIDEO_NOT_FOUND", StatusCodes.Status404NotFound,
            "لم يعد فيديو Bunny موجودًا في مكتبته."),
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new BunnyOriginalMediaException(
            "BUNNY_ANALYSIS_LIBRARY_ACCESS_DENIED", StatusCodes.Status422UnprocessableEntity,
            "تعذر الوصول إلى فيديو Bunny بمفتاح المكتبة الحالي."),
        _ when (int)statusCode >= 500 => new BunnyOriginalMediaException(
            "BUNNY_ANALYSIS_PROVIDER_UNAVAILABLE", StatusCodes.Status503ServiceUnavailable,
            "خدمة Bunny غير متاحة للتحليل الآن. أعد المحاولة لاحقًا."),
        _ => new BunnyOriginalMediaException(
            "BUNNY_ANALYSIS_PLAY_DATA_UNAVAILABLE", StatusCodes.Status502BadGateway,
            "تعذر تجهيز فيديو Bunny للتحليل.")
    };

    private static BunnyOriginalMediaException MapContentFailure(HttpStatusCode statusCode) => statusCode switch
    {
        _ when (int)statusCode is >= 300 and < 400 => new BunnyOriginalMediaException(
            "BUNNY_ANALYSIS_ORIGINAL_REDIRECT_UNSAFE", StatusCodes.Status502BadGateway,
            "أعاد Bunny إعادة توجيه غير معتمدة لمصدر الملف الأصلي."),
        HttpStatusCode.NotFound => new BunnyOriginalMediaException(
            "BUNNY_ANALYSIS_ORIGINAL_UNAVAILABLE", StatusCodes.Status422UnprocessableEntity,
            "لا يتوفر الملف الأصلي لهذا الفيديو في Bunny."),
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new BunnyOriginalMediaException(
            "BUNNY_ANALYSIS_CDN_ACCESS_DENIED", StatusCodes.Status422UnprocessableEntity,
            "إعداد وصول CDN لفيديو Bunny لا يسمح للتحليل الداخلي."),
        _ when statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500 => new BunnyOriginalMediaException(
            "BUNNY_ANALYSIS_PROVIDER_UNAVAILABLE", StatusCodes.Status503ServiceUnavailable,
            "خدمة Bunny غير متاحة للتحليل الآن. أعد المحاولة لاحقًا."),
        _ => new BunnyOriginalMediaException(
            "BUNNY_ANALYSIS_ORIGINAL_UNAVAILABLE", StatusCodes.Status422UnprocessableEntity,
            "تعذر فتح الملف الأصلي للفيديو من Bunny.")
    };

    private static Uri ValidateOriginalUrl(string value, string videoGuid)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment)
            || (uri.Port != -1 && uri.Port != 443)
            || !uri.Host.EndsWith(".b-cdn.net", StringComparison.OrdinalIgnoreCase)
            || !HasExpectedOriginalPath(uri.AbsolutePath, videoGuid))
        {
            throw new BunnyOriginalMediaException(
                "BUNNY_ANALYSIS_ORIGINAL_URL_UNSAFE",
                StatusCodes.Status502BadGateway,
                "تعذر التحقق من مصدر Bunny الآمن للتحليل.");
        }

        return uri;
    }

    private static bool HasExpectedOriginalPath(string path, string videoGuid)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 2
            && string.Equals(segments[0], videoGuid, StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[1], "original", StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveCdnSecurityKey(long libraryId) =>
        ResolvePerLibrarySecurityKey(libraryId, CdnSecurityKeyConfiguration);

    private string? ResolvePlayerTokenSecurityKey(long libraryId) =>
        ResolvePerLibrarySecurityKey(libraryId, PlayerTokenSecurityKeyConfiguration);

    private string? ResolvePerLibrarySecurityKey(
        long libraryId,
        PerLibrarySecurityKeyConfiguration keyConfiguration)
    {
        var rawMap = _configuration[keyConfiguration.JsonMapKey];
        if (string.IsNullOrWhiteSpace(rawMap)) return null;

        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(rawMap);
            return map is not null && map.TryGetValue(libraryId.ToString(), out var value)
                ? value?.Trim()
                : null;
        }
        catch (JsonException)
        {
            throw new BunnyOriginalMediaException(
                keyConfiguration.InvalidConfigurationCode,
                StatusCodes.Status422UnprocessableEntity,
                keyConfiguration.InvalidConfigurationMessage);
        }
    }

    private static bool IsSupportedMediaContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase));

    private static string Base64UrlEncode(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private sealed record BunnyPlayData(string? OriginalUrl);

    private sealed record PerLibrarySecurityKeyConfiguration(
        string JsonMapKey,
        string InvalidConfigurationCode,
        string InvalidConfigurationMessage);
}
