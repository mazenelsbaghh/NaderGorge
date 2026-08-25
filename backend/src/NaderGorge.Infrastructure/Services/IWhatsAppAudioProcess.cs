namespace NaderGorge.Infrastructure.Services;

public interface IWhatsAppAudioProcess
{
    Task<byte[]> TranscodeToOggOpusMonoAsync(
        ReadOnlyMemory<byte> source,
        int maximumOutputBytes,
        CancellationToken cancellationToken);
}
