using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class BunnyHlsPlaybackValidator : IBunnyHlsPlaybackValidator
{
    private const int MaximumManifestBytes = 64 * 1024;
    private const string StudentApplicationOrigin = "https://app.massar-academy.net";
    private static readonly TimeSpan ValidCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan InvalidCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly Uri StudentApplicationReferrer = new("https://app.massar-academy.net/");

    private readonly HttpClient _httpClient;
    private readonly IAppDbContext _db;
    private readonly IBunnyHlsSecretProtector _secretProtector;
    private readonly IMemoryCache _cache;

    public BunnyHlsPlaybackValidator(
        HttpClient httpClient,
        IAppDbContext db,
        IBunnyHlsSecretProtector secretProtector,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _db = db;
        _secretProtector = secretProtector;
        _cache = cache;
    }

    public async Task<BunnyHlsPlaybackValidationResult> ValidateVideoAsync(
        Guid libraryId,
        string videoGuid,
        CancellationToken cancellationToken)
    {
        if (libraryId == Guid.Empty || !Guid.TryParse(videoGuid, out var parsedVideoGuid))
        {
            return BunnyHlsPlaybackValidationResult.Fail(
                "BUNNY_HLS_VIDEO_INVALID",
                "معرّف فيديو Bunny غير صالح لتشغيل HLS.");
        }

        var normalizedVideoGuid = parsedVideoGuid.ToString("D");
        var library = await LoadLibraryAsync(libraryId, cancellationToken);
        var configurationFailure = ValidateConfiguration(library);
        if (configurationFailure is not null) return configurationFailure;
        var configuredLibrary = library!;
        var tokenCiphertext = configuredLibrary.HlsTokenKeyCiphertext!;

        var cacheKey = $"bunny-hls-validation:v1:{libraryId:N}:{normalizedVideoGuid}:{configuredLibrary.UpdatedAt?.Ticks ?? 0}";
        if (_cache.TryGetValue(cacheKey, out BunnyHlsPlaybackValidationResult? cached) && cached is not null)
        {
            return cached;
        }

        var validationResult = await ValidateConfiguredVideoAsync(
            new HlsValidationRequest(
                libraryId,
                normalizedVideoGuid,
                configuredLibrary.HlsCdnHostname!,
                tokenCiphertext),
            cancellationToken);

        _cache.Set(
            cacheKey,
            validationResult,
            validationResult.Success ? ValidCacheDuration : InvalidCacheDuration);
        return validationResult;
    }

    private Task<HlsLibraryConfiguration?> LoadLibraryAsync(Guid libraryId, CancellationToken cancellationToken) =>
        _db.BunnyStreamLibraries
            .AsNoTracking()
            .Where(library => library.Id == libraryId)
            .Select(library => new HlsLibraryConfiguration(
                library.HlsCdnHostname,
                library.HlsTokenKeyCiphertext,
                library.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<BunnyHlsPlaybackValidationResult> ValidateConfiguredVideoAsync(
        HlsValidationRequest validationRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            var tokenKey = _secretProtector.Unprotect(
                validationRequest.LibraryId,
                validationRequest.TokenCiphertext);
            var playlistUrl = new BunnyHlsUrlSigner().SignPlaylist(
                validationRequest.Hostname,
                validationRequest.VideoGuid,
                tokenKey,
                DateTime.UtcNow.AddMinutes(3));
            return await FetchManifestAsync(playlistUrl, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return BunnyHlsPlaybackValidationResult.Fail(
                "BUNNY_HLS_TIMEOUT",
                "انتهت مهلة الاتصال بـBunny HLS. لم يتم تغيير المشغل؛ أعد المحاولة.");
        }
        catch (Exception exception) when (exception is HttpRequestException
                                          or IOException
                                          or CryptographicException
                                          or InvalidOperationException
                                          or ArgumentException)
        {
            return Unreachable();
        }
    }

    private async Task<BunnyHlsPlaybackValidationResult> FetchManifestAsync(
        string playlistUrl,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, playlistUrl);
        request.Headers.Referrer = StudentApplicationReferrer;
        request.Headers.TryAddWithoutValidation("Origin", StudentApplicationOrigin);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.apple.mpegurl"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-mpegURL"));
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        return await ValidateResponseAsync(response, cancellationToken);
    }

    private static async Task<BunnyHlsPlaybackValidationResult> ValidateResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var statusFailure = FailureForStatus(response);
        if (statusFailure is not null) return statusFailure;
        if (!AllowsStudentOrigin(response))
        {
            return BunnyHlsPlaybackValidationResult.Fail(
                "BUNNY_HLS_CORS_REJECTED",
                "Bunny لم يسمح للمتصفح بقراءة HLS من نطاق الطلاب. راجع Allowed Domains وإعدادات CDN.");
        }
        return await ValidateManifestBodyAsync(response.Content, cancellationToken);
    }

    private static async Task<BunnyHlsPlaybackValidationResult> ValidateManifestBodyAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumManifestBytes) return InvalidManifest();
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (buffer.Length <= MaximumManifestBytes)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read == 0) break;
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (buffer.Length > MaximumManifestBytes) return InvalidManifest();
        var manifest = System.Text.Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return manifest.StartsWith("#EXTM3U", StringComparison.Ordinal)
            ? BunnyHlsPlaybackValidationResult.Ok()
            : InvalidManifest();
    }

    private static BunnyHlsPlaybackValidationResult? FailureForStatus(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return BunnyHlsPlaybackValidationResult.Fail(
                "BUNNY_HLS_AUTH_REJECTED",
                $"Bunny رفض رابط HLS ({(int)response.StatusCode}). تأكد أن المفتاح هو CDN Token Authentication Key وأن app.massar-academy.net موجود ضمن Allowed Domains.");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return BunnyHlsPlaybackValidationResult.Fail(
                "BUNNY_HLS_NOT_FOUND",
                "Bunny لم يجد ملف HLS لهذا الفيديو (404). تأكد أن CDN hostname تابع لنفس المكتبة وأن ترميز الفيديو اكتمل.");
        return response.IsSuccessStatusCode
            ? null
            : BunnyHlsPlaybackValidationResult.Fail(
                "BUNNY_HLS_HTTP_ERROR",
                $"Bunny أعاد حالة {(int)response.StatusCode} عند اختبار HLS. لم يتم تغيير المشغل.");
    }

    private static BunnyHlsPlaybackValidationResult? ValidateConfiguration(HlsLibraryConfiguration? library) =>
        library?.HlsTokenKeyCiphertext is { Length: > 0 }
        && !string.IsNullOrWhiteSpace(library.HlsCdnHostname)
            ? null
            : BunnyHlsPlaybackValidationResult.Fail(
                "BUNNY_HLS_CONFIG_INCOMPLETE",
                "أكمل CDN hostname وToken Authentication Key للمكتبة قبل اختيار مشغل المنصة HLS.");

    private static bool AllowsStudentOrigin(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins)
        && origins.Any(origin => origin is "*" or StudentApplicationOrigin);

    private static BunnyHlsPlaybackValidationResult InvalidManifest() =>
        BunnyHlsPlaybackValidationResult.Fail(
            "BUNNY_HLS_MANIFEST_INVALID",
            "استجابة Bunny ليست ملف HLS صالحًا. تأكد من CDN hostname والمفتاح الخاصين بنفس المكتبة.");

    private static BunnyHlsPlaybackValidationResult Unreachable() =>
        BunnyHlsPlaybackValidationResult.Fail(
            "BUNNY_HLS_UNREACHABLE",
            "تعذر التحقق من رابط Bunny HLS. لم يتم تغيير المشغل؛ راجع إعدادات CDN والاتصال.");

    private sealed record HlsLibraryConfiguration(
        string? HlsCdnHostname,
        byte[]? HlsTokenKeyCiphertext,
        DateTime? UpdatedAt);

    private sealed record HlsValidationRequest(
        Guid LibraryId,
        string VideoGuid,
        string Hostname,
        byte[] TokenCiphertext);
}
