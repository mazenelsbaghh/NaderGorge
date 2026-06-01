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

        // Ensure it has oid= and id=
        var match = Regex.Match(url, @"oid=([^&]+)&id=([^&]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return $"oid={match.Groups[1].Value}&id={match.Groups[2].Value}";
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
