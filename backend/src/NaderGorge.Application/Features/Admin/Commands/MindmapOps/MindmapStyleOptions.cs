namespace NaderGorge.Application.Features.Admin.Commands.MindmapOps;

public static class MindmapStyleOptions
{
    private static readonly HashSet<string> VisualStyles =
    [
        "editorial-infographic", "cinematic-3d", "scientific-notebook",
        "museum-exhibit", "motion-poster", "random"
    ];

    private static readonly HashSet<string> TeacherStyles =
    [
        "photorealistic", "cartoon", "3d-character", "digital-illustration", "random"
    ];

    public static string[] ValidVisualStyles(IEnumerable<string>? styles) =>
        Normalize(styles, VisualStyles, "editorial-infographic");

    public static string[] ValidTeacherStyles(IEnumerable<string>? styles) =>
        Normalize(styles, TeacherStyles, "photorealistic");

    private static string[] Normalize(IEnumerable<string>? styles, HashSet<string> allowed, string fallback)
    {
        var selected = styles?
            .Where(allowed.Contains)
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        return selected is { Length: > 0 } ? selected : [fallback];
    }
}
