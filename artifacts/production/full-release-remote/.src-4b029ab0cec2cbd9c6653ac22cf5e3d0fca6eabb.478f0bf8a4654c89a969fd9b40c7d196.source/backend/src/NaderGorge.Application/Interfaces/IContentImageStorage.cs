namespace NaderGorge.Application.Interfaces;

public interface IContentImageStorage
{
    Task<string> SaveAsWebpAsync(
        Stream imageStream,
        string contentFolder,
        CancellationToken cancellationToken);
}
