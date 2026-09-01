using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Providers;

public sealed class BunnyVideoProvider : IVideoProvider
{
    public string Name => "bunny";

    public string ExtractVideoId(string url)
    {
        return BunnyVideoReferenceParser.TryParse(url, out var reference)
            ? reference!.VideoGuid
            : string.Empty;
    }

    public string GetEmbedUrl(string videoId)
    {
        // A Bunny video GUID is not globally unique for playback; callers must resolve
        // its persisted BunnyStreamLibrary instead of falling back to a process-wide ID.
        return string.Empty;
    }
}
