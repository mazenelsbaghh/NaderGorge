namespace NaderGorge.Application.Interfaces;

public interface IBunnyStreamClient
{
    long LibraryId { get; }
    Task<BunnyStreamValidationResult> ValidateLibraryAccessAsync(CancellationToken cancellationToken);
    Task<BunnyStreamVideoDto> CreateVideoAsync(string title, string? collectionId, CancellationToken cancellationToken);
    Task<BunnyFetchVideoResultDto> FetchVideoAsync(string videoGuid, string url, CancellationToken cancellationToken);
    Task DeleteVideoAsync(string videoGuid, CancellationToken cancellationToken);
    Task<BunnyStreamVideoDto?> GetVideoAsync(string videoGuid, CancellationToken cancellationToken);
    Task<IReadOnlyList<BunnyStreamVideoDto>> ListVideosAsync(CancellationToken cancellationToken);
    Task<BunnyVideoStorageDto?> GetVideoStorageAsync(string videoGuid, CancellationToken cancellationToken);
    Task<BunnyVideoLibraryDto?> GetVideoLibraryAsync(CancellationToken cancellationToken);
    BunnyTusUploadSignatureDto CreateTusUploadSignature(string videoGuid, TimeSpan expiresIn);
    Task TriggerSmartActionsAsync(string videoGuid, BunnySmartActionsRequest request, CancellationToken cancellationToken);
}

public interface IBunnyStreamClientFactory
{
    IBunnyStreamClient Create(long libraryId, string apiKey);
}

public interface IBunnyStreamLibrarySecretProtector
{
    byte[] Protect(Guid libraryId, string apiKey);
    string Unprotect(Guid libraryId, ReadOnlySpan<byte> ciphertext);
}

public interface IBunnyStreamLibraryAccessService
{
    Task<BunnyStreamLibraryAccessResult> ResolveAsync(
        Guid libraryId,
        bool requireActive,
        CancellationToken cancellationToken);

    Task<BunnyStreamLibraryAccessResult> ResolveByExternalIdAsync(
        long externalLibraryId,
        bool requireActive,
        CancellationToken cancellationToken);
}

public sealed record BunnyStreamLibraryAccess(
    Guid Id,
    string Name,
    long ExternalLibraryId,
    string ApiKey,
    bool IsActive);

public sealed record BunnyStreamLibraryAccessResult(
    bool Success,
    BunnyStreamLibraryAccess? Access,
    string? ErrorCode,
    string? Message)
{
    public static BunnyStreamLibraryAccessResult Ok(BunnyStreamLibraryAccess access) =>
        new(true, access, null, null);

    public static BunnyStreamLibraryAccessResult Fail(string code, string message) =>
        new(false, null, code, message);
}

public sealed record BunnyStreamValidationResult(bool Success, string? ErrorCode, string? Message);

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
