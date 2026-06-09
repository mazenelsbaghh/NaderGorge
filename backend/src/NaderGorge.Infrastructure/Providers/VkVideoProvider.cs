using System.Text.RegularExpressions;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Providers;

public class VkVideoProvider : IVideoProvider
{
    public string Name => "vk";

    public string ExtractVideoId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        // Try standard embed format query parameters first
        var embedMatch = Regex.Match(url, @"oid=([^&]+)&id=([^&]+)", RegexOptions.IgnoreCase);
        if (embedMatch.Success)
        {
            return $"oid={embedMatch.Groups[1].Value}&id={embedMatch.Groups[2].Value}";
        }

        // Try standard VK video/clip URL format: vk.com/video-123456_7891011 or vk.com/clip-123456_7891011
        var urlMatch = Regex.Match(url, @"(?:video|clip)(-?\d+)_(\d+)", RegexOptions.IgnoreCase);
        if (urlMatch.Success)
        {
            return $"oid={urlMatch.Groups[1].Value}&id={urlMatch.Groups[2].Value}";
        }

        return url; // fallback mapping if what's passed is already an ID (though unlikely for VK as it requires both)
    }

    public string GetEmbedUrl(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            return string.Empty;

        // videoId is already stored as "oid=-22822305&id=456241864" based on the frontend logic
        return $"https://vk.com/video_ext.php?{videoId}&hd=2&js_api=1";
    }
}
