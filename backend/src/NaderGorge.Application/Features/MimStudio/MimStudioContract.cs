namespace NaderGorge.Application.Features.MimStudio;

public sealed record MimShot(int Start, int End, string Title, string Action, string Camera, string Dialogue, string Sound);
public sealed record MimScene(string Title, string EducationalPoint, Guid[] SourceChapterIds, MimShot[] Shots, string Prompt);
public sealed record MimStudioDocument(int SchemaVersion, string Title, string Premise, string Style, string Continuity, MimScene[] Scenes);
public sealed record SaveMimStudio(Guid? Version, Guid SourceVideoId, int SourceRevision, MimStudioDocument Document);
public sealed record MimStudioSnapshot(Guid Version, Guid SourceVideoId, int SourceRevision, bool Stale, MimStudioDocument Document, DateTime? UpdatedAt);

public static class MimStudioContract
{
    public static void Validate(MimStudioDocument? doc, IReadOnlySet<Guid> chapterIds)
    {
        if (doc is null || doc.SchemaVersion != 1 || !Text(doc.Title, 200) || !Text(doc.Premise, 3000) ||
            !Text(doc.Style, 3000) || !Text(doc.Continuity, 6000) || doc.Scenes is null || doc.Scenes.Length != 4)
            throw new ArgumentException("السيناريو لازم يحتوي على أربعة مشاهد وبيانات القصة والشخصيات.");
        foreach (var scene in doc.Scenes)
        {
            if (scene is null || !Text(scene.Title, 200) || !Text(scene.EducationalPoint, 2000) || !Text(scene.Prompt, 16000) ||
                scene.SourceChapterIds is null || scene.SourceChapterIds.Length is < 1 or > 16 ||
                scene.SourceChapterIds.Distinct().Count() != scene.SourceChapterIds.Length ||
                scene.SourceChapterIds.Any(id => !chapterIds.Contains(id)) || scene.Shots is null || scene.Shots.Length is < 6 or > 15)
                throw new ArgumentException("راجع عنوان المشهد وبرومبته والكادرات ومراجع فصول الحصة.");
            var end = 0;
            foreach (var shot in scene.Shots)
            {
                if (shot is null || shot.Start != end || shot.End <= shot.Start || shot.End > 30 ||
                    !Text(shot.Title, 150) || !Text(shot.Action, 2000) || !Text(shot.Camera, 1000) ||
                    !Text(shot.Dialogue, 1500, true) || !Text(shot.Sound, 1000, true))
                    throw new ArgumentException("الكادرات لازم تكون متتابعة من صفر إلى ٣٠ ثانية، مع حركة وكاميرا لكل كادر.");
                end = shot.End;
            }
            if (end != 30) throw new ArgumentException("مدة كل مشهد لازم تكون ٣٠ ثانية بالضبط.");
        }
    }

    private static bool Text(string? value, int max, bool empty = false) =>
        value is not null && value.Length <= max && (empty || !string.IsNullOrWhiteSpace(value));
}
