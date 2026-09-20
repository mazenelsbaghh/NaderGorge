using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.MimGames;

public sealed record MimGameTaskDto(string Label, string Icon, int CorrectChoiceIndex, string Explanation);
public sealed record MimGameSourceRefDto(Guid VideoId, Guid ChapterId, int StartTime, int EndTime);
public sealed record MimGameMissionDto(string Title, string Instruction, string Hint, string Reward, string Icon,
    IReadOnlyList<MimGameSourceRefDto> SourceRefs, IReadOnlyList<string> Choices, IReadOnlyList<MimGameTaskDto> Tasks);
public sealed record MimGameContentDto(int SchemaVersion, string Title, string Intro, string SourceLabel,
    IReadOnlyList<MimGameMissionDto> Missions);
public sealed record LessonMimGameStateDto(Guid Id, string Status, bool IsEnabled, string? DraftContentJson,
    string? DraftFingerprint, string? PublishedFingerprint, Guid? GenerationSourceVideoId, Guid? DraftSourceVideoId,
    Guid? PublishedSourceVideoId, string? LastError, DateTime? GeneratedAtUtc, DateTime? PublishedAtUtc);
public sealed record StudentMimGameDto(string ContentJson, string Fingerprint, int SchemaVersion = 1);

public static class MimGameContract
{
    private static readonly HashSet<string> Icons = new(StringComparer.Ordinal)
        { "book", "lightbulb", "target", "shield", "clock", "globe", "scale", "building", "flag" };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public static bool TryValidate(string json, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        MimGameContentDto? content;
        try { content = JsonSerializer.Deserialize<MimGameContentDto>(json, JsonOptions); }
        catch (JsonException) { error = "MIM_INVALID_JSON"; return false; }
        if (content is null || content.SchemaVersion != 1 || content.Missions?.Count != 3 ||
            !Text(content.Title, 120) || !Text(content.Intro, 500) || !Text(content.SourceLabel, 160))
        { error = "MIM_INVALID_CONTRACT"; return false; }
        foreach (var mission in content.Missions)
        {
            if (!Text(mission.Title, 100) || !Text(mission.Instruction, 500) || !Text(mission.Hint, 300) ||
                !Text(mission.Reward, 100) || !Icons.Contains(mission.Icon) || mission.SourceRefs is null || mission.SourceRefs.Count == 0 ||
                mission.Choices is null || mission.Choices.Count is < 2 or > 4 || mission.Choices.Any(choice => !Text(choice, 160)) ||
                mission.Tasks is null || mission.Tasks.Count is < 3 or > 5) { error = "MIM_INVALID_MISSION"; return false; }
            if (mission.SourceRefs.Any(source => source.VideoId == Guid.Empty || source.ChapterId == Guid.Empty || source.StartTime < 0 || source.EndTime < source.StartTime))
            { error = "MIM_INVALID_SOURCE_REF"; return false; }
            if (mission.Tasks.Any(task => !Text(task.Label, 240) || !Icons.Contains(task.Icon) || !Text(task.Explanation, 400) ||
                task.CorrectChoiceIndex < 0 || task.CorrectChoiceIndex >= mission.Choices.Count))
            { error = "MIM_INVALID_TASK"; return false; }
        }
        normalized = JsonSerializer.Serialize(content, JsonOptions);
        return true;
    }

    public static bool HasGroundedSourceRefs(string normalizedJson, MimSourcePack sourcePack)
    {
        var content = JsonSerializer.Deserialize<MimGameContentDto>(normalizedJson, JsonOptions)!;
        var chapters = sourcePack.Videos
            .SelectMany(video => video.Chapters.Select(chapter => (video.Id, Chapter: chapter)))
            .ToDictionary(entry => (entry.Id, entry.Chapter.Id), entry => entry.Chapter);
        return content.Missions.SelectMany(mission => mission.SourceRefs).All(source =>
            chapters.TryGetValue((source.VideoId, source.ChapterId), out var chapter) &&
            chapter.StartTime == source.StartTime && chapter.EndTime == source.EndTime);
    }

    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max &&
        !value.Contains('<') && !value.Contains('>') && !value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) &&
        !Uri.TryCreate(value, UriKind.Absolute, out _);
}

public sealed record MimSourceChapter(Guid Id, string Title, string Summary, int StartTime, int EndTime);
public sealed record MimSourceVideo(Guid Id, int SourceRevision, string Title, IReadOnlyList<MimSourceChapter> Chapters);
public sealed record MimSourcePack(Guid LessonId, string LessonTitle, string OutputLanguage, IReadOnlyList<MimSourceVideo> Videos);
public sealed record MimSourceResult(bool Success, string? Error, string? Fingerprint, MimSourcePack? Pack, IReadOnlyList<string> MissingVideos);

public static class MimGameSource
{
    public static Task<MimSourceResult> BuildAsync(IAppDbContext db, Guid lessonId, CancellationToken ct) =>
        BuildAsync(db, lessonId, null, ct);

    public static async Task<MimSourceResult> BuildAsync(
        IAppDbContext db,
        Guid lessonId,
        Guid? sourceVideoId,
        CancellationToken ct)
    {
        var lesson = await db.Lessons.AsNoTracking().Where(x => x.Id == lessonId).Select(x => new
        {
            x.Id, x.Title, Language = x.ContentSection.Term.Package.AiOutputLanguage,
            Videos = x.Videos.Where(v => v.IsActive).OrderBy(v => v.Order).Select(v => new
            {
                v.Id, v.Title, v.SourceRevision, v.SubtitleUrl,
                Chapters = v.VideoChapters.OrderBy(c => c.Order).Select(c => new MimSourceChapter(c.Id, c.Title, c.SummaryText, c.StartTime, c.EndTime)).ToList()
            }).ToList()
        }).SingleOrDefaultAsync(ct);
        if (lesson is null) return new(false, "MIM_LESSON_NOT_FOUND", null, null, []);
        var selectedVideos = sourceVideoId.HasValue
            ? lesson.Videos.Where(video => video.Id == sourceVideoId.Value).ToList()
            : lesson.Videos;
        if (sourceVideoId.HasValue && selectedVideos.Count == 0)
            return new(false, "MIM_SOURCE_VIDEO_NOT_FOUND", null, null, []);
        var missing = selectedVideos.Where(v => string.IsNullOrWhiteSpace(v.SubtitleUrl) || v.Chapters.Count == 0 || v.Chapters.Any(c => string.IsNullOrWhiteSpace(c.Summary)))
            .Select(v => v.Title).ToArray();
        if (selectedVideos.Count == 0 || missing.Length > 0) return new(false, "MIM_ANALYSIS_REQUIRED", null, null, missing);
        var pack = new MimSourcePack(lesson.Id, lesson.Title, AiOutputLanguageContract.ToWorkerCode(lesson.Language),
            selectedVideos.Select(v => new MimSourceVideo(v.Id, v.SourceRevision, v.Title, v.Chapters)).ToArray());
        var canonical = JsonSerializer.Serialize(pack, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new(true, null, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant(), pack, []);
    }
}

public record GetLessonMimGameQuery(Guid LessonId) : IRequest<ApiResponse<LessonMimGameStateDto?>>;
public sealed class GetLessonMimGameQueryHandler(IAppDbContext db) : IRequestHandler<GetLessonMimGameQuery, ApiResponse<LessonMimGameStateDto?>>
{
    public async Task<ApiResponse<LessonMimGameStateDto?>> Handle(GetLessonMimGameQuery request, CancellationToken ct)
    {
        var game = await db.LessonMimGames.AsNoTracking().SingleOrDefaultAsync(x => x.LessonId == request.LessonId, ct);
        return ApiResponse<LessonMimGameStateDto?>.Ok(game is null ? null : new(game.Id, game.Status.ToString(), game.IsEnabled,
            game.DraftContentJson, game.DraftFingerprint, game.PublishedFingerprint, game.GenerationSourceVideoId,
            game.DraftSourceVideoId, game.PublishedSourceVideoId, game.LastError, game.GeneratedAtUtc, game.PublishedAtUtc));
    }
}

public record GenerateLessonMimGameCommand(Guid LessonId, Guid SourceVideoId) : IRequest<ApiResponse<Guid>>;
public sealed class GenerateLessonMimGameCommandHandler(IAppDbContext db, IJobEnqueuer jobs) : IRequestHandler<GenerateLessonMimGameCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(GenerateLessonMimGameCommand request, CancellationToken ct)
    {
        var source = await MimGameSource.BuildAsync(db, request.LessonId, request.SourceVideoId, ct);
        if (!source.Success) return ApiResponse<Guid>.Fail(source.Error switch
        {
            "MIM_ANALYSIS_REQUIRED" => $"حلّل الجزء المختار أولاً: {string.Join("، ", source.MissingVideos)}",
            "MIM_SOURCE_VIDEO_NOT_FOUND" => "الجزء المختار غير موجود أو غير مفعّل.",
            _ => "الحصة غير موجودة"
        }, [source.Error!]);
        var now = DateTime.UtcNow;
        var game = await db.LessonMimGames.AsNoTracking().SingleOrDefaultAsync(x => x.LessonId == request.LessonId, ct);
        var runId = Guid.NewGuid();
        if (game is null)
        {
            game = new LessonMimGame
            {
                Id = Guid.NewGuid(), LessonId = request.LessonId, CreatedAt = now, UpdatedAt = now,
                Status = LessonMimGameStatus.Generating, CurrentGenerationRunId = runId,
                GenerationSourceVideoId = request.SourceVideoId,
                GenerationStartedAtUtc = now, GenerationExpiresAtUtc = now.AddMinutes(20)
            };
            db.LessonMimGames.Add(game);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return ApiResponse<Guid>.Fail("توليد اللعبة قيد التنفيذ بالفعل.", ["MIM_ALREADY_GENERATING"]); }
        }
        else
        {
            var claimed = await db.LessonMimGames
                .Where(x => x.Id == game.Id && (x.Status != LessonMimGameStatus.Generating || x.GenerationExpiresAtUtc <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, LessonMimGameStatus.Generating)
                    .SetProperty(x => x.CurrentGenerationRunId, runId)
                    .SetProperty(x => x.GenerationSourceVideoId, request.SourceVideoId)
                    .SetProperty(x => x.GenerationStartedAtUtc, now)
                    .SetProperty(x => x.GenerationExpiresAtUtc, now.AddMinutes(20))
                    .SetProperty(x => x.LastError, (string?)null)
                    .SetProperty(x => x.Version, x => x.Version + 1)
                    .SetProperty(x => x.UpdatedAt, now), ct);
            if (claimed == 0) return ApiResponse<Guid>.Fail("توليد اللعبة قيد التنفيذ بالفعل.", ["MIM_ALREADY_GENERATING"]);
        }
        try
        {
            await jobs.EnqueueJobAsync("ai-lesson-game-queue", "generate-mim", new
            {
                gameId = game.Id, lessonId = request.LessonId, runId, sourceFingerprint = source.Fingerprint,
                sourcePack = source.Pack, schemaVersion = 1
            });
        }
        catch
        {
            await db.LessonMimGames.Where(x => x.Id == game.Id && x.CurrentGenerationRunId == runId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, LessonMimGameStatus.Failed)
                    .SetProperty(x => x.CurrentGenerationRunId, (Guid?)null)
                    .SetProperty(x => x.GenerationSourceVideoId, (Guid?)null)
                    .SetProperty(x => x.LastError, "MIM_QUEUE_FAILED")
                    .SetProperty(x => x.Version, x => x.Version + 1), CancellationToken.None);
            throw;
        }
        return ApiResponse<Guid>.Ok(runId, "تم بدء توليد اللعبة.");
    }
}

public record PublishLessonMimGameCommand(Guid LessonId) : IRequest<ApiResponse>;
public sealed class PublishLessonMimGameCommandHandler(IAppDbContext db) : IRequestHandler<PublishLessonMimGameCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(PublishLessonMimGameCommand request, CancellationToken ct)
    {
        var game = await db.LessonMimGames.SingleOrDefaultAsync(x => x.LessonId == request.LessonId, ct);
        if (game is null)
            return ApiResponse.Fail("المسودة غير جاهزة أو لم تعد مطابقة لمحتوى الحصة.", ["MIM_DRAFT_NOT_CURRENT"]);
        var source = await MimGameSource.BuildAsync(db, request.LessonId, game.DraftSourceVideoId, ct);
        if (!source.Success || game.Status != LessonMimGameStatus.Ready || game.DraftFingerprint != source.Fingerprint ||
            game.DraftContentJson is null || !MimGameContract.TryValidate(game.DraftContentJson, out var normalized, out _))
            return ApiResponse.Fail("المسودة غير جاهزة أو لم تعد مطابقة لمحتوى الحصة.", ["MIM_DRAFT_NOT_CURRENT"]);
        game.PublishedContentJson = normalized;
        game.PublishedFingerprint = source.Fingerprint;
        game.PublishedSourceVideoId = game.DraftSourceVideoId;
        game.PublishedAtUtc = game.UpdatedAt = DateTime.UtcNow;
        game.IsEnabled = true;
        game.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ApiResponse.Fail("المسودة لم تعد مطابقة لمحتوى الحصة.", ["MIM_DRAFT_NOT_CURRENT"]); }
        return ApiResponse.Ok("تم نشر اللعبة وتفعيلها.");
    }
}

public record DisableLessonMimGameCommand(Guid LessonId) : IRequest<ApiResponse>;
public sealed class DisableLessonMimGameCommandHandler(IAppDbContext db) : IRequestHandler<DisableLessonMimGameCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DisableLessonMimGameCommand request, CancellationToken ct)
    {
        var game = await db.LessonMimGames.SingleOrDefaultAsync(x => x.LessonId == request.LessonId, ct);
        if (game is null) return ApiResponse.Fail("اللعبة غير موجودة.");
        game.IsEnabled = false;
        game.UpdatedAt = DateTime.UtcNow;
        game.Version++;
        await db.SaveChangesAsync(ct);
        return ApiResponse.Ok("تم تعطيل اللعبة.");
    }
}

public record CompleteLessonMimGameCommand(Guid GameId, Guid RunId, string SourceFingerprint, string ContentJson) : IRequest<ApiResponse>;
public sealed class CompleteLessonMimGameCommandHandler(IAppDbContext db) : IRequestHandler<CompleteLessonMimGameCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(CompleteLessonMimGameCommand request, CancellationToken ct)
    {
        var game = await db.LessonMimGames.SingleOrDefaultAsync(x => x.Id == request.GameId, ct);
        if (game is null) return ApiResponse.Fail("MIM game not found");
        if (game.CurrentGenerationRunId != request.RunId || game.Status != LessonMimGameStatus.Generating)
            return ApiResponse.Ok("تم تجاهل نتيجة قديمة.");
        var source = await MimGameSource.BuildAsync(db, game.LessonId, game.GenerationSourceVideoId, ct);
        if (!source.Success || source.Fingerprint != request.SourceFingerprint)
        {
            game.Status = LessonMimGameStatus.Stale; game.CurrentGenerationRunId = null; game.GenerationSourceVideoId = null;
            game.LastError = "MIM_SOURCE_CHANGED"; game.Version++;
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { }
            return ApiResponse.Ok("تم تجاهل نتيجة لمصدر قديم.");
        }
        if (!MimGameContract.TryValidate(request.ContentJson, out var normalized, out var error) ||
            !MimGameContract.HasGroundedSourceRefs(normalized, source.Pack!))
        {
            error = string.IsNullOrEmpty(error) ? "MIM_UNGROUNDED_SOURCE_REF" : error;
            game.Status = LessonMimGameStatus.Failed; game.CurrentGenerationRunId = null; game.GenerationSourceVideoId = null;
            game.LastError = error; game.Version++;
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { }
            return ApiResponse.Fail("محتوى اللعبة غير صالح.", [error]);
        }
        game.DraftContentJson = normalized; game.DraftFingerprint = request.SourceFingerprint;
        game.DraftSourceVideoId = game.GenerationSourceVideoId; game.Status = LessonMimGameStatus.Ready;
        game.CurrentGenerationRunId = null; game.GenerationSourceVideoId = null; game.GenerationExpiresAtUtc = null;
        game.LastError = null; game.GeneratedAtUtc = game.UpdatedAt = DateTime.UtcNow;
        game.Version++;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return ApiResponse.Ok("تم تجاهل نتيجة قديمة."); }
        return ApiResponse.Ok("تم حفظ مسودة اللعبة.");
    }
}

public record FailLessonMimGameCommand(Guid GameId, Guid RunId, string? ErrorCode) : IRequest<ApiResponse>;
public sealed class FailLessonMimGameCommandHandler(IAppDbContext db) : IRequestHandler<FailLessonMimGameCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(FailLessonMimGameCommand request, CancellationToken ct)
    {
        await db.LessonMimGames.Where(x => x.Id == request.GameId && x.CurrentGenerationRunId == request.RunId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, LessonMimGameStatus.Failed)
                .SetProperty(x => x.CurrentGenerationRunId, (Guid?)null)
                .SetProperty(x => x.GenerationSourceVideoId, (Guid?)null)
                .SetProperty(x => x.GenerationExpiresAtUtc, (DateTime?)null)
                .SetProperty(x => x.Version, x => x.Version + 1)
                .SetProperty(x => x.LastError, SafeError(request.ErrorCode)).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        return ApiResponse.Ok("تم تسجيل فشل التوليد.");
    }
    private static string SafeError(string? value) => value is "MIM_MODEL_TIMEOUT" or "MIM_MODEL_INVALID" ? value : "MIM_GENERATION_FAILED";
}
