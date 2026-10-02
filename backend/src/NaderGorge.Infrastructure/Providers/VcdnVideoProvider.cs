using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Providers;

public sealed class VcdnVideoProvider : IVideoProvider
{
    public string Name => VideoProviders.Vcdn;
    public string ExtractVideoId(string url) => VcdnVideoReference.ExtractVideoId(url) ?? string.Empty;
    public string GetEmbedUrl(string videoId) => VcdnVideoReference.ExtractVideoId(videoId) is { } id
        ? $"https://embed.vcdn.me/embed/{id}" : string.Empty;
}
