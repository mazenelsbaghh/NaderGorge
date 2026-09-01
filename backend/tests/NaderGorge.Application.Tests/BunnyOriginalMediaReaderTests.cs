using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Interfaces;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests;

public sealed class BunnyOriginalMediaReaderTests
{
    private const long LibraryId = 740733;
    private const string VideoGuid = "12345678-abcd-1234-abcd-123456789abc";
    private static readonly BunnyStreamLibraryAccess Library = new(
        Guid.Parse("55b8c5f6-9617-4a77-9e83-7c1dde09f22b"),
        "Science",
        LibraryId,
        "stream-api-key-should-never-leave-backend",
        true);

    [Fact]
    public async Task OpenAsync_UsesServerCredentialAndStreamsOnlySignedCdnContent()
    {
        var metadata = new RecordingHandler(request =>
        {
            var requestUri = Assert.IsType<Uri>(request.RequestUri);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("video.bunnycdn.com", requestUri.Host);
            Assert.Equal($"/library/{LibraryId}/videos/{VideoGuid}/play", requestUri.AbsolutePath);
            Assert.Equal(Library.ApiKey, request.Headers.GetValues("AccessKey").Single());
            var query = ParseQuery(requestUri);
            var expires = query["expires"];
            var expectedToken = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"player-token-security-key{VideoGuid}{expires}"))).ToLowerInvariant();
            Assert.Equal(expectedToken, query["token"]);
            Assert.DoesNotContain(Library.ApiKey, requestUri.ToString(), StringComparison.Ordinal);
            return JsonResponse($"{{\"originalUrl\":\"https://private-stream.b-cdn.net/{VideoGuid}/original\"}}");
        });
        var media = new RecordingHandler(request =>
        {
            Assert.Equal("private-stream.b-cdn.net", request.RequestUri?.Host);
            Assert.Equal($"/{VideoGuid}/original", request.RequestUri?.AbsolutePath);
            Assert.Contains("token=HS256-", request.RequestUri?.Query, StringComparison.Ordinal);
            Assert.Contains("expires=", request.RequestUri?.Query, StringComparison.Ordinal);
            Assert.DoesNotContain(Library.ApiKey, request.RequestUri?.ToString(), StringComparison.Ordinal);
            return MediaResponse("audio/mp4", "safe-audio-bytes");
        });
        var reader = CreateReader(metadata, media, new Dictionary<string, string?>
        {
            ["BunnyAnalysis:CdnTokenSecurityKeysJson"] = "{\"740733\":\"cdn-token-security-key\"}",
            ["BunnyAnalysis:PlayerTokenSecurityKeysJson"] = "{\"740733\":\"player-token-security-key\"}"
        });

        await using var source = await reader.OpenAsync(Library, VideoGuid, CancellationToken.None);
        using var content = new StreamReader(source.Content, Encoding.UTF8, leaveOpen: true);
        Assert.Equal("safe-audio-bytes", await content.ReadToEndAsync());
        Assert.Equal("audio/mp4", source.ContentType);
        Assert.Equal(16, source.ContentLength);
        Assert.Equal(1, metadata.CallCount);
        Assert.Equal(1, media.CallCount);
    }

    [Fact]
    public async Task OpenAsync_RejectsOriginalUrlOutsideBunnyCdnBeforeMediaFetch()
    {
        var metadata = new RecordingHandler(_ =>
            JsonResponse($"{{\"originalUrl\":\"https://example.com/{VideoGuid}/original\"}}"));
        var media = new RecordingHandler(_ => throw new Xunit.Sdk.XunitException("CDN must not be called."));
        var reader = CreateReader(metadata, media);

        var error = await Assert.ThrowsAsync<BunnyOriginalMediaException>(
            () => reader.OpenAsync(Library, VideoGuid, CancellationToken.None));

        Assert.Equal("BUNNY_ANALYSIS_ORIGINAL_URL_UNSAFE", error.ErrorCode);
        Assert.Equal(0, media.CallCount);
    }

    [Fact]
    public async Task OpenAsync_ReturnsSafeConfigurationErrorWhenBunnyDoesNotExposeAnOriginalUrl()
    {
        var metadata = new RecordingHandler(_ => JsonResponse("{\"originalUrl\":null}"));
        var media = new RecordingHandler(_ => throw new Xunit.Sdk.XunitException("CDN must not be called."));
        var reader = CreateReader(metadata, media);

        var error = await Assert.ThrowsAsync<BunnyOriginalMediaException>(
            () => reader.OpenAsync(Library, VideoGuid, CancellationToken.None));

        Assert.Equal("BUNNY_ANALYSIS_ORIGINAL_UNAVAILABLE", error.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, error.StatusCode);
        Assert.Equal(0, media.CallCount);
    }

    [Fact]
    public async Task OpenAsync_DoesNotDeriveAPlayerTokenFromTheStreamApiKey()
    {
        var metadata = new RecordingHandler(request =>
        {
            Assert.Empty(ParseQuery(request.RequestUri!));
            return JsonResponse($"{{\"originalUrl\":\"https://private-stream.b-cdn.net/{VideoGuid}/original\"}}");
        });
        var media = new RecordingHandler(_ => MediaResponse("video/mp4", "safe-video-bytes"));
        var reader = CreateReader(metadata, media);

        await using var source = await reader.OpenAsync(Library, VideoGuid, CancellationToken.None);

        Assert.Equal(1, metadata.CallCount);
        Assert.Equal(1, media.CallCount);
    }

    [Fact]
    public async Task OpenAsync_MapsProtectedCdnRejectionToSafeConfigurationFailure()
    {
        var metadata = new RecordingHandler(_ =>
            JsonResponse($"{{\"originalUrl\":\"https://private-stream.b-cdn.net/{VideoGuid}/original\"}}"));
        var media = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var reader = CreateReader(metadata, media);

        var error = await Assert.ThrowsAsync<BunnyOriginalMediaException>(
            () => reader.OpenAsync(Library, VideoGuid, CancellationToken.None));

        Assert.Equal("BUNNY_ANALYSIS_CDN_ACCESS_DENIED", error.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, error.StatusCode);
    }

    [Fact]
    public async Task OpenAsync_RejectsAnUnexpectedCdnRedirectWithoutFollowingIt()
    {
        var metadata = new RecordingHandler(_ =>
            JsonResponse($"{{\"originalUrl\":\"https://private-stream.b-cdn.net/{VideoGuid}/original\"}}"));
        var media = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("https://unexpected.example/video") }
        });
        var reader = CreateReader(metadata, media);

        var error = await Assert.ThrowsAsync<BunnyOriginalMediaException>(
            () => reader.OpenAsync(Library, VideoGuid, CancellationToken.None));

        Assert.Equal("BUNNY_ANALYSIS_ORIGINAL_REDIRECT_UNSAFE", error.ErrorCode);
        Assert.Equal(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.Equal(1, media.CallCount);
    }

    private static BunnyOriginalMediaReader CreateReader(
        HttpMessageHandler metadata,
        HttpMessageHandler media,
        IDictionary<string, string?>? configuration = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuration ?? new Dictionary<string, string?>())
            .Build();
        return new BunnyOriginalMediaReader(
            new NamedHttpClientFactory(
                new HttpClient(metadata) { BaseAddress = new Uri("https://video.bunnycdn.com") },
                new HttpClient(media)),
            config);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage MediaResponse(string contentType, string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8)
        {
            Headers = { ContentType = new MediaTypeHeaderValue(contentType) }
        }
    };

    private static IReadOnlyDictionary<string, string> ParseQuery(Uri uri) => uri.Query
        .TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .ToDictionary(
            part => Uri.UnescapeDataString(part[0]),
            part => part.Length == 2 ? Uri.UnescapeDataString(part[1]) : string.Empty,
            StringComparer.Ordinal);

    private sealed class NamedHttpClientFactory(HttpClient metadata, HttpClient media) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => name switch
        {
            "BunnyStream" => metadata,
            "BunnyAnalysisMedia" => media,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected named client: {name}")
        };
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request));
        }
    }
}
