using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.MimStudio;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public sealed partial class LessonMimStudioService
{
    public async Task<MimStudioSnapshot> GenerateAsync(Guid actor, Guid lessonId, GenerateMimScene request, CancellationToken ct)
    {
        var lesson = await db.Lessons.AsNoTracking().Where(x => x.Id == lessonId)
            .Select(x => new { x.Title, x.ContentSectionId, x.Order }).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();
        var row = await db.Set<LessonMimStudio>().SingleOrDefaultAsync(x => x.LessonId == lessonId, ct);
        CheckVersion(row, request.Version);
        CheckIdle(row);
        var previous = row is null ? null : JsonSerializer.Deserialize<MimStudioDocument>(row.DocumentJson, JsonOptions);
        var count = previous?.Scenes.Length ?? 0;
        if (request.TargetSceneCount is < 1 or > MimStudioContract.MaximumSceneCount || request.EpisodeContext?.Length > 2000)
            throw new ArgumentException("اختار من مشهد إلى ٢٠ مشهدًا، وسياق الحلقة بحد أقصى ٢٠٠٠ حرف.");
        if (request.ExpectedSceneCount != count || count >= request.TargetSceneCount)
            throw new MimStudioConflictException("حدّث الاسكربت قبل كتابة المشهد التالي. اكتمل العدد المطلوب أو اتغيّرت النسخة.");
        if (count > 0 && (previous!.TargetSceneCount != request.TargetSceneCount || previous.EpisodeContext != request.EpisodeContext))
            throw new MimStudioConflictException("كمّل بنفس عدد المشاهد وسياق الحلقة المحفوظين، علشان ترتيب القصة يفضل متماسك.");
        if (count > 0 && row is not null && (row.SourceVideoId != request.SourceVideoId || row.SourceRevision != request.SourceRevision || previous?.SourceText != request.SourceText))
            throw new MimStudioConflictException("استخدم نفس مصدر المشاهد المحفوظة علشان تكمل القصة.");
        var source = await WritingSourceAsync(lessonId, request.SourceVideoId, request.SourceRevision, request.SourceText, ct);
        var previousLessonId = await db.Lessons.AsNoTracking()
            .Where(x => x.ContentSectionId == lesson.ContentSectionId && x.Order < lesson.Order)
            .OrderByDescending(x => x.Order).ThenBy(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var previousJson = previousLessonId is null ? null : await db.Set<LessonMimStudio>().AsNoTracking()
            .Where(x => x.LessonId == previousLessonId).Select(x => x.DocumentJson).SingleOrDefaultAsync(ct);
        var opening = previousJson is null ? null : JsonSerializer.Deserialize<MimStudioDocument>(previousJson, JsonOptions)?.Scenes.FirstOrDefault();
        if (row is null)
        {
            row = new LessonMimStudio { LessonId = lessonId, SourceVideoId = request.SourceVideoId, SourceRevision = request.SourceRevision,
                DocumentJson = JsonSerializer.Serialize(new MimStudioDocument(1, lesson.Title, "", "", "", [], source.Text, request.TargetSceneCount, request.EpisodeContext), JsonOptions) };
            db.Add(row);
        }
        if (count == 0)
        {
            row.SourceVideoId = request.SourceVideoId;
            row.SourceRevision = request.SourceRevision;
            row.DocumentJson = JsonSerializer.Serialize(new MimStudioDocument(1, lesson.Title, "", "", "", [], source.Text, request.TargetSceneCount, request.EpisodeContext), JsonOptions);
        }
        row.GenerationStartedAt = DateTime.UtcNow;
        row.UpdatedByUserId = actor;
        row.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct); // Durable claim: concurrent requests cannot both call the model.
        var claim = row.Version;
        try
        {
            var ending = count == 0 ? null : previous!.Scenes[^1].Shots.TakeLast(2).ToArray();
            var generated = await writer.WriteAsync(new(lesson.Title, source, count == 0 ? null : previous, opening,
                request.TargetSceneCount, request.EpisodeContext, ending), ct);
            generated = generated with { TargetSceneCount = request.TargetSceneCount, EpisodeContext = request.EpisodeContext };
            MimStudioContract.Validate(generated, source.Chapters.Select(x => x.Id).ToHashSet());
            if (generated.Scenes.Length != 1) throw new MimStudioGenerationException("خدمة الكتابة رجّعت أكثر من مشهد. لم يتم تغيير الاسكربت.");
            // Recheck the video source after inference; a changed/deleted source cannot receive stale output.
            var currentSource = await WritingSourceAsync(lessonId, request.SourceVideoId, request.SourceRevision, request.SourceText, ct);
            if (JsonSerializer.Serialize(source) != JsonSerializer.Serialize(currentSource))
                throw new MimStudioConflictException("الشرح اتغيّر أثناء كتابة المشهد. راجع المصدر وأعد المحاولة.");
            var doc = count == 0 ? generated with { SourceText = source.Text } : previous! with { Scenes = [.. previous!.Scenes, generated.Scenes[0]] };
            doc.Scenes[^1] = doc.Scenes[^1] with { Prompt = MimVideoPrompt.Build(doc, count) };
            row.DocumentJson = JsonSerializer.Serialize(doc, JsonOptions);
            row.GenerationStartedAt = null;
            row.UpdatedAt = DateTime.UtcNow;
            row.Version = Guid.NewGuid();
            await db.SaveChangesAsync(ct);
            return Snapshot(row, false);
        }
        catch
        {
            // Release only our own claim. Never overwrite a newer writer after a timeout or concurrency failure.
            await db.Set<LessonMimStudio>().Where(x => x.Id == row.Id && x.Version == claim)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.GenerationStartedAt, (DateTime?)null), CancellationToken.None);
            throw;
        }
    }

    private async Task<MimWritingSource> WritingSourceAsync(Guid lessonId, Guid? videoId, int revision, string? text, CancellationToken ct)
    {
        if (videoId is null)
        {
            if (revision != 0 || string.IsNullOrWhiteSpace(text) || text.Trim().Length < 100 || text.Length > 24000)
                throw new ArgumentException("أدخل شرح الحصة من ١٠٠ إلى ٢٤ ألف حرف، أو اختر فيديو له فصول محللة.");
            return new(null, "نص الشرح المضاف للحصة", 0, [], text);
        }
        if (text is not null) throw new ArgumentException("اختار مصدر واحد للشرح.");
        var source = await db.LessonVideos.AsNoTracking()
            .Where(x => x.Id == videoId && x.LessonId == lessonId && x.IsActive && x.ArchiveMode == NaderGorge.Domain.Enums.ContentArchiveMode.None)
            .Select(x => new MimWritingSource(x.Id, x.Title, x.SourceRevision,
                x.VideoChapters.OrderBy(c => c.Order).Select(c => new MimWritingChapter(c.Id, c.Title, c.SummaryText)).ToArray(), null))
            .SingleOrDefaultAsync(ct) ?? throw new ArgumentException("اختار فيديو مفعّل من نفس الحصة.");
        if (source.Revision != revision) throw new MimStudioConflictException("مصدر الحصة اتغيّر. حدّث الصفحة.");
        if (source.Chapters.Length == 0 || source.Chapters.All(x => string.IsNullOrWhiteSpace(x.Summary)))
            throw new ArgumentException("حلّل فيديو الشرح من تبويب تحليل AI أولاً، أو أدخل نص الشرح.");
        if (source.Chapters.Sum(x => x.Summary.Length + x.Title.Length) > 24000)
            throw new ArgumentException("الشرح كبير. أدخل ملخصًا للحصة في مصدر النص، بحد أقصى ٢٤ ألف حرف.");
        return source;
    }

    private static void CheckVersion(LessonMimStudio? row, Guid? version)
    {
        if (row?.Version != version) throw new MimStudioConflictException("في تعديل أحدث محفوظ. حدّث النسخة قبل المتابعة.");
    }
    private static void CheckIdle(LessonMimStudio? row)
    {
        if (row?.GenerationStartedAt > DateTime.UtcNow.AddMinutes(-2))
            throw new MimStudioConflictException("المشهد بيتكتب حاليًا. انتظر ثم حدّث الحالة.");
    }
}
