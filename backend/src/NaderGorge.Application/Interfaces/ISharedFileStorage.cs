namespace NaderGorge.Application.Interfaces;

public enum SharedFileArea
{
    Public,
    Protected,
    Private,
    LiveSupport,
    Subtitles,
    MindMaps
}

public sealed record SharedFileWriteResult(
    string RelativePath,
    long SizeBytes,
    string Sha256);

public interface ISharedFileStorage
{
    Task<SharedFileWriteResult> WriteAsync(
        SharedFileArea area,
        string relativePath,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        SharedFileArea area,
        string relativePath,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        SharedFileArea area,
        string relativePath,
        CancellationToken cancellationToken);
}
