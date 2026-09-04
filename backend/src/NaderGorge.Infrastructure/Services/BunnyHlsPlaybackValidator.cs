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

        var cacheKey = $"bunny-hls-validation:v3:{libraryId:N}:{normalizedVideoGuid}:{configuredLibrary.UpdatedAt?.Ticks ?? 0}";
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
            var browserValidation = await FetchManifestAsync(
                playlistUrl,
                HlsRequestProfile.Browser,
                cancellationToken);
            if (!browserValidation.Success) return browserValidation;

            var nativeValidation = await FetchManifestAsync(
                playlistUrl,
                HlsRequestProfile.NativeApple,
                cancellationToken);
            return nativeValidation.ErrorCode == "BUNNY_HLS_AUTH_REJECTED"
                ? NativeAppleAccessRejected()
                : nativeValidation;
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
        HlsRequestProfile requestProfile,
        CancellationToken cancellationToken)
    {
        var master = await FetchManifestDocumentAsync(
            new Uri(playlistUrl),
            requestProfile,
            cancellationToken);
        if (!master.Validation.Success || master.Document is null) return master.Validation;

        var variantUri = ResolveFirstPlaylistUri(master.Document.RequestUri, master.Document.Body);
        if (variantUri is null)
        {
            if (master.Document.Body.Contains("#EXT-X-STREAM-INF:", StringComparison.Ordinal))
                return InvalidManifest();
            if (!master.Document.Body.Contains("#EXTINF:", StringComparison.Ordinal))
                return BunnyHlsPlaybackValidationResult.Ok();
            var directSegmentUri = ResolveFirstMediaUri(master.Document.RequestUri, master.Document.Body);
            return directSegmentUri is null
                ? InvalidManifest()
                : await ValidateMediaResourceAsync(directSegmentUri, requestProfile, cancellationToken);
        }

        var variant = await FetchManifestDocumentAsync(variantUri, requestProfile, cancellationToken);
        if (!variant.Validation.Success || variant.Document is null) return variant.Validation;

        var segmentUri = ResolveFirstMediaUri(variant.Document.RequestUri, variant.Document.Body);
        if (segmentUri is null) return InvalidManifest();

        return await ValidateMediaResourceAsync(segmentUri, requestProfile, cancellationToken);
    }

    private async Task<ManifestFetchResult> FetchManifestDocumentAsync(
        Uri manifestUri,
        HlsRequestProfile requestProfile,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, manifestUri, requestProfile);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.apple.mpegurl"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-mpegURL"));
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var statusFailure = FailureForStatus(response);
        if (statusFailure is not null) return ManifestFetchResult.Fail(statusFailure);
        if (requestProfile == HlsRequestProfile.Browser && !AllowsStudentOrigin(response))
        {
            return ManifestFetchResult.Fail(BunnyHlsPlaybackValidationResult.Fail(
                "BUNNY_HLS_CORS_REJECTED",
                "Bunny لم يسمح للمتصفح بقراءة HLS من نطاق الطلاب. راجع Allowed Domains وإعدادات CDN."));
        }

        var body = await ReadManifestBodyAsync(response.Content, cancellationToken);
        if (body is null) return ManifestFetchResult.Fail(InvalidManifest());
        var effectiveUri = response.RequestMessage?.RequestUri ?? manifestUri;
        return ManifestFetchResult.Ok(new ManifestDocument(effectiveUri, body));
    }

    private static async Task<string?> ReadManifestBodyAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumManifestBytes) return null;
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (buffer.Length <= MaximumManifestBytes)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read == 0) break;
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (buffer.Length > MaximumManifestBytes) return null;
        var manifest = System.Text.Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return manifest.StartsWith("#EXTM3U", StringComparison.Ordinal) ? manifest : null;
    }

    private async Task<BunnyHlsPlaybackValidationResult> ValidateMediaResourceAsync(
        Uri mediaUri,
        HlsRequestProfile requestProfile,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, mediaUri, requestProfile);
        request.Headers.Range = new RangeHeaderValue(0, 0);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var statusFailure = FailureForStatus(response);
        if (statusFailure is not null) return statusFailure;
        return requestProfile == HlsRequestProfile.NativeApple || AllowsStudentOrigin(response)
            ? BunnyHlsPlaybackValidationResult.Ok()
            : BunnyHlsPlaybackValidationResult.Fail(
                "BUNNY_HLS_CORS_REJECTED",
                "Bunny لم يسمح للمتصفح بتحميل أجزاء الفيديو من نطاق الطلاب. راجع Allowed Domains وإعدادات CDN.");
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        Uri uri,
        HlsRequestProfile requestProfile)
    {
        var request = new HttpRequestMessage(method, uri);
        if (requestProfile == HlsRequestProfile.Browser)
        {
            request.Headers.Referrer = StudentApplicationReferrer;
            request.Headers.TryAddWithoutValidation("Origin", StudentApplicationOrigin);
        }
        return request;
    }

    private static Uri? ResolveFirstPlaylistUri(Uri baseUri, string manifest)
    {
        var expectVariant = false;
        foreach (var rawLine in manifest.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal))
            {
                expectVariant = true;
                continue;
            }
            if (!expectVariant || line.Length == 0 || line.StartsWith('#')) continue;
            return ResolveTrustedUri(baseUri, line);
        }
        return null;
    }

    private static Uri? ResolveFirstMediaUri(Uri baseUri, string manifest)
    {
        foreach (var rawLine in manifest.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            return ResolveTrustedUri(baseUri, line);
        }
        return null;
    }

    private static Uri? ResolveTrustedUri(Uri baseUri, string reference)
    {
        if (!Uri.TryCreate(baseUri, reference, out var resolved)
            || resolved.Scheme != Uri.UriSchemeHttps
            || !string.Equals(resolved.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return resolved;
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

    private static BunnyHlsPlaybackValidationResult NativeAppleAccessRejected() =>
        BunnyHlsPlaybackValidationResult.Fail(
            "BUNNY_HLS_NATIVE_AUTH_REJECTED",
            "Bunny يسمح بطلبات المتصفح لكنه يرفض مشغل Apple الأصلي (403). أزل Allowed Domains/Hotlink Protection من Pull Zone واعتمد على CDN Token Authentication، ثم أعد اختيار HLS.");

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

    private enum HlsRequestProfile
    {
        Browser,
        NativeApple
    }

    private sealed record ManifestDocument(Uri RequestUri, string Body);

    private sealed record ManifestFetchResult(
        BunnyHlsPlaybackValidationResult Validation,
        ManifestDocument? Document)
    {
        public static ManifestFetchResult Ok(ManifestDocument document) =>
            new(BunnyHlsPlaybackValidationResult.Ok(), document);

        public static ManifestFetchResult Fail(BunnyHlsPlaybackValidationResult result) =>
            new(result, null);
    }
}
