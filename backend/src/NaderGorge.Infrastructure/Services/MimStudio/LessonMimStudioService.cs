using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.MimStudio;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public sealed class LessonMimStudioService(AppDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MimStudioSnapshot?> ReadAsync(Guid lessonId, CancellationToken ct)
    {
        if (!await db.Lessons.AnyAsync(x => x.Id == lessonId, ct)) throw new KeyNotFoundException();
        var studio = await db.Set<LessonMimStudio>().AsNoTracking().SingleOrDefaultAsync(x => x.LessonId == lessonId, ct);
        if (studio is null) return null;
        var source = await db.LessonVideos.AsNoTracking().Where(x => x.Id == studio.SourceVideoId)
            .Select(x => new { x.SourceRevision, x.IsActive, x.ArchiveMode }).SingleOrDefaultAsync(ct);
        return Snapshot(studio, source is null || source.SourceRevision != studio.SourceRevision || !source.IsActive ||
            source.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.None);
    }

    public async Task<MimStudioSnapshot> SaveAsync(Guid actor, Guid lessonId, SaveMimStudio request, CancellationToken ct)
    {
        var source = await db.LessonVideos.AsNoTracking()
            .Where(x => x.Id == request.SourceVideoId && x.LessonId == lessonId && x.IsActive &&
                x.ArchiveMode == NaderGorge.Domain.Enums.ContentArchiveMode.None)
            .Select(x => new { x.SourceRevision, Chapters = x.VideoChapters.Select(c => c.Id).ToList() }).SingleOrDefaultAsync(ct)
            ?? throw new ArgumentException("اختار فيديو مفعّل من نفس الحصة.");
        if (source.SourceRevision != request.SourceRevision) throw new MimStudioConflictException("مصدر الحصة اتغيّر. حدّث الصفحة وراجع الاسكربت.");
        MimStudioContract.Validate(request.Document, source.Chapters.ToHashSet());
        var studio = await db.Set<LessonMimStudio>().SingleOrDefaultAsync(x => x.LessonId == lessonId, ct);
        if ((studio is null && request.Version is not null) || (studio is not null && studio.Version != request.Version))
            throw new MimStudioConflictException("في تعديل أحدث محفوظ. حدّث النسخة قبل الحفظ.");
        if (studio is null)
        {
            studio = new LessonMimStudio { LessonId = lessonId };
            db.Add(studio);
        }
        studio.SourceVideoId = request.SourceVideoId;
        studio.SourceRevision = request.SourceRevision;
        studio.DocumentJson = JsonSerializer.Serialize(request.Document, JsonOptions);
        studio.UpdatedByUserId = actor;
        studio.UpdatedAt = DateTime.UtcNow;
        studio.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        return Snapshot(studio, false);
    }

    public async Task<object> SourcesAsync(Guid lessonId, CancellationToken ct) => await db.LessonVideos.AsNoTracking()
        .Where(x => x.LessonId == lessonId && x.IsActive && x.ArchiveMode == NaderGorge.Domain.Enums.ContentArchiveMode.None)
        .OrderBy(x => x.Order).Select(x => new
        {
            x.Id, x.Title, x.SourceRevision,
            Chapters = x.VideoChapters.OrderBy(c => c.Order).Select(c => new { c.Id, c.Title, Summary = c.SummaryText, c.StartTime, c.EndTime }).ToList()
        }).ToListAsync(ct);

    private static MimStudioSnapshot Snapshot(LessonMimStudio studio, bool stale) => new(studio.Version,
        studio.SourceVideoId, studio.SourceRevision, stale,
        JsonSerializer.Deserialize<MimStudioDocument>(studio.DocumentJson, JsonOptions)!, studio.UpdatedAt);
}

public sealed class MimStudioConflictException(string message) : Exception(message);
