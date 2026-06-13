using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;
using NaderGorge.API.Services;
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
            var storage = new ContentImageStorage(new TestWebHostEnvironment(webRoot));
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

    private sealed class TestWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "NaderGorge.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.GetDirectoryName(webRootPath)!;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
