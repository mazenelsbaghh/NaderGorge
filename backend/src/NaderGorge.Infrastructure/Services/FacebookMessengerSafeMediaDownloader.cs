using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace NaderGorge.Infrastructure.Services;

public sealed class FacebookMessengerSafeMediaDownloader : IDisposable
{
    private const int MaximumInboundMediaBytes = 10 * 1024 * 1024;
    private readonly HttpClient _httpClient;

    public FacebookMessengerSafeMediaDownloader()
        : this(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            UseProxy = false,
            ConnectCallback = ConnectPublicHostAsync
        })
    {
    }

    public FacebookMessengerSafeMediaDownloader(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<FacebookMessengerDownloadedMedia> DownloadAsync(
        string mediaUrl,
        CancellationToken ct)
    {
        var publicUri = await RequirePublicUriAsync(mediaUrl, ct);
        using var response = await _httpClient.GetAsync(
            publicUri,
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        EnsureSuccess(response);
        var content = await ReadBoundedAsync(response.Content, ct);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new FacebookMessengerDownloadedMedia(
            content,
            contentType,
            FileName(response.Content.Headers.ContentDisposition, contentType));
    }

    public void Dispose() => _httpClient.Dispose();

    private static async ValueTask<Stream> ConnectPublicHostAsync(
        SocketsHttpConnectionContext context,
        CancellationToken ct)
    {
        var addresses = await ResolveAddressesAsync(context.DnsEndPoint.Host, ct);
        SocketException? lastFailure = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(address, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException exception)
            {
                socket.Dispose();
                lastFailure = exception;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
        throw new HttpRequestException("Messenger media host could not be reached.", lastFailure);
    }

    private static async Task<Uri> RequirePublicUriAsync(string mediaUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.Port != 443)
            throw Failure("MESSENGER_MEDIA_URL_REJECTED", false);
        await ResolveAddressesAsync(uri.DnsSafeHost, ct);
        return uri;
    }

    private static async Task<IPAddress[]> ResolveAddressesAsync(string host, CancellationToken ct)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, ct);
        }
        catch (SocketException)
        {
            throw Failure("MESSENGER_MEDIA_HOST_UNRESOLVED", true);
        }
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw Failure("MESSENGER_MEDIA_HOST_REJECTED", false);
        return addresses;
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) || address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            return false;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var ipv6 = address.GetAddressBytes();
            return (ipv6[0] & 0xfe) != 0xfc && !address.Equals(IPAddress.IPv6None);
        }
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var octets = address.GetAddressBytes();
        return octets[0] != 0 && octets[0] != 10 && octets[0] != 127 &&
            !(octets[0] == 100 && octets[1] is >= 64 and <= 127) &&
            !(octets[0] == 169 && octets[1] == 254) &&
            !(octets[0] == 172 && octets[1] is >= 16 and <= 31) &&
            !(octets[0] == 192 && octets[1] == 0 && octets[2] is 0 or 2) &&
            !(octets[0] == 192 && octets[1] == 168) &&
            !(octets[0] == 198 && octets[1] is 18 or 19) &&
            !(octets[0] == 198 && octets[1] == 51 && octets[2] == 100) &&
            !(octets[0] == 203 && octets[1] == 0 && octets[2] == 113) &&
            octets[0] < 224;
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if ((int)response.StatusCode is >= 300 and < 400)
            throw Failure("MESSENGER_MEDIA_REDIRECT_REJECTED", false);
        if (!response.IsSuccessStatusCode)
            throw Failure(
                "MESSENGER_MEDIA_DOWNLOAD_FAILED",
                IsRetryable(response.StatusCode));
        if (response.Content.Headers.ContentLength is > MaximumInboundMediaBytes)
            throw Failure("MESSENGER_MEDIA_TOO_LARGE", false);
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken ct)
    {
        await using var source = await content.ReadAsStreamAsync(ct);
        await using var destination = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, ct);
            if (read == 0) return destination.ToArray();
            if (destination.Length + read > MaximumInboundMediaBytes)
                throw Failure("MESSENGER_MEDIA_TOO_LARGE", false);
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    private static string FileName(ContentDispositionHeaderValue? disposition, string contentType)
    {
        var suppliedName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"');
        if (!string.IsNullOrWhiteSpace(suppliedName)) return Path.GetFileName(suppliedName);
        var extension = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "audio/mpeg" => ".mp3",
            "audio/ogg" => ".ogg",
            "application/pdf" => ".pdf",
            _ => ".bin"
        };
        return $"messenger-{Guid.NewGuid():N}{extension}";
    }

    private static bool IsRetryable(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static FacebookMessengerProviderException Failure(
        string errorCode,
        bool retryable) =>
        new(errorCode, retryable);
}
