namespace NaderGorge.Infrastructure.Services;

public interface IWhatsAppAudioProcess
{
    Task<byte[]> TranscodeToMp3MonoAsync(
        ReadOnlyMemory<byte> source,
        int maximumOutputBytes,
        CancellationToken cancellationToken);
}
