using NaderGorge.Application.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace NaderGorge.API.Services;

public sealed class ContentImageStorage : IContentImageStorage
{
    private const int MaximumDimension = 1200;
    private const long MaximumPixelCount = 40_000_000;
    private readonly IWebHostEnvironment _environment;

    public ContentImageStorage(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveAsWebpAsync(
        Stream imageStream,
        string contentFolder,
        CancellationToken cancellationToken)
    {
        var imageInfo = await Image.IdentifyAsync(imageStream, cancellationToken);
        if ((long)imageInfo.Width * imageInfo.Height > MaximumPixelCount)
        {
            throw new InvalidImageContentException("Image dimensions are too large.");
        }

        imageStream.Position = 0;
        using var image = await Image.LoadAsync(imageStream, cancellationToken);
        
        // Clear metadata to reduce file size
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.XmpProfile = null;

        image.Mutate(context =>
        {
            context.AutoOrient();
            if (image.Width > MaximumDimension || image.Height > MaximumDimension)
            {
                context.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaximumDimension, MaximumDimension)
                });
            }
        });

        var randomFileName = $"{Guid.NewGuid():N}.webp";
        var relativeDirectory = Path.Combine("uploads", "content", contentFolder);
        var physicalDirectory = Path.Combine(_environment.WebRootPath, relativeDirectory);
        Directory.CreateDirectory(physicalDirectory);

        var physicalPath = Path.Combine(physicalDirectory, randomFileName);
        await image.SaveAsWebpAsync(
            physicalPath,
            new WebpEncoder { Quality = 75 },
            cancellationToken);

        return $"/{relativeDirectory.Replace(Path.DirectorySeparatorChar, '/')}/{randomFileName}";
    }
}
