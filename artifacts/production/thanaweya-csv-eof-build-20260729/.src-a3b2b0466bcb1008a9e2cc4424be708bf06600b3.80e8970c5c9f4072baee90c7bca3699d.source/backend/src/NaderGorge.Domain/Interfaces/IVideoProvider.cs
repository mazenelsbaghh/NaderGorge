namespace NaderGorge.Domain.Interfaces;

public interface IVideoProvider
{
    string Name { get; }

    // E.g., parse a URL "https://www.youtube.com/watch?v=dQw4w9WgXcQ" to "dQw4w9WgXcQ"
    string ExtractVideoId(string url);

    // E.g., convert "dQw4w9WgXcQ" to an embeddable URL like "https://www.youtube.com/embed/dQw4w9WgXcQ"
    string GetEmbedUrl(string videoId);
}
