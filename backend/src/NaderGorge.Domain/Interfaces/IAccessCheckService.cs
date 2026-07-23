namespace NaderGorge.Domain.Interfaces;

public interface IAccessCheckService
{
    // Check if user has access to a specific package
    Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default);

    // Check if user has access to a specific lesson (by checking if they have the lesson's package)
    Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default);

    // Check lesson-level access or a direct grant to this video.
    Task<bool> HasAccessToVideoAsync(Guid userId, Guid lessonVideoId, CancellationToken ct = default);

    // Check if user has access to a specific exam
    Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default);
}
