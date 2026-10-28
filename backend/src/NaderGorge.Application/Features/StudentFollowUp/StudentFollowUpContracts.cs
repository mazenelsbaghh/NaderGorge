using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.StudentFollowUp;

public sealed record StudentFollowUpFilter(
    Guid TeacherId, Guid PackageId, Guid[] LessonIds,
    EducationStage? Stage = null, GradeLevel? Grade = null,
    string State = "Incomplete", int MinimumDays = 0, string? Search = null,
    int Page = 1, int PageSize = 20, Guid? StudentId = null, string Attendance = "All");

public sealed record FollowUpStudentSearch(Guid TeacherId, Guid PackageId, string Search);
public sealed record FollowUpStudent(Guid Id, string Name, string Phone);
public sealed record FollowUpStudentChoices(IReadOnlyList<FollowUpStudent> Students, bool HasMore);
public sealed record FollowUpVideo(Guid Id, string Title, string State, int? ProgressPercentage, DateTime? LastWatchedAtUtc);

public sealed record FollowUpTeacher(Guid Id, string Name);
public sealed record FollowUpGrade(int Stage, int Grade);
public sealed record FollowUpPackage(Guid Id, string Name)
{
    public IReadOnlyList<FollowUpGrade> Grades { get; init; } = [];
}
public sealed record FollowUpLesson(Guid Id, string Title, string TermTitle, int Order);
public sealed record StudentFollowUpOptions(
    IReadOnlyList<FollowUpTeacher> Teachers, IReadOnlyList<FollowUpPackage> Packages,
    IReadOnlyList<FollowUpLesson> Lessons);

public sealed record StudentFollowUpRow(
    Guid StudentId, string StudentName, string Phone, string? ParentPhone,
    string Stage, string Grade, Guid LessonId, string LessonTitle, string PackageName,
    string State, int CompletedVideos, int TotalVideos, int? ProgressPercentage,
    DateTime? AvailableSinceUtc, int? DaysWaiting, string AvailabilitySource,
    DateTime? LastWatchedAtUtc)
{
    public IReadOnlyList<FollowUpVideo> Videos { get; init; } = [];
}

public sealed record StudentFollowUpReport(
    IReadOnlyList<StudentFollowUpRow> Rows, int TotalRows, int TotalStudents,
    int Completed, int Started, int NotStarted, int Overdue,
    int UnknownAvailability, int PlaybackDatedAvailability,
    int Page, int PageSize, DateTime GeneratedAtUtc);

public interface IStudentFollowUpReader
{
    Task<StudentFollowUpOptions> OptionsAsync(Guid? teacherId, Guid? packageId, bool includeTeachers, CancellationToken ct);
    Task<StudentFollowUpReport> ReadAsync(StudentFollowUpFilter filter, bool export, CancellationToken ct);
    Task<FollowUpStudentChoices> SearchStudentsAsync(FollowUpStudentSearch search, CancellationToken ct);
}

public interface IStudentFollowUpExporter
{
    Task<byte[]> ExportAsync(StudentFollowUpReport report, StudentFollowUpFilter filter, Guid actorId, CancellationToken ct);
}
