using NaderGorge.API.Services;
using NaderGorge.Application.Interfaces;
using NaderGorge.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaderGorge.Application.Tests;

public class ContentImageStorageTests
{
    [Fact]
    public async Task PngUpload_IsSavedAsRandomlyNamedWebp()
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"content-image-{Guid.NewGuid():N}");
        var webRoot = Path.Combine(temporaryRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);

        try
        {
            var storage = new ContentImageStorage(CreateSharedStorage(webRoot));
            await using var pngStream = new MemoryStream();
            using (var sourceImage = new Image<Rgba32>(8, 8))
            {
                await sourceImage.SaveAsPngAsync(pngStream);
            }
            pngStream.Position = 0;

            var imageUrl = await storage.SaveAsWebpAsync(pngStream, "package", CancellationToken.None);

            Assert.Matches("^/uploads/content/package/[0-9a-f]{32}\\.webp$", imageUrl);
            var savedImagePath = Path.Combine(webRoot, imageUrl.TrimStart('/'));
            Assert.True(File.Exists(savedImagePath));
            Assert.Equal("Webp", Image.DetectFormat(savedImagePath).Name);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SpoofedImageBytes_AreRejectedByImageDecoder()
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"content-image-{Guid.NewGuid():N}");
        var webRoot = Path.Combine(temporaryRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);

        try
        {
            var storage = new ContentImageStorage(CreateSharedStorage(webRoot));
            await using var spoofedStream = new MemoryStream("<html>not an image</html>"u8.ToArray());

            await Assert.ThrowsAsync<UnknownImageFormatException>(() =>
                storage.SaveAsWebpAsync(spoofedStream, "package", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static SharedFileStorage CreateSharedStorage(string webRoot) =>
        new(new Dictionary<SharedFileArea, string>
        {
            [SharedFileArea.Public] = webRoot
        });
}
