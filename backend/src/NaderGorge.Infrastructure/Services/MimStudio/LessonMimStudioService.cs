using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.MimStudio;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services.MimStudio;

public sealed partial class LessonMimStudioService(AppDbContext db, MimSceneWriter writer)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MimStudioSnapshot?> ReadAsync(Guid lessonId, CancellationToken ct)
    {
        if (!await db.Lessons.AnyAsync(x => x.Id == lessonId, ct)) throw new KeyNotFoundException();
        var studio = await db.Set<LessonMimStudio>().AsNoTracking().SingleOrDefaultAsync(x => x.LessonId == lessonId, ct);
        if (studio is null) return null;
        var source = await db.LessonVideos.AsNoTracking().Where(x => x.Id == studio.SourceVideoId)
            .Select(x => new { x.SourceRevision, x.IsActive, x.ArchiveMode }).SingleOrDefaultAsync(ct);
        return Snapshot(studio, studio.SourceVideoId is not null && (source is null || source.SourceRevision != studio.SourceRevision || !source.IsActive ||
            source.ArchiveMode != NaderGorge.Domain.Enums.ContentArchiveMode.None));
    }

    public async Task<MimStudioSnapshot> SaveAsync(Guid actor, Guid lessonId, SaveMimStudio request, CancellationToken ct)
    {
        if (!await db.Lessons.AnyAsync(x => x.Id == lessonId, ct)) throw new KeyNotFoundException();
        var source = await WritingSourceAsync(lessonId, request.SourceVideoId, request.SourceRevision, request.Document?.SourceText, ct);
        MimStudioContract.Validate(request.Document, source.Chapters.Select(x => x.Id).ToHashSet());
        var studio = await db.Set<LessonMimStudio>().SingleOrDefaultAsync(x => x.LessonId == lessonId, ct);
        CheckVersion(studio, request.Version);
        CheckIdle(studio);
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
        JsonSerializer.Deserialize<MimStudioDocument>(studio.DocumentJson, JsonOptions)!, studio.UpdatedAt, studio.GenerationStartedAt > DateTime.UtcNow.AddMinutes(-2));
}

public sealed class MimStudioConflictException(string message) : Exception(message);
