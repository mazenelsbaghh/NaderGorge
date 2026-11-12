using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed partial class LessonYouTubeLinks
{
    private const string StudentLessonsPath = "/student/lessons";
    private readonly ILookup<string, Destination> _destinations;

    private LessonYouTubeLinks(IEnumerable<Destination> destinations)
    {
        _destinations = destinations.ToLookup(video => video.ProviderId, StringComparer.Ordinal);
    }

    public bool HasDestinations => _destinations.Count > 0;

    public static async Task<LessonYouTubeLinks> ResolveAsync(
        IAppDbContext db, IEnumerable<string?> values, CancellationToken ct)
    {
        var sourceIds = values.OfType<string>().SelectMany(value => YouTubeUrl().Matches(value))
            .Select(match => ReadVideoId(match.Value.TrimEnd('.', ',')))
            .OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        if (sourceIds.Length == 0) return new LessonYouTubeLinks([]);

        var destinations = await db.LessonVideos.AsNoTracking()
            .Where(video => video.Provider.Trim().ToLower() == VideoProviders.YouTube
                && sourceIds.Contains(video.ProviderVideoId))
            .OrderByDescending(video => video.IsActive && video.ArchiveMode == ContentArchiveMode.None)
            .ThenBy(video => video.Order).ThenBy(video => video.Id)
            .Select(video => new Destination(video.ProviderVideoId, video.LessonId, video.Id))
            .ToListAsync(ct);
        return new LessonYouTubeLinks(destinations);
    }

    [return: NotNullIfNotNull(nameof(text))]
    public string? Rewrite(string? text, Guid? preferredLessonId = null)
    {
        if (!HasDestinations || string.IsNullOrEmpty(text)) return text;
        return YouTubeUrl().Replace(text, match =>
        {
            var url = match.Value.TrimEnd('.', ',');
            var path = FindLessonPath(url, preferredLessonId);
            return path is null ? match.Value : path + match.Value[url.Length..];
        });
    }

    public string? FindLessonPath(string? url, Guid? preferredLessonId = null)
    {
        if (!HasDestinations) return null;
        var videoId = ReadVideoId(url);
        if (videoId is null) return null;

        var destinations = _destinations[videoId];
        var preferred = destinations.FirstOrDefault(destination => destination.LessonId == preferredLessonId);
        if (preferred is not null) return preferred.Path;

        // A source can be reused in several packages. Without lesson context, avoid choosing the wrong package.
        var lessons = destinations.Select(destination => destination.LessonId).Distinct().Take(2).ToArray();
        return lessons.Length switch
        {
            0 => null,
            1 => destinations.First().Path,
            _ => StudentLessonsPath
        };
    }

    private static string? ReadVideoId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var candidate = url.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal)) candidate = "https://" + candidate;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return null;

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var videoId = uri.Host switch
        {
            "youtu.be" or "www.youtu.be" when parts.Length == 1 => parts[0],
            var host when IsYouTubeHost(host) => ReadYouTubePath(parts, uri.Query),
            _ => null
        };
        return videoId is not null && VideoProviders.IsYouTubeVideoId(videoId) ? videoId : null;
    }

    private static bool IsYouTubeHost(string host) => host is "youtube.com" or "youtube-nocookie.com"
        || host.EndsWith(".youtube.com", StringComparison.Ordinal)
        || host.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal);

    private static string? ReadYouTubePath(string[] parts, string query) => parts switch
    {
        ["watch"] => query.TrimStart('?').Replace("&amp;", "&", StringComparison.Ordinal)
            .Split('&').FirstOrDefault(parameter => parameter.StartsWith("v=", StringComparison.Ordinal))?[2..],
        ["embed" or "shorts" or "live" or "v" or "e", var videoId] => videoId,
        _ => null
    };

    [GeneratedRegex("""(?<![\p{L}\p{N}_./@-])(?:https?://)?(?:[a-z0-9-]+\.)*(?:youtube(?:-nocookie)?\.com|youtu\.be)/[^\s\p{Cf}<>"'()\[\]،؛؟]+""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex YouTubeUrl();

    private sealed record Destination(string ProviderId, Guid LessonId, Guid VideoId)
    {
        public string Path => $"{StudentLessonsPath}/{LessonId:D}?videoId={VideoId:D}";
    }
}
