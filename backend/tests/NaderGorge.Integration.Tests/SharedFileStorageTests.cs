using System.Security.Cryptography;
using NaderGorge.Application.Interfaces;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests;

public sealed class SharedFileStorageTests : IDisposable
{
    private readonly string _temporaryRoot =
        Path.Combine(Path.GetTempPath(), $"massar-shared-storage-{Guid.NewGuid():N}");

    [Fact]
    public async Task WriteReadDelete_IsAtomicAndReturnsChecksum()
    {
        var storage = CreateStorage();
        var bytes = RandomNumberGenerator.GetBytes(256 * 1024);
        await using var input = new MemoryStream(bytes, writable: false);

        var result = await storage.WriteAsync(
            SharedFileArea.Protected,
            "resources/2026/07/sample.bin",
            input,
            CancellationToken.None);

        Assert.Equal(bytes.Length, result.SizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), result.Sha256);
        Assert.Empty(Directory.EnumerateFiles(_temporaryRoot, "*.tmp", SearchOption.AllDirectories));

        await using var stored = await storage.OpenReadAsync(
            SharedFileArea.Protected,
            result.RelativePath,
            CancellationToken.None);
        using var output = new MemoryStream();
        await stored.CopyToAsync(output);
        Assert.Equal(bytes, output.ToArray());

        await storage.DeleteAsync(
            SharedFileArea.Protected,
            result.RelativePath,
            CancellationToken.None);
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            storage.OpenReadAsync(
                SharedFileArea.Protected,
                result.RelativePath,
                CancellationToken.None));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("safe/../../secret")]
    [InlineData("/absolute/path")]
    [InlineData("")]
    public async Task UnsafePaths_AreRejected(string relativePath)
    {
        var storage = CreateStorage();
        await using var input = new MemoryStream("content"u8.ToArray());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.WriteAsync(
                SharedFileArea.Private,
                relativePath,
                input,
                CancellationToken.None));
    }

    [Fact]
    public async Task FailedWrite_RemovesTemporaryFileAndDoesNotPublishDestination()
    {
        var storage = CreateStorage();
        await using var input = new ThrowingStream();

        await Assert.ThrowsAsync<IOException>(() =>
            storage.WriteAsync(
                SharedFileArea.Public,
                "uploads/failure.bin",
                input,
                CancellationToken.None));

        Assert.False(File.Exists(Path.Combine(_temporaryRoot, "public", "uploads", "failure.bin")));
        Assert.Empty(Directory.EnumerateFiles(_temporaryRoot, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SymbolicLinkInsideRoot_CannotEscapeStorageArea()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var storage = CreateStorage();
        var publicRoot = Path.Combine(_temporaryRoot, "public");
        var outside = Path.Combine(_temporaryRoot, "outside");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Path.Combine(publicRoot, "escape"), outside);
        await using var input = new MemoryStream("blocked"u8.ToArray());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.WriteAsync(
                SharedFileArea.Public,
                "escape/file.txt",
                input,
                CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(outside, "file.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private SharedFileStorage CreateStorage() =>
        new(new Dictionary<SharedFileArea, string>
        {
            [SharedFileArea.Public] = Path.Combine(_temporaryRoot, "public"),
            [SharedFileArea.Protected] = Path.Combine(_temporaryRoot, "protected"),
            [SharedFileArea.Private] = Path.Combine(_temporaryRoot, "private")
        });

    private sealed class ThrowingStream : Stream
    {
        private int _reads;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_reads++ == 0)
            {
                buffer.Span[0] = 42;
                return ValueTask.FromResult(1);
            }
            throw new IOException("Injected read failure.");
        }
    }
}
