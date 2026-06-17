namespace NaderGorge.Application.Interfaces;

public interface IBunnyStreamClient
{
    Task<BunnyStreamVideoDto> CreateVideoAsync(string title, string? collectionId, CancellationToken cancellationToken);
    Task<BunnyFetchVideoResultDto> FetchVideoAsync(string url, string title, string? collectionId, CancellationToken cancellationToken);
    Task<BunnyStreamVideoDto?> GetVideoAsync(string videoGuid, CancellationToken cancellationToken);
    Task<IReadOnlyList<BunnyStreamVideoDto>> ListVideosAsync(CancellationToken cancellationToken);
    Task<BunnyVideoStorageDto?> GetVideoStorageAsync(string videoGuid, CancellationToken cancellationToken);
    Task<BunnyVideoLibraryDto?> GetVideoLibraryAsync(CancellationToken cancellationToken);
    BunnyTusUploadSignatureDto CreateTusUploadSignature(string videoGuid, TimeSpan expiresIn);
    Task TriggerSmartActionsAsync(string videoGuid, BunnySmartActionsRequest request, CancellationToken cancellationToken);
}

public sealed record BunnyStreamVideoDto(
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
    bool HasMp4Fallback,
    bool HasOriginal);

public sealed record BunnyFetchVideoResultDto(bool Success, string? Message, int StatusCode);

public sealed record BunnyVideoStorageDto(
    long EncodedBytes,
    long ThumbnailsBytes,
    long PreviewsBytes,
    long OriginalsBytes,
    long Mp4FallbackBytes,
    long MiscellaneousBytes,
    DateTime? CalculatedAtUtc)
{
    public long TotalBytes => EncodedBytes + ThumbnailsBytes + PreviewsBytes + OriginalsBytes + Mp4FallbackBytes + MiscellaneousBytes;
}

public sealed record BunnyVideoLibraryDto(long Id, long TrafficUsage, long StorageUsage, int VideoCount);

public sealed record BunnyTusUploadSignatureDto(
    long LibraryId,
    string VideoId,
    string TusEndpoint,
    string AuthorizationSignature,
    long AuthorizationExpire);

public sealed record BunnySmartActionsRequest(
    bool? GenerateTitle,
    bool? GenerateDescription,
    bool? GenerateChapters,
    bool? GenerateMoments,
    string? SourceLanguage);
