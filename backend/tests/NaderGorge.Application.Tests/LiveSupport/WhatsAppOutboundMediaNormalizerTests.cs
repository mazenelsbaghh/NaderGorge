using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class WhatsAppOutboundMediaNormalizerTests
{
    [Fact]
    public async Task ProductionRegression_20260829_Code131053_RealFfmpeg_ProducesMp3Frames()
    {
        var audioProcess = new FfmpegWhatsAppAudioProcess(FfmpegExecutable());

        var mp3 = await audioProcess.TranscodeToMp3MonoAsync(
            SilentWave(), 1024 * 1024, CancellationToken.None);

        Assert.True(mp3.Length > 10);
        Assert.Equal("ID3", System.Text.Encoding.ASCII.GetString(mp3, 0, 3));
        var frameOffset = 10 + ((mp3[6] & 0x7F) << 21) + ((mp3[7] & 0x7F) << 14) +
            ((mp3[8] & 0x7F) << 7) + (mp3[9] & 0x7F);
        Assert.InRange(frameOffset, 10, mp3.Length - 2);
        Assert.Equal(0xFF, mp3[frameOffset]);
        Assert.Equal(0xE0, mp3[frameOffset + 1] & 0xE0);
    }

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
    public async Task SupportedStoredAudio_UsesProcessBoundaryAndReturnsGenericMp3(
        string contentType,
        string fileName)
    {
        byte[] sourceBytes = [1, 2, 3, 4];
        byte[] mp3Bytes = [0x49, 0x44, 0x33, 1, 2, 3];
        var audioProcess = new RecordingAudioProcess(mp3Bytes);
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
        Assert.Equal("voice.mp3", normalized.FileName);
        Assert.Equal("audio/mpeg", normalized.ContentType);
        Assert.Equal(mp3Bytes, normalized.Content);
        Assert.Equal(sourceBytes, audioProcess.Source);
        Assert.Equal(16 * 1024 * 1024, audioProcess.MaximumOutputBytes);
    }

    private sealed class RejectingAudioProcess : IWhatsAppAudioProcess
    {
        public Task<byte[]> TranscodeToMp3MonoAsync(
            ReadOnlyMemory<byte> source,
            int maximumOutputBytes,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No audio conversion expected.");
    }

    private sealed class RecordingAudioProcess(byte[] output) : IWhatsAppAudioProcess
    {
        public byte[]? Source { get; private set; }
        public int MaximumOutputBytes { get; private set; }

        public Task<byte[]> TranscodeToMp3MonoAsync(
            ReadOnlyMemory<byte> source,
            int maximumOutputBytes,
            CancellationToken cancellationToken)
        {
            Source = source.ToArray();
            MaximumOutputBytes = maximumOutputBytes;
            return Task.FromResult(output);
        }
    }

    private static string FfmpegExecutable() =>
        new[] { "/usr/bin/ffmpeg", "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg" }
            .FirstOrDefault(File.Exists)
        ?? throw new InvalidOperationException("FFmpeg is required for WhatsApp audio tests.");

    private static byte[] SilentWave()
    {
        const int sampleRate = 8_000;
        const int sampleCount = 800;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + sampleCount * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(sampleCount * 2);
        writer.Write(new byte[sampleCount * 2]);
        return stream.ToArray();
    }
}
