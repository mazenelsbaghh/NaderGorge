using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LearningCenter;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.VideoLearning;

public sealed class VideoLearningEvidence(IAppDbContext db)
{
    public async Task<List<LearningAttemptEvidence>> ReadAsync(Guid[] packageIds, int days, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var query = db.VideoLearningEntries.AsNoTracking().Where(e => e.Kind == "answer" && e.CreatedAt >= since &&
            e.Student.IsActive && !e.Student.IsDeleted && e.SourceRevision == e.LessonVideo.SourceRevision &&
            packageIds.Contains(e.LessonVideo.Lesson.ContentSection.Term.PackageId) &&
            db.VideoLearningConfigurations.Any(c => c.LessonVideoId == e.LessonVideoId && c.Version == e.ConfigurationVersion));
        if (await query.CountAsync(ct) > 50000) throw new ArgumentException("عدد تفاعلات الفيديو كبير. اختر كورسًا أو فترة أقصر.");
        var rows = await query.Select(e => new { e.Id, e.StudentId, Name = e.Student.FullName, e.LessonVideoId, e.LessonVideo.LessonId,
            PackageId = e.LessonVideo.Lesson.ContentSection.Term.PackageId, e.ConfigurationVersion, e.ActivityId, e.Correct, e.Text, e.CreatedAt }).ToListAsync(ct);
        var ids = rows.Select(r => r.LessonVideoId).Distinct().ToArray();
        var configs = await db.VideoLearningConfigurations.AsNoTracking().Where(c => ids.Contains(c.LessonVideoId)).ToListAsync(ct);
        var documents = configs.ToDictionary(c => c.LessonVideoId, c => JsonSerializer.Deserialize<LearningDocument>(c.DocumentJson, VideoLearningService.Json)!);
        var result = new List<LearningAttemptEvidence>();
        foreach (var row in rows)
        {
            var activity = documents[row.LessonVideoId].Activities.FirstOrDefault(a => a.Id == row.ActivityId);
            if (activity is null || string.IsNullOrWhiteSpace(activity.Concept)) continue;
            var correct = row.Correct == true;
            var wrong = !correct && int.TryParse(row.Text, out var option) && option >= 0 && option < activity.Options.Length ? activity.Options[option] : null;
            result.Add(new(row.Id, row.StudentId, row.Name, row.LessonVideoId, row.PackageId, row.CreatedAt,
                correct ? 100 : 0, $"video:{row.ConfigurationVersion}",
                [new(activity.Id, row.LessonId, activity.Concept, activity.Title, correct, wrong, 1, correct ? 1 : 0)]));
        }
        return result;
    }
}
