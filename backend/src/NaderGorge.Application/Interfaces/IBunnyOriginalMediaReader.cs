using System.Net.Http;

namespace NaderGorge.Application.Interfaces;

/// <summary>
/// Opens an original Bunny Stream upload for an internal-only media consumer.
/// The caller receives bytes only; Bunny URLs and credentials stay server-side.
/// </summary>
public interface IBunnyOriginalMediaReader
{
    Task<BunnyOriginalMediaStream> OpenAsync(
        BunnyStreamLibraryAccess library,
        string videoGuid,
        CancellationToken cancellationToken);
}

public sealed class BunnyOriginalMediaStream : IAsyncDisposable
{
    private readonly HttpResponseMessage _response;

    public BunnyOriginalMediaStream(
        HttpResponseMessage response,
        Stream content,
        string contentType,
        long? contentLength)
    {
        _response = response;
        Content = content;
        ContentType = contentType;
        ContentLength = contentLength;
    }

    public Stream Content { get; }
    public string ContentType { get; }
    public long? ContentLength { get; }

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
        _response.Dispose();
    }
}

/// <summary>
/// A safe, code-only failure suitable for the internal AI-media endpoint.
/// It deliberately carries no upstream URL, response body, or credential.
/// </summary>
public sealed class BunnyOriginalMediaException : Exception
{
    public BunnyOriginalMediaException(string errorCode, int statusCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }
}
