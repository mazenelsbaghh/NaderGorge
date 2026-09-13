using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LearningCenter;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.VideoLearning;

public sealed partial class VideoLearningService(IAppDbContext db, IAccessCheckService access, IVideoLearningAi ai)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static readonly LearningDocument Empty = new(new(), []);

    internal async Task RequireAdminAsync(Guid actor, CancellationToken ct)
    {
        if (!await db.UserRoles.AnyAsync(r => r.UserId == actor && r.Role.Type == RoleType.Admin, ct))
            throw new UnauthorizedAccessException();
    }

    internal async Task<LessonVideo> VideoAsync(Guid actor, Guid videoId, bool author, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == actor && u.IsActive && !u.IsDeleted, ct))
            throw new UnauthorizedAccessException();
        var video = await db.LessonVideos.AsNoTracking().SingleOrDefaultAsync(v => v.Id == videoId, ct)
            ?? throw new KeyNotFoundException();
        if (author)
        {
            if (!await db.UserRoles.AnyAsync(r => r.UserId == actor && (r.Role.Type == RoleType.Admin || r.Role.Type == RoleType.Teacher), ct))
                throw new UnauthorizedAccessException();
            if (!await new TeacherAuthorizationService(db).CanAccessLessonAsync(actor, video.LessonId, ct))
                throw new UnauthorizedAccessException();
        }
        else if (!video.IsActive || !await access.HasAccessToVideoAsync(actor, videoId, ct))
            throw new UnauthorizedAccessException();
        if (!author && await db.Exams.AnyAsync(e => e.IsActive && e.IsMandatory &&
            (e.LessonVideoId == videoId || e.Id == video.ExamId) &&
            !db.StudentExamAttempts.Any(a => a.UserId == actor && a.ExamId == e.Id && a.IsPassed), ct))
            throw new UnauthorizedAccessException();
        return video;
    }

    internal async Task<(VideoLearningConfiguration? Config, LearningDocument Document)> ReadAsync(Guid videoId, CancellationToken ct)
    {
        var config = await db.VideoLearningConfigurations.SingleOrDefaultAsync(c => c.LessonVideoId == videoId, ct);
        return (config, config is null ? Empty : JsonSerializer.Deserialize<LearningDocument>(config.DocumentJson, Json) ?? Empty);
    }

    internal static void CheckVersion(LessonVideo video, VideoLearningConfiguration? config, Guid version)
    {
        if ((config?.Version ?? Guid.Empty) != version || (config is not null && config.SourceRevision != video.SourceRevision))
            throw new LearningConflictException("اتغير الفيديو أو إعدادات التفاعل. حدّث الصفحة وحاول تاني.");
    }

    public async Task<LearningSnapshot> SnapshotAsync(Guid actor, Guid videoId, bool author, CancellationToken ct)
    {
        if (author) await RequireAdminAsync(actor, ct);
        var video = await VideoAsync(actor, videoId, author, ct);
        var (config, document) = await ReadAsync(videoId, ct);
        var version = config?.Version ?? Guid.Empty;
        var stale = config is not null && config.SourceRevision != video.SourceRevision;
        var entries = author ? [] : await db.VideoLearningEntries.AsNoTracking()
            .Where(e => e.StudentId == actor && e.LessonVideoId == videoId && e.SourceRevision == video.SourceRevision &&
                e.Kind != "ai" && ((e.Kind != "answer" && e.Kind != "mastery") || e.ConfigurationVersion == version))
            .OrderByDescending(e => e.CreatedAt).Take(1000).Select(e => new LearningEntryDto(e.Id, e.Kind, e.Seconds,
                e.Text, e.Title, e.ActivityId, e.Correct, e.CommentId)).ToListAsync(ct);
        var density = document.Tools.Timeline && !stale ? await DensityAsync(videoId, video.SourceRevision, ct) : [];
        var studentDocument = LearningRules.ForStudent(document);
        var answered = entries.Where(e => e.Kind == "answer").Select(e => e.ActivityId).ToHashSet();
        studentDocument = studentDocument with { Activities = studentDocument.Activities.Select(a =>
            a.Kind == "question" && answered.Contains(a.Id) ? a with { Answer = document.Activities.Single(original => original.Id == a.Id).Answer } : a).ToArray() };
        return new(version, video.SourceRevision, stale, author ? document : stale ? Empty : studentDocument, entries, density);
    }

    public async Task<LearningSnapshot> SaveAsync(Guid actor, Guid videoId, SaveLearningDocument request, CancellationToken ct)
    {
        await RequireAdminAsync(actor, ct);
        var video = await VideoAsync(actor, videoId, true, ct);
        if (request.SourceRevision != video.SourceRevision) throw new LearningConflictException("اتغير مصدر الفيديو. حدّث الصفحة وراجع التوقيتات.");
        LearningRules.Validate(request.Document, await DurationAsync(videoId, ct));
        var (config, _) = await ReadAsync(videoId, ct);
        if ((config?.Version ?? Guid.Empty) != request.Version) throw new LearningConflictException("الإعدادات اتعدلت من مستخدم تاني. حدّث الصفحة.");
        foreach (var activity in request.Document.Activities.Where(a => a.QuestionBankId.HasValue))
            await new LearningCenterScope(db).QuestionAsync(actor, activity.QuestionBankId!.Value, ct);
        config ??= new VideoLearningConfiguration { LessonVideoId = videoId };
        if (request.Version == Guid.Empty) db.VideoLearningConfigurations.Add(config);
        config.SourceRevision = video.SourceRevision;
        config.Version = Guid.NewGuid();
        config.DocumentJson = JsonSerializer.Serialize(request.Document, Json);
        config.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { Action = "SaveVideoLearning", EntityType = "LessonVideo", EntityId = videoId,
            PerformedByUserId = actor, NewValues = $"Version={config.Version};Activities={request.Document.Activities.Length}" });
        await db.SaveChangesAsync(ct);
        return await SnapshotAsync(actor, videoId, true, ct);
    }

    public async Task<LearningAnswerResult> RecordAsync(Guid actor, Guid videoId, LearningEntryRequest request, CancellationToken ct)
    {
        var video = await VideoAsync(actor, videoId, false, ct);
        var (config, document) = await ReadAsync(videoId, ct);
        CheckVersion(video, config, request.Version);
        if (request.Id == Guid.Empty || !LearningRules.Enabled(document.Tools, request.Kind) ||
            request.Seconds < 0 || request.Seconds > (await DurationAsync(videoId, ct) ?? 86400) ||
            request.Text is null || request.Text.Length > 1800 || request.Title is null || request.Title.Length > 300)
            throw new ArgumentException("راجع بيانات التفاعل وتفعيله.");
        var activity = document.Activities.FirstOrDefault(a => a.Id == request.ActivityId);
        var explanation = request.Kind == "answer" ? activity?.Answer ?? "" : "";
        var existing = await db.VideoLearningEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == request.Id, ct);
        if (existing is not null)
        {
            if (existing.StudentId != actor || existing.LessonVideoId != videoId || existing.Kind != request.Kind ||
                existing.Text != request.Text.Trim() || existing.ActivityId != request.ActivityId)
                throw new LearningConflictException("معرف التفاعل مستخدم. حدّث الصفحة.");
            return new(ToDto(existing), explanation);
        }
        var entry = new VideoLearningEntry { Id = request.Id, StudentId = actor, LessonVideoId = videoId,
            SourceRevision = video.SourceRevision, ConfigurationVersion = request.Version, Kind = request.Kind,
            Seconds = request.Seconds, Text = request.Text.Trim(), Title = request.Title.Trim(), ActivityId = request.ActivityId };
        switch (request.Kind)
        {
            case "answer":
                if (activity?.Kind != "question" || !int.TryParse(request.Text, out var option) || option < 0 || option >= activity.Options.Length)
                    throw new ArgumentException("اختار إجابة صالحة للسؤال.");
                var previous = await db.VideoLearningEntries.AsNoTracking().FirstOrDefaultAsync(e => e.StudentId == actor &&
                    e.LessonVideoId == videoId && e.ConfigurationVersion == request.Version && e.Kind == "answer" && e.ActivityId == activity.Id, ct);
                if (previous is not null) return new(ToDto(previous), explanation);
                entry.Correct = option == activity.CorrectOption;
                entry.Seconds = activity.Seconds;
                entry.Title = activity.Concept;
                break;
            case "mastery":
                if (activity?.Kind != "concept" || request.Text is not ("understood" or "review")) throw new ArgumentException("حدد حالة المفهوم.");
                entry.Title = activity.Title;
                break;
            case "understanding":
                if (request.Text is not ("understood" or "confused" or "example")) throw new ArgumentException("حدد مؤشر الفهم.");
                break;
            case "note" or "bookmark" or "ask":
                if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("اكتب محتوى أو عنوانًا.");
                break;
            default: throw new ArgumentException("نوع التفاعل غير صالح.");
        }
        if (await db.VideoLearningEntries.CountAsync(e => e.StudentId == actor && e.LessonVideoId == videoId && e.SourceRevision == video.SourceRevision && e.Kind != "ai", ct) >= 1000)
            throw new ArgumentException("وصلت لحد التفاعلات لهذا الفيديو. احذف ملاحظات قديمة لإضافة المزيد.");
        if (request.Kind == "ask")
        {
            var videoTitle = video.Title.Length > 120 ? video.Title[..120] : video.Title;
            var comment = new LessonComment { LessonId = video.LessonId, AuthorUserId = actor,
                Body = $"[{videoTitle} · {TimeSpan.FromSeconds(request.Seconds):hh\\:mm\\:ss}]\n{entry.Text}",
                Status = LessonCommentStatus.Pending };
            db.LessonComments.Add(comment);
            entry.CommentId = comment.Id;
            var teacherUserId = await db.Lessons.Where(l => l.Id == video.LessonId)
                .Select(l => (Guid?)l.ContentSection.Term.Package.Teacher.UserId).FirstOrDefaultAsync(ct);
            if (teacherUserId.HasValue) db.OutboxEvents.Add(new OutboxEvent { Type = "LessonCommentCreated", TargetUserId = teacherUserId.Value.ToString(),
                PayloadJson = JsonSerializer.Serialize(new { commentId = comment.Id, lessonId = video.LessonId }, Json) });
            db.OutboxEvents.Add(new OutboxEvent { Type = "LessonCommentCreated", TargetGroup = "Role_Admin",
                PayloadJson = JsonSerializer.Serialize(new { commentId = comment.Id, lessonId = video.LessonId }, Json) });
        }
        db.VideoLearningEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return new(ToDto(entry), explanation);
    }

    public async Task<object> RepliesAsync(Guid actor, Guid videoId, Guid entryId, CancellationToken ct)
    {
        await VideoAsync(actor, videoId, false, ct);
        var commentId = await db.VideoLearningEntries.Where(e => e.Id == entryId && e.StudentId == actor &&
            e.LessonVideoId == videoId && e.Kind == "ask").Select(e => e.CommentId).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException();
        return await db.LessonComments.AsNoTracking().Where(c => c.ParentCommentId == commentId && c.Status == LessonCommentStatus.Approved)
            .OrderBy(c => c.CreatedAt).Take(100).Select(c => new { c.Id, c.Body, Author = c.AuthorUser.FullName }).ToListAsync(ct);
    }

    public async Task DeleteAsync(Guid actor, Guid videoId, Guid entryId, CancellationToken ct)
    {
        await VideoAsync(actor, videoId, false, ct);
        var entry = await db.VideoLearningEntries.SingleOrDefaultAsync(e => e.Id == entryId && e.StudentId == actor && e.LessonVideoId == videoId, ct)
            ?? throw new KeyNotFoundException();
        if (entry.Kind is not ("note" or "bookmark" or "understanding" or "mastery")) throw new ArgumentException("لا يمكن حذف هذا التفاعل.");
        db.VideoLearningEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
    }

    private Task<int?> DurationAsync(Guid videoId, CancellationToken ct) => db.VideoPlaybackSessions.AsNoTracking()
        .Where(s => s.LessonVideoId == videoId && s.TrackingDurationSeconds > 0)
        .OrderByDescending(s => s.CreatedAt).Select(s => s.TrackingDurationSeconds).FirstOrDefaultAsync(ct);

    internal static LearningEntryDto ToDto(VideoLearningEntry e) => new(e.Id, e.Kind, e.Seconds, e.Text, e.Title, e.ActivityId, e.Correct, e.CommentId);

    private async Task<List<TimelineDensity>> DensityAsync(Guid videoId, int revision, CancellationToken ct)
    {
        var groups = await db.VideoLearningEntries.AsNoTracking().Where(e => e.LessonVideoId == videoId && e.SourceRevision == revision && e.Kind == "understanding")
            .GroupBy(e => new { e.StudentId, Bucket = e.Seconds / 15 }).Select(g => g.OrderByDescending(e => e.CreatedAt).First()).ToListAsync(ct);
        return groups.GroupBy(e => e.Seconds / 15).Select(g => new TimelineDensity(g.Key * 15,
            g.Count(e => e.Text == "understood"), g.Count(e => e.Text == "confused"), g.Count(e => e.Text == "example"))).OrderBy(d => d.Seconds).ToList();
    }
}

public sealed class LearningConflictException(string message) : Exception(message);
