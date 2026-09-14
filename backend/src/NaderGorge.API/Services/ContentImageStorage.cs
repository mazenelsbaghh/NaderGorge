using NaderGorge.Application.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace NaderGorge.API.Services;

public sealed class ContentImageStorage : IContentImageStorage
{
    private const int MaximumDimension = 1200;
    private const long MaximumPixelCount = 40_000_000;
    private readonly ISharedFileStorage _sharedStorage;

    public ContentImageStorage(ISharedFileStorage sharedStorage)
    {
        _sharedStorage = sharedStorage;
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
        await using var encoded = new MemoryStream();
        await image.SaveAsWebpAsync(
            encoded,
            new WebpEncoder { Quality = 75 },
            cancellationToken);
        encoded.Position = 0;
        await _sharedStorage.WriteAsync(
            SharedFileArea.Public,
            Path.Combine(relativeDirectory, randomFileName),
            encoded,
            cancellationToken);

        return $"/{relativeDirectory.Replace(Path.DirectorySeparatorChar, '/')}/{randomFileName}";
    }
}
