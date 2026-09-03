using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Common;

public sealed record StudentLessonCompletionContext(
    IAppDbContext Db,
    Guid UserId,
    IReadOnlyCollection<Guid> CandidateLessonIds);

/// <summary>
/// Preserves legacy assessment completion and also completes a video lesson once
/// every active part visible to the student has a registered server-side view.
/// </summary>
public static class StudentLessonCompletionReader
{
    public static Task<HashSet<Guid>> GetCompletedLessonIdsAsync(
        StudentLessonCompletionContext context,
        CancellationToken cancellationToken) =>
        GetCompletedLessonIdsCoreAsync(
            context,
            eligibleActiveVideoIds: null,
            cancellationToken);

    public static Task<HashSet<Guid>> GetCompletedLessonIdsAsync(
        StudentLessonCompletionContext context,
        IReadOnlyCollection<Guid> eligibleActiveVideoIds,
        CancellationToken cancellationToken) =>
        GetCompletedLessonIdsCoreAsync(
            context,
            eligibleActiveVideoIds,
            cancellationToken);

    public static async Task<HashSet<Guid>> GetVisibleActiveVideoIdsAsync(
        StudentLessonCompletionContext context,
        IAcademicScopeService academicScope,
        IContentArchiveAccessService archiveAccess,
        CancellationToken cancellationToken)
    {
        if (context.CandidateLessonIds.Count == 0)
            return [];

        var lessonIds = context.CandidateLessonIds.Distinct().ToList();
        var activeVideoIds = await context.Db.LessonVideos
            .AsNoTracking()
            .Where(video => lessonIds.Contains(video.LessonId) && video.IsActive)
            .Select(video => video.Id)
            .ToListAsync(cancellationToken);
        var academicallyEligibleVideoIds = await academicScope
            .GetEligibleLessonVideoIdsForStudentAsync(
                activeVideoIds,
                context.UserId,
                cancellationToken);
        return (await archiveAccess.GetViewableLessonVideoIdsAsync(
                context.UserId,
                academicallyEligibleVideoIds,
                cancellationToken))
            .ToHashSet();
    }

    private static async Task<HashSet<Guid>> GetCompletedLessonIdsCoreAsync(
        StudentLessonCompletionContext context,
        IReadOnlyCollection<Guid>? eligibleActiveVideoIds,
        CancellationToken cancellationToken)
    {
        if (context.CandidateLessonIds.Count == 0)
            return [];

        var lessonIds = context.CandidateLessonIds.Distinct().ToList();
        var completedLessonIds = await GetLegacyCompletedLessonIdsAsync(
            context,
            lessonIds,
            cancellationToken);

        if (eligibleActiveVideoIds is { Count: 0 })
            return completedLessonIds;

        var activeVideoParts = await GetActiveVideoPartsAsync(
            context,
            lessonIds,
            eligibleActiveVideoIds,
            cancellationToken);
        if (activeVideoParts.Count == 0)
            return completedLessonIds;

        var watchedVideoPartIds = await GetWatchedVideoPartIdsAsync(
            context,
            activeVideoParts,
            cancellationToken);
        completedLessonIds.UnionWith(activeVideoParts
            .GroupBy(video => video.LessonId)
            .Where(parts => parts.All(part => watchedVideoPartIds.Contains(part.Id)))
            .Select(parts => parts.Key));
        return completedLessonIds;
    }

    private static async Task<HashSet<Guid>> GetLegacyCompletedLessonIdsAsync(
        StudentLessonCompletionContext context,
        IReadOnlyCollection<Guid> lessonIds,
        CancellationToken cancellationToken) =>
        (await context.Db.LessonProgresses
            .AsNoTracking()
            .Where(progress =>
                progress.UserId == context.UserId &&
                progress.IsCompleted &&
                lessonIds.Contains(progress.LessonId))
            .Select(progress => progress.LessonId)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    private static async Task<List<ActiveVideoPart>> GetActiveVideoPartsAsync(
        StudentLessonCompletionContext context,
        IReadOnlyCollection<Guid> lessonIds,
        IReadOnlyCollection<Guid>? eligibleActiveVideoIds,
        CancellationToken cancellationToken)
    {
        var activeVideoPartsQuery = context.Db.LessonVideos
            .AsNoTracking()
            .Where(video => lessonIds.Contains(video.LessonId) && video.IsActive);
        if (eligibleActiveVideoIds is not null)
        {
            var eligibleIds = eligibleActiveVideoIds.Distinct().ToList();
            activeVideoPartsQuery = activeVideoPartsQuery
                .Where(video => eligibleIds.Contains(video.Id));
        }

        return await activeVideoPartsQuery
            .Select(video => new ActiveVideoPart(video.Id, video.LessonId))
            .ToListAsync(cancellationToken);
    }

    private static async Task<HashSet<Guid>> GetWatchedVideoPartIdsAsync(
        StudentLessonCompletionContext context,
        IReadOnlyCollection<ActiveVideoPart> activeVideoParts,
        CancellationToken cancellationToken)
    {
        var activeVideoPartIds = activeVideoParts.Select(video => video.Id).ToList();
        return (await context.Db.VideoWatchEvents
                .AsNoTracking()
                .Where(watch =>
                    watch.UserId == context.UserId &&
                    watch.WatchCount > 0 &&
                    activeVideoPartIds.Contains(watch.LessonVideoId))
                .Select(watch => watch.LessonVideoId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    private sealed record ActiveVideoPart(Guid Id, Guid LessonId);
}
