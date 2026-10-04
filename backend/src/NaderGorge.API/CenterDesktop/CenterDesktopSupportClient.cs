using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace NaderGorge.API.CenterDesktop;

public sealed partial class CenterDesktopSupportClient(HttpClient http, IOptions<CenterDesktopSupportOptions> options)
{
    private const long MaximumBundleBytes = 128L * 1024 * 1024;
    private const int MaximumMetadataBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 24 };
    private static readonly Regex UploadIdPattern = new("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Hash = new("^[a-f0-9]{64}$", RegexOptions.CultureInvariant);
    private readonly CenterDesktopSupportOptions _options = options.Value;
    public bool Configured => _options.TryGetOrigin(out _);

    public async Task<DesktopSupportStatus> StatusAsync(CancellationToken cancellationToken)
    {
        if (!Configured) return new(false, false, "اتصال دعم السنتر غير مُعدّ على الخادم.");
        try
        {
            await UploadsAsync(1, null, cancellationToken);
            return new(true, true, "خدمة دعم السنتر متاحة.");
        }
        catch (DesktopSupportException)
        {
            return new(true, false, "تعذر الوصول إلى خدمة دعم السنتر. راجع إعداد الاتصال على الخادم.");
        }
    }

    public async Task<DesktopUploadsPage> UploadsAsync(int limit, string? after, CancellationToken cancellationToken)
    {
        ValidateLimit(limit);
        if (after is not null && !ValidId(after)) throw BadRequest();
        var page = await ReadJsonAsync<DesktopUploadsPage>($"/v1/uploads?limit={limit}" +
            (after is null ? "" : $"&after={after.ToLowerInvariant()}"), cancellationToken);
        if (page.Uploads is null || page.Uploads.Length > limit || page.Uploads.Any(r => !ValidReceipt(r)) ||
            page.NextCursor is null || (page.NextCursor.Length != 0 && !ValidId(page.NextCursor))) throw InvalidResponse();
        return page;
    }

    public async Task<DesktopDiagnostics> DiagnosticsAsync(string id, int limit, CancellationToken cancellationToken)
    {
        ValidateId(id);
        ValidateLimit(limit);
        var value = await ReadJsonAsync<DesktopDiagnostics>($"/v1/uploads/{id.ToLowerInvariant()}/diagnostics?limit={limit}", cancellationToken);
        if (!ValidReceipt(value.Receipt) || value.Receipt.UploadId != Guid.Parse(id) ||
            value.Kind is not ("database" or "diagnostics") || value.Events is null || value.Events.Length > limit ||
            value.Total < value.Events.Length || value.Events.Any(e => !ValidEvent(e))) throw InvalidResponse();
        return value;
    }

    public async Task<DesktopReleases> ReleasesAsync(CancellationToken cancellationToken)
    {
        var value = await ReadJsonAsync<DesktopReleases>("/v1/admin/releases", cancellationToken);
        if (value.Releases is null || value.Releases.Length != 4 ||
            value.Releases.Any(r => r is null || r.Platform is not ("windows-x64" or "macos-arm64") || r.Role is not ("host" or "client") ||
                r.Status is not ("available" or "missing" or "invalid") ||
                (r.Status == "available" ? !ValidManifest(r.Manifest, r.Platform, r.Role) : r.Manifest is not null)) ||
            value.Releases.Select(r => (r.Platform, r.Role)).Distinct().Count() != 4) throw InvalidResponse();
        return value;
    }

    public async Task<DesktopSupportDownload> DownloadAsync(string id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        HttpResponseMessage? response = null;
        try
        {
            response = await SendAsync($"/v1/uploads/{id.ToLowerInvariant()}", timeout.Token);
            if (response.Content.Headers.ContentLength is > MaximumBundleBytes) throw InvalidResponse();
            var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            return new DesktopSupportDownload(response, stream, timeout, MaximumBundleBytes);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            response?.Dispose(); timeout.Dispose(); throw TimedOut();
        }
        catch (Exception error) when (error is HttpRequestException or IOException)
        {
            response?.Dispose(); timeout.Dispose(); throw Unavailable();
        }
        catch
        {
            response?.Dispose(); timeout.Dispose(); throw;
        }
    }

    private async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response = await SendAsync(path, timeout.Token);
            if (response.Content.Headers.ContentLength is > MaximumMetadataBytes) throw InvalidResponse();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var bytes = new MemoryStream();
            await CopyBoundedAsync(stream, bytes, MaximumMetadataBytes, timeout.Token);
            return JsonSerializer.Deserialize<T>(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)), JsonOptions)
                ?? throw InvalidResponse();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw TimedOut(); }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (IOException) { throw Unavailable(); }
        catch (JsonException) { throw InvalidResponse(); }
    }

    private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken cancellationToken)
    {
        if (!_options.TryGetOrigin(out var origin))
            throw new DesktopSupportException(503, "اتصال دعم السنتر غير مُعدّ على الخادم.");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin!, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AdminToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) { response.Dispose(); throw new DesktopSupportException(404, "لم يتم العثور على الملف المطلوب."); }
        if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType != "application/json")
        { response.Dispose(); throw InvalidResponse(); }
        return response;
    }

    internal static async Task CopyBoundedAsync(Stream source, Stream destination, long maximum, CancellationToken token)
    {
        var buffer = new byte[64 * 1024];
        long total = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, token)) != 0)
        {
            total += count;
            if (total > maximum) throw InvalidResponse();
            await destination.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }

    public static bool ValidId(string id) => id is not null && UploadIdPattern.IsMatch(id);
    private static void ValidateId(string id) { if (!ValidId(id)) throw BadRequest(); }
    private static void ValidateLimit(int limit) { if (limit is < 1 or > 500) throw BadRequest(); }
    private static bool ValidReceipt(DesktopUploadReceipt? r) => r is not null && ValidId(r.ReceiptId.ToString("D")) && ValidId(r.UploadId.ToString("D")) &&
        r.CenterId is not null && Regex.IsMatch(r.CenterId, "^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$") &&
        r.Sha256 is not null && Hash.IsMatch(r.Sha256) && r.BundleSha256 is not null && Hash.IsMatch(r.BundleSha256) &&
        r.Size is > 0 and <= MaximumBundleBytes && r.CreatedAt != default && r.CreatedAt.Offset == TimeSpan.Zero && r.ReceivedAt != default && r.ReceivedAt.Offset == TimeSpan.Zero && r.App is not null &&
        r.App.Role is "host" or "client" && r.App.Version is not null && r.App.Version.Length <= 128 &&
        r.App.Build is not null && r.App.Build.Length <= 128 && r.App.Os is not null && r.App.Os.Length <= 32;
    private static bool ValidEvent(DesktopDiagnosticEvent? e) => e is not null && e.Schema == 1 &&
        e.Kind is "error" or "session" && e.Id != Guid.Empty && e.Session != Guid.Empty &&
        e.Time != default && e.Time.Offset == TimeSpan.Zero && e.Version is not null && e.Version.Length <= 128 &&
        e.Platform is not null && e.Platform.Length <= 32 && e.Operation is not null && e.Operation.Length <= 128 &&
        (e.Build?.Length ?? 0) <= 128 && (e.Role is null or "host" or "client") &&
        (e.Errors?.Length ?? 0) <= 4 && (e.Frames?.Length ?? 0) <= 16 &&
        (e.Kind != "error" || e.Errors is { Length: > 0 }) &&
        (e.Errors?.All(error => error is not null && error.Type is not null && error.Type.Length <= 128) ?? true) &&
        (e.Frames?.All(frame => frame is not null && frame.File is not null && frame.File.Length <= 128) ?? true);
    private static bool ValidManifest(DesktopReleaseManifest? m, string platform, string role) => m is not null &&
        m.Platform == platform && m.Role == role && m.Size is > 0 and <= 2L * 1024 * 1024 * 1024 &&
        m.Sha256 is not null && Hash.IsMatch(m.Sha256) && m.ReleaseId is not null && m.ReleaseId.Length <= 64 &&
        m.Version is not null && m.Version.Length <= 128 && m.Build is not null && m.Build.Length <= 128 &&
        m.DownloadPath is not null && Regex.IsMatch(m.DownloadPath, "^/v1/releases/[A-Za-z0-9][A-Za-z0-9._-]{0,199}\\.zip$") && (m.Notes?.Length ?? 0) <= 8192;
    private static DesktopSupportException BadRequest() => new(400, "معرّف الملف أو حد النتائج غير صالح.");
    private static DesktopSupportException InvalidResponse() => new(502, "تعذر قراءة رد خدمة دعم السنتر.");
    private static DesktopSupportException Unavailable() => new(502, "تعذر الاتصال بخدمة دعم السنتر.");
    private static DesktopSupportException TimedOut() => new(504, "انتهت مهلة الاتصال بخدمة دعم السنتر.");
}

public sealed class DesktopSupportDownload(HttpResponseMessage response, Stream stream,
    CancellationTokenSource timeout, long maximum) : IDisposable
{
    public async Task CopyToAsync(Stream destination, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        await CenterDesktopSupportClient.CopyBoundedAsync(stream, destination, maximum, linked.Token);
    }
    public void Dispose() { stream.Dispose(); response.Dispose(); timeout.Dispose(); }
}
