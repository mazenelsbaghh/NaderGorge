namespace NaderGorge.Domain.Interfaces;

public interface IAccessCheckService
{
    // Check if user has access to a specific package
    Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default);

    // Check if user has access to a specific lesson (by checking if they have the lesson's package)
    Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default);

    async Task<IReadOnlySet<Guid>> GetAccessibleLessonIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> lessonIds,
        CancellationToken ct = default)
    {
        var accessible = new HashSet<Guid>();
        foreach (var lessonId in lessonIds.Distinct())
        {
            if (await HasAccessToLessonAsync(userId, lessonId, ct))
                accessible.Add(lessonId);
        }

        return accessible;
    }

    // Check lesson-level access or a direct grant to this video.
    Task<bool> HasAccessToVideoAsync(Guid userId, Guid lessonVideoId, CancellationToken ct = default);

    async Task<IReadOnlySet<Guid>> GetAccessibleVideoIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> lessonVideoIds,
        CancellationToken ct = default)
    {
        var accessible = new HashSet<Guid>();
        foreach (var lessonVideoId in lessonVideoIds.Distinct())
        {
            if (await HasAccessToVideoAsync(userId, lessonVideoId, ct))
                accessible.Add(lessonVideoId);
        }

        return accessible;
    }

    // Check if user has access to a specific exam
    Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default);
}
