using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

internal sealed class AdminAIStudentActivitySnapshotReader(IAppDbContext db)
{
    public async Task<AdminAIStudentSnapshotSection<AdminAIStudentActivitySection>> LoadAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var watching = request.ActivityFields.Contains("watching")
            ? await LoadWatchingSectionAsync(request, ct)
            : null;
        var completedLessons = request.ActivityFields.Contains("lessonProgress")
            ? await LoadCompletedLessonCountAsync(request.StudentId, ct)
            : (int?)null;
        var devices = request.ActivityFields.Contains("devices")
            ? await LoadDeviceActivityAsync(request.StudentId, ct)
            : null;
        var commitment = request.ActivityFields.Contains("commitment")
            ? await LoadCommitmentAsync(request.StudentId, ct)
            : null;
        var warnings = request.ActivityFields.Contains("warnings")
            ? await LoadWarningsAsync(request.StudentId, ct)
            : null;
        var notes = request.ActivityFields.Contains("adminNotes")
            ? await LoadNoteCountsAsync(request.StudentId, ct)
            : null;

        var activity = new AdminAIStudentActivitySection(
            watching?.Payload,
            completedLessons,
            devices,
            commitment,
            warnings,
            notes);
        return new(activity, watching?.IsTruncated == true);
    }

    private async Task<AdminAIStudentSnapshotSection<AdminAIStudentWatchingActivity>> LoadWatchingSectionAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        var watchActivity = await LoadWatchActivityAsync(request.StudentId, ct);
        var recentWatches = await LoadRecentWatchesAsync(request, ct);
        var watching = new AdminAIStudentWatchingActivity(
            watchActivity.VideoCount,
            watchActivity.WatchedSeconds,
            watchActivity.ActualWatchedSeconds,
            watchActivity.LockedVideoCount,
            watchActivity.LastWatchedAt,
            recentWatches.Take(request.RecentLimit).ToArray());
        return new(watching, recentWatches.Count > request.RecentLimit);
    }

    private async Task<int> LoadCompletedLessonCountAsync(Guid studentId, CancellationToken ct) =>
        await db.LessonProgresses.AsNoTracking()
            .CountAsync(progress => progress.UserId == studentId && progress.IsCompleted, ct);

    private async Task<WatchActivity> LoadWatchActivityAsync(Guid studentId, CancellationToken ct)
    {
        var activity = await db.VideoWatchEvents.AsNoTracking()
            .Where(watch => watch.UserId == studentId)
            .GroupBy(_ => 1)
            .Select(group => new WatchActivity(
                group.Count(),
                group.Sum(watch => watch.TimeWatchedInSeconds > 0 ? watch.TimeWatchedInSeconds : 0),
                group.Sum(watch => watch.ActualWatchedSeconds > 0 ? watch.ActualWatchedSeconds : 0),
                group.Count(watch =>
                    watch.IsLocked &&
                    watch.WatchCount >= (watch.CustomMaxWatchCount ?? watch.LessonVideo.MaxWatchCount)),
                group.Max(watch => (DateTime?)(watch.UpdatedAt ?? watch.CreatedAt))))
            .SingleOrDefaultAsync(ct);
        return activity ?? new(0, 0, 0m, 0, null);
    }

    private async Task<IReadOnlyList<AdminAIStudentWatchItem>> LoadRecentWatchesAsync(
        AdminAIStudentSnapshotRequest request,
        CancellationToken ct)
    {
        if (request.RecentLimit == 0)
            return [];

        var watches = await db.VideoWatchEvents.AsNoTracking()
            .Where(watch => watch.UserId == request.StudentId)
            .OrderByDescending(watch => watch.UpdatedAt ?? watch.CreatedAt)
            .ThenByDescending(watch => watch.Id)
            .Take(request.RecentLimit + 1)
            .Select(watch => new AdminAIStudentWatchItem(
                watch.LessonVideoId,
                watch.LessonVideo.Title,
                watch.LessonVideo.Lesson.Title,
                watch.LessonVideo.Lesson.ContentSection.Term.Package.Name,
                watch.LessonVideo.Lesson.ContentSection.Term.Package.TeacherId,
                watch.LessonVideo.Lesson.ContentSection.Term.Package.Teacher.User.FullName,
                watch.WatchCount,
                watch.IsLocked &&
                watch.WatchCount >= (watch.CustomMaxWatchCount ?? watch.LessonVideo.MaxWatchCount),
                watch.UpdatedAt ?? watch.CreatedAt))
            .ToArrayAsync(ct);
        return watches.Select(SanitizeWatch).ToArray();
    }

    private static AdminAIStudentWatchItem SanitizeWatch(AdminAIStudentWatchItem watch) => watch with
    {
        VideoTitle = AdminAIReadArguments.SafeText(watch.VideoTitle, 160),
        LessonTitle = AdminAIReadArguments.SafeText(watch.LessonTitle, 160),
        PackageName = AdminAIReadArguments.SafeText(watch.PackageName, 160),
        TeacherName = AdminAIReadArguments.SafeText(watch.TeacherName, 120)
    };

    private async Task<AdminAIStudentDeviceActivity> LoadDeviceActivityAsync(
        Guid studentId,
        CancellationToken ct)
    {
        var activity = await db.Devices.AsNoTracking()
            .Where(device => device.UserId == studentId)
            .GroupBy(_ => 1)
            .Select(group => new AdminAIStudentDeviceActivity(
                group.Count(),
                group.Count(device => device.IsActive),
                group.Max(device => (DateTime?)device.LastUsedAt)))
            .SingleOrDefaultAsync(ct);
        return activity ?? new(0, 0, null);
    }

    private async Task<AdminAIStudentCommitmentActivity> LoadCommitmentAsync(
        Guid studentId,
        CancellationToken ct)
    {
        var tracker = await db.StudentStatusTrackers.AsNoTracking()
            .Where(status => status.StudentId == studentId)
            .Select(status => new
            {
                status.CurrentStatus,
                status.ConsecutiveMissedHomeworks,
                status.ConsecutiveFailedExams
            })
            .SingleOrDefaultAsync(ct);
        return tracker is null
            ? new(string.Empty, 0, 0)
            : new(
                tracker.CurrentStatus.ToString(),
                tracker.ConsecutiveMissedHomeworks,
                tracker.ConsecutiveFailedExams);
    }

    private async Task<AdminAIStudentWarningActivity> LoadWarningsAsync(
        Guid studentId,
        CancellationToken ct)
    {
        var counts = await db.WarningEvents.AsNoTracking()
            .Where(warning => warning.StudentId == studentId && !warning.IsResolved)
            .GroupBy(_ => 1)
            .Select(group => new AdminAIStudentWarningActivity(
                group.Count(),
                group.Count(warning =>
                    warning.Severity == NaderGorge.Domain.Entities.Student.WarningSeverity.Critical)))
            .SingleOrDefaultAsync(ct);
        return counts ?? new(0, 0);
    }

    private async Task<AdminAIStudentNoteActivity> LoadNoteCountsAsync(
        Guid studentId,
        CancellationToken ct)
    {
        var counts = await db.StudentNotes.AsNoTracking()
            .Where(note => note.StudentId == studentId)
            .GroupBy(_ => 1)
            .Select(group => new AdminAIStudentNoteActivity(
                group.Count(),
                group.Count(note => note.IsPinned)))
            .SingleOrDefaultAsync(ct);
        return counts ?? new(0, 0);
    }

    private sealed record WatchActivity(
        int VideoCount,
        int WatchedSeconds,
        decimal ActualWatchedSeconds,
        int LockedVideoCount,
        DateTime? LastWatchedAt);

}
