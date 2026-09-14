using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LearningCenter;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.VideoLearning;

public sealed partial class VideoLearningService
{
    public async Task<LearningAiResult> AiAsync(Guid actor, Guid videoId, LearningAiRequest request, CancellationToken ct)
    {
        var author = request.Mode == "author";
        if (request.Mode is not ("author" or "simplify" or "example" or "quiz" or "foundation" or "ask" or "note") ||
            request.Id == Guid.Empty || request.Text is null || request.Text.Length > 1000)
            throw new ArgumentException("راجع طلب المساعدة.");
        if (author) await RequireAdminAsync(actor, ct);
        var video = await VideoAsync(actor, videoId, author, ct);
        var (config, document) = await ReadAsync(videoId, ct);
        CheckVersion(video, config, request.Version);
        if (author ? !document.Tools.AiAuthoring : !document.Tools.AiTutor)
            throw new ArgumentException("المساعدة الذكية غير مفعّلة لهذا الفيديو.");
        if (request.Seconds < 0 || request.Seconds > (await DurationAsync(videoId, ct) ?? 86400)) throw new ArgumentException("التوقيت غير صالح.");
        var chapters = await db.VideoChapters.AsNoTracking().Where(c => c.LessonVideoId == videoId)
            .OrderBy(c => c.StartTime).Take(150).ToListAsync(ct);
        var selected = author ? chapters : chapters.Where(c => c.StartTime <= request.Seconds && c.EndTime >= request.Seconds).Take(1).ToList();
        if (selected.Count == 0 || selected.All(c => string.IsNullOrWhiteSpace(c.SummaryText)))
            throw new ArgumentException("حلّل الفيديو وراجع ملخصات فصوله أولًا عشان المساعد يعتمد على محتوى الحصة.");
        var context = string.Join("\n", selected.Select(c => $"[{c.StartTime}-{c.EndTime}] {c.Title}\n{c.SummaryText}"));
        if (context.Length > 24000) context = context[..24000];
        VideoLearningEntry reservation;
        await using (var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var existing = await db.VideoLearningEntries.SingleOrDefaultAsync(e => e.Id == request.Id, ct);
            if (existing is not null)
            {
                if (existing.StudentId != actor || existing.LessonVideoId != videoId || existing.Kind != "ai" ||
                    existing.ConfigurationVersion != request.Version || existing.SourceRevision != video.SourceRevision ||
                    existing.Title != request.Mode || existing.Text != request.Text || existing.Seconds != request.Seconds)
                    throw new LearningConflictException("معرف الطلب مستخدم.");
                if (existing.ResultJson is null) throw new LearningConflictException("الطلب قيد التنفيذ أو لم يكتمل. حاول بطلب جديد بعد قليل.");
                return JsonSerializer.Deserialize<LearningAiResult>(existing.ResultJson, Json)!;
            }
            var today = DateTime.UtcNow.Date;
            var count = await db.VideoLearningEntries.CountAsync(e => e.StudentId == actor && e.Kind == "ai" &&
                e.LessonVideoId == videoId && e.CreatedAt >= today, ct);
            if (count >= (author ? 30 : document.Tools.AiDailyLimit)) throw new ArgumentException("وصلت لحد المساعدة الذكية اليومي لهذا الفيديو.");
            reservation = new VideoLearningEntry { Id = request.Id, StudentId = actor, LessonVideoId = videoId,
                SourceRevision = video.SourceRevision, ConfigurationVersion = request.Version, Kind = "ai",
                Seconds = request.Seconds, Title = request.Mode, Text = request.Text };
            db.VideoLearningEntries.Add(reservation);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        var result = await ai.GenerateAsync(request.Mode, request.Text, context, ct);
        if (result.Text is null || result.Text.Length > 6000 || result.Activities is null || result.Activities.Length > 30 || result.Activities.Any(a => a is null))
            throw new InvalidOperationException("AI_INVALID_RESPONSE");
        if (author)
        {
            result = result with { Activities = result.Activities.Select(a => a with { Id = Guid.NewGuid(), QuestionBankId = null, Required = false }).ToArray() };
            LearningRules.Validate(new(document.Tools, result.Activities), await DurationAsync(videoId, ct));
        }
        else result = result with { Activities = [] };
        reservation.ResultJson = JsonSerializer.Serialize(result, Json);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<object> ReportAsync(Guid actor, Guid videoId, CancellationToken ct)
    {
        var video = await VideoAsync(actor, videoId, true, ct);
        await new LearningCenterScope(db).LessonAsync(actor, video.LessonId, ct);
        var (config, document) = await ReadAsync(videoId, ct);
        var version = config?.Version ?? Guid.Empty;
        var answers = await db.VideoLearningEntries.AsNoTracking().Where(e => e.LessonVideoId == videoId &&
            e.ConfigurationVersion == version && e.Kind == "answer")
            .GroupBy(e => e.ActivityId).Select(g => new { ActivityId = g.Key, Students = g.Count(), Correct = g.Count(e => e.Correct == true) }).ToListAsync(ct);
        var questions = await db.VideoLearningEntries.AsNoTracking().Where(e => e.LessonVideoId == videoId &&
            e.SourceRevision == video.SourceRevision && e.Kind == "ask")
            .OrderByDescending(e => e.CreatedAt).Take(100)
            .Select(e => new { e.Id, e.Seconds, e.Text, e.CommentId, StudentName = e.Student.FullName }).ToListAsync(ct);
        return new { answers, questions, density = await DensityAsync(videoId, video.SourceRevision, ct),
            activities = document.Activities.Select(a => new { a.Id, a.Title, a.Concept, a.Seconds }) };
    }

    public async Task<object> ReviewAsync(Guid actor, CancellationToken ct)
    {
        var rows = await db.VideoLearningEntries.AsNoTracking().Where(e => e.StudentId == actor &&
            e.SourceRevision == e.LessonVideo.SourceRevision && e.LessonVideo.IsActive &&
            (e.Kind == "understanding" || ((e.Kind == "answer" || e.Kind == "mastery") &&
                db.VideoLearningConfigurations.Any(c => c.LessonVideoId == e.LessonVideoId && c.Version == e.ConfigurationVersion))))
            .OrderByDescending(e => e.CreatedAt).Take(2000)
            .Select(e => new { e.Id, e.LessonVideoId, e.LessonVideo.LessonId, VideoTitle = e.LessonVideo.Title,
                e.Seconds, e.Kind, e.Title, e.Text, e.Correct, e.ActivityId }).ToListAsync(ct);
        var difficult = rows.GroupBy(e => new { e.LessonVideoId, e.Kind, Key = e.ActivityId?.ToString() ?? (e.Seconds / 15).ToString() })
            .Select(g => g.First()).Where(e => e.Kind switch
            {
                "understanding" => e.Text != "understood", "mastery" => e.Text == "review", _ => e.Correct == false
            }).ToArray();
        var allowed = await access.GetAccessibleVideoIdsAsync(actor, difficult.Select(r => r.LessonVideoId).Distinct().ToArray(), ct);
        return difficult.Where(r => allowed.Contains(r.LessonVideoId)).Take(100)
            .Select(r => new { r.Id, r.LessonVideoId, r.LessonId, r.VideoTitle, r.Seconds, r.Kind, r.Title });
    }
}
