using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class BunnyStreamClient : IBunnyStreamClient
{
    private const string StreamBaseUrl = "https://video.bunnycdn.com";
    private const string CoreBaseUrl = "https://api.bunny.net";
    private const string TusEndpoint = "https://video.bunnycdn.com/tusupload";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly long _libraryId;
    private readonly string _apiKey;

    public BunnyStreamClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _libraryId = long.TryParse(configuration["BunnyStream:LibraryId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var libraryId)
            ? libraryId
            : 0;
        _apiKey = configuration["BunnyStream:ApiKey"] ?? string.Empty;
    }

    public async Task<BunnyStreamVideoDto> CreateVideoAsync(string title, string? collectionId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Post, $"{StreamBaseUrl}/library/{_libraryId}/videos");
        request.Content = JsonContent.Create(new { title, collectionId }, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var model = await response.Content.ReadFromJsonAsync<BunnyVideoResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Bunny create video response was empty.");

        return model.ToDto();
    }

    public async Task<BunnyFetchVideoResultDto> FetchVideoAsync(string url, string title, string? collectionId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var endpoint = $"{StreamBaseUrl}/library/{_libraryId}/videos/fetch";
        if (!string.IsNullOrWhiteSpace(collectionId))
        {
            endpoint += $"?collectionId={Uri.EscapeDataString(collectionId)}";
        }

        using var request = CreateRequest(HttpMethod.Post, endpoint);
        request.Content = JsonContent.Create(new { url, title, headers = new Dictionary<string, string>() }, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<BunnyApiResultResponse>(JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new BunnyFetchVideoResultDto(false, result?.Message ?? response.ReasonPhrase, (int)response.StatusCode);
        }

        return new BunnyFetchVideoResultDto(result?.Success ?? true, result?.Message, result?.StatusCode ?? (int)response.StatusCode);
    }

    public async Task<BunnyStreamVideoDto?> GetVideoAsync(string videoGuid, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Get, $"{StreamBaseUrl}/library/{_libraryId}/videos/{Uri.EscapeDataString(videoGuid)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var model = await response.Content.ReadFromJsonAsync<BunnyVideoResponse>(JsonOptions, cancellationToken);
        return model?.ToDto();
    }

    public async Task<IReadOnlyList<BunnyStreamVideoDto>> ListVideosAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Get, $"{StreamBaseUrl}/library/{_libraryId}/videos");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var model = await response.Content.ReadFromJsonAsync<BunnyVideoListResponse>(JsonOptions, cancellationToken);
        return model?.Items?.Select(item => item.ToDto()).ToList() ?? [];
    }

    public async Task<BunnyVideoStorageDto?> GetVideoStorageAsync(string videoGuid, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Get, $"{StreamBaseUrl}/library/{_libraryId}/videos/{Uri.EscapeDataString(videoGuid)}/storage");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var model = await response.Content.ReadFromJsonAsync<BunnyStorageResponse>(JsonOptions, cancellationToken);
        return model?.Data?.ToDto();
    }

    public async Task<BunnyVideoLibraryDto?> GetVideoLibraryAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Get, $"{CoreBaseUrl}/videolibrary/{_libraryId}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var model = await response.Content.ReadFromJsonAsync<BunnyLibraryResponse>(JsonOptions, cancellationToken);
        return model is null ? null : new BunnyVideoLibraryDto(model.Id, model.TrafficUsage, model.StorageUsage, model.VideoCount);
    }

    public BunnyTusUploadSignatureDto CreateTusUploadSignature(string videoGuid, TimeSpan expiresIn)
    {
        EnsureConfigured();
        var expires = DateTimeOffset.UtcNow.Add(expiresIn).ToUnixTimeSeconds();
        var payload = $"{_libraryId}{_apiKey}{expires}{videoGuid}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();
        return new BunnyTusUploadSignatureDto(_libraryId, videoGuid, TusEndpoint, signature, expires);
    }

    public async Task TriggerSmartActionsAsync(string videoGuid, BunnySmartActionsRequest request, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var httpRequest = CreateRequest(HttpMethod.Post, $"{StreamBaseUrl}/library/{_libraryId}/videos/{Uri.EscapeDataString(videoGuid)}/smart");
        httpRequest.Content = JsonContent.Create(new
        {
            generateTitle = request.GenerateTitle,
            generateDescription = request.GenerateDescription,
            generateChapters = request.GenerateChapters,
            generateMoments = request.GenerateMoments,
            sourceLanguage = request.SourceLanguage
        }, options: JsonOptions);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("AccessKey", _apiKey);
        return request;
    }

    private void EnsureConfigured()
    {
        if (_libraryId <= 0 || string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Bunny Stream is not configured. Set BunnyStream:LibraryId and BunnyStream:ApiKey.");
        }
    }

    private sealed record BunnyApiResultResponse(bool Success, string? Message, int StatusCode);

    private sealed record BunnyVideoListResponse(List<BunnyVideoResponse>? Items);

    private sealed record BunnyVideoResponse(
        long VideoLibraryId,
        string Guid,
        string Title,
        int Status,
        int EncodeProgress,
        long StorageSize,
        int Length,
        long Views,
        long TotalWatchTime,
        string? CollectionId,
        bool HasMP4Fallback,
        bool HasOriginal)
    {
        public BunnyStreamVideoDto ToDto()
        {
            return new BunnyStreamVideoDto(VideoLibraryId, Guid, Title, Status, EncodeProgress, StorageSize, Length, Views, TotalWatchTime, CollectionId, HasMP4Fallback, HasOriginal);
        }
    }

    private sealed record BunnyStorageResponse(BunnyStorageDataResponse? Data);

    private sealed record BunnyStorageDataResponse(
        object? Encoded,
        long Thumbnails,
        long Previews,
        long Originals,
        long Mp4Fallback,
        long Miscellaneous,
        DateTime? CalculatedAt)
    {
        public BunnyVideoStorageDto ToDto()
        {
            return new BunnyVideoStorageDto(SumEncodedBytes(Encoded), Thumbnails, Previews, Originals, Mp4Fallback, Miscellaneous, CalculatedAt);
        }

        private static long SumEncodedBytes(object? encoded)
        {
            if (encoded is null)
            {
                return 0;
            }

            if (encoded is JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.Number when element.TryGetInt64(out var value) => value,
                    JsonValueKind.Object => element.EnumerateObject().Sum(property => property.Value.TryGetInt64(out var value) ? value : 0),
                    _ => 0
                };
            }

            return 0;
        }
    }

    private sealed record BunnyLibraryResponse(long Id, long TrafficUsage, long StorageUsage, int VideoCount);
}
