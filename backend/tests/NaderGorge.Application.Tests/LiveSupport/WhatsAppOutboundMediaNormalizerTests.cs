using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class WhatsAppOutboundMediaNormalizerTests
{
    [Fact]
    public async Task ProductionRegression_20260825_StoredWebp_NormalizesToBoundedOpaqueJpeg()
    {
        await using var webp = new MemoryStream();
        using (var sourceImage = new Image<Rgba32>(2_500, 2, new Rgba32(0, 0, 0, 0)))
            await sourceImage.SaveAsWebpAsync(webp, CancellationToken.None);
        var webpBytes = webp.ToArray();
        var normalizer = new WhatsAppOutboundMediaNormalizer(new RejectingAudioProcess());
        await using var source = new MemoryStream(webpBytes, writable: false);

        var normalized = await normalizer.NormalizeAsync(
            new WhatsAppOutboundMediaSource(
                LiveSupportMessageType.Image,
                "legacy.webp",
                "image/webp",
                webpBytes.Length,
                source),
            CancellationToken.None);

        Assert.Equal("image", normalized.MediaType);
        Assert.Equal("legacy.jpg", normalized.FileName);
        Assert.Equal("image/jpeg", normalized.ContentType);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, normalized.Content[..3]);
        using var jpeg = Image.Load<Rgba32>(normalized.Content);
        Assert.Equal(2_048, jpeg.Width);
        Assert.All(new[] { jpeg[0, 0].R, jpeg[0, 0].G, jpeg[0, 0].B }, channel =>
            Assert.InRange(channel, (byte)240, byte.MaxValue));
    }

    [Theory]
    [InlineData("audio/mpeg", "voice.mp3")]
    [InlineData("audio/mp4", "voice.mp4")]
    [InlineData("audio/ogg", "voice.ogg")]
    [InlineData("audio/webm", "voice.webm")]
    public async Task SupportedStoredAudio_UsesProcessBoundaryAndReturnsOgg(
        string contentType,
        string fileName)
    {
        byte[] sourceBytes = [1, 2, 3, 4];
        byte[] oggBytes = [0x4F, 0x67, 0x67, 0x53, 1, 2, 3];
        var audioProcess = new RecordingAudioProcess(oggBytes);
        var normalizer = new WhatsAppOutboundMediaNormalizer(audioProcess);
        await using var source = new MemoryStream(sourceBytes, writable: false);

        var normalized = await normalizer.NormalizeAsync(
            new WhatsAppOutboundMediaSource(
                LiveSupportMessageType.Audio,
                fileName,
                contentType,
                sourceBytes.Length,
                source),
            CancellationToken.None);

        Assert.Equal("audio", normalized.MediaType);
        Assert.Equal("voice.ogg", normalized.FileName);
        Assert.Equal("audio/ogg", normalized.ContentType);
        Assert.Equal(oggBytes, normalized.Content);
        Assert.Equal(sourceBytes, audioProcess.Source);
        Assert.Equal(16 * 1024 * 1024, audioProcess.MaximumOutputBytes);
    }

    private sealed class RejectingAudioProcess : IWhatsAppAudioProcess
    {
        public Task<byte[]> TranscodeToOggOpusMonoAsync(
            ReadOnlyMemory<byte> source,
            int maximumOutputBytes,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No audio conversion expected.");
    }

    private sealed class RecordingAudioProcess(byte[] output) : IWhatsAppAudioProcess
    {
        public byte[]? Source { get; private set; }
        public int MaximumOutputBytes { get; private set; }

        public Task<byte[]> TranscodeToOggOpusMonoAsync(
            ReadOnlyMemory<byte> source,
            int maximumOutputBytes,
            CancellationToken cancellationToken)
        {
            Source = source.ToArray();
            MaximumOutputBytes = maximumOutputBytes;
            return Task.FromResult(output);
        }
    }
}
