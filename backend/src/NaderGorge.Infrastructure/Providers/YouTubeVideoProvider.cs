using System.Text.RegularExpressions;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Providers;

public class YouTubeVideoProvider : IVideoProvider
{
    public string ExtractVideoId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var regex = new Regex(@"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})", RegexOptions.IgnoreCase);
        var match = regex.Match(url);
        
        return match.Success ? match.Groups[1].Value : url; // fallback mapping if what's passed is already an ID
    }

    public string GetEmbedUrl(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            return string.Empty;
            
        return $"https://www.youtube.com/embed/{videoId}?rel=0&modestbranding=1";
    }
}
