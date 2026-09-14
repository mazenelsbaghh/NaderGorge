using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.Content;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAITeacherSearchCandidate(
    Guid TeacherId,
    string DisplayName,
    string Specialization,
    IReadOnlyList<string> SubjectNames,
    bool AccountActive);

public sealed record AdminAITeacherSearchOutput(
    string Resolution,
    Guid? ResolvedTeacherId,
    IReadOnlyList<AdminAITeacherSearchCandidate> Candidates,
    bool HasMore);

public sealed class AdminAITeacherSearchRead(IAppDbContext db) : IAdminAIReadCapability
{
    private const int CandidateLimit = 3;

    public string Key => "teachers.search";
    public Type OutputType => typeof(AdminAITeacherSearchOutput);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var suppliedQuery = AdminAIReadArguments.RequireQuery(input);
        var searchText = AdminAIReadArguments.StripTeacherHonorific(suppliedQuery);
        var normalizedQuery = AdminAIReadArguments.NormalizeArabic(searchText);
        if (normalizedQuery.Length < 2)
            throw new InvalidOperationException("The normalized teacher query is too short.");

        var exactCandidates = await LoadTeachersAsync(searchText, normalizedQuery, true, ct);
        var candidates = exactCandidates;
        if (candidates.Count == 0 && normalizedQuery.Length >= 3)
            candidates = await LoadTeachersAsync(searchText, normalizedQuery, false, ct);
        var hasMore = candidates.Count > CandidateLimit;
        var visibleCandidates = candidates.Take(CandidateLimit).Select(MapCandidate).ToArray();
        var isUnique = visibleCandidates.Length == 1 && !hasMore;
        var searchOutput = new AdminAITeacherSearchOutput(
            ResolutionName(visibleCandidates.Length, isUnique),
            isUnique ? visibleCandidates[0].TeacherId : null,
            visibleCandidates,
            hasMore);
        var asOf = DateTime.UtcNow;
        return new(searchOutput, visibleCandidates.Length, !hasMore, hasMore, asOf, ["admin.teachers"]);
    }

    private async Task<IReadOnlyList<TeacherSearchRow>> LoadTeachersAsync(
        string searchText,
        string normalizedQuery,
        bool exactOnly,
        CancellationToken ct)
    {
        var teachers = db.TeacherProfiles.AsNoTracking()
            .Where(teacher => !teacher.User.IsDeleted);
        if (UsesPostgres())
        {
            teachers = exactOnly
                ? teachers.Where(teacher =>
                    PostgresSearchFunctions.NormalizeArabic(teacher.User.FullName) == normalizedQuery)
                : teachers.Where(teacher => EF.Functions.ILike(
                    PostgresSearchFunctions.NormalizeArabic(teacher.User.FullName),
                    $"%{EscapeLikePattern(normalizedQuery)}%",
                    "\\"));
        }
        else
        {
            teachers = exactOnly
                ? teachers.Where(teacher =>
                    teacher.User.FullName == searchText ||
                    teacher.User.FullName == normalizedQuery)
                : teachers.Where(teacher =>
                    teacher.User.FullName.Contains(searchText) ||
                    teacher.User.FullName.Contains(normalizedQuery));
        }
        var candidates = await teachers
            .OrderBy(teacher => teacher.User.FullName)
            .ThenBy(teacher => teacher.Id)
            .Take(CandidateLimit + 1)
            .Select(teacher => new TeacherSearchRow(
                teacher.Id,
                teacher.User.FullName,
                teacher.Specialization,
                teacher.User.IsActive,
                teacher.TeacherSubjects
                    .OrderBy(subject => subject.Subject.Name)
                    .Select(subject => subject.Subject.Name)
                    .Take(5)
                    .ToArray()))
            .ToListAsync(ct);
        return candidates
            .OrderBy(teacher =>
                AdminAIReadArguments.NormalizeArabic(teacher.FullName) == normalizedQuery ? 0 : 1)
            .ThenBy(teacher => teacher.FullName, StringComparer.Ordinal)
            .ThenBy(teacher => teacher.Id)
            .ToArray();
    }

    private bool UsesPostgres() =>
        db is DbContext context &&
        StringComparer.Ordinal.Equals(context.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL");

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static AdminAITeacherSearchCandidate MapCandidate(TeacherSearchRow teacher) =>
        new(
            teacher.Id,
            AdminAIReadArguments.SafeText(teacher.FullName, 120),
            AdminAIReadArguments.SafeText(teacher.Specialization, 120),
            teacher.Subjects.Select(subject => AdminAIReadArguments.SafeText(subject, 100)).ToArray(),
            teacher.IsActive);

    private static string ResolutionName(int candidateCount, bool isUnique) =>
        candidateCount == 0 ? "not_found" : isUnique ? "unique" : "ambiguous";

    private sealed record TeacherSearchRow(
        Guid Id,
        string FullName,
        string Specialization,
        bool IsActive,
        IReadOnlyList<string> Subjects);
}

public sealed record AdminAITeacherSubscriberCounts(int NonGift, int GiftOnly, int Total);

public sealed record AdminAITeacherSubscriberScope(
    AdminAITeacherSubscriberCounts Active,
    AdminAITeacherSubscriberCounts NonCancelledHistorical);

public sealed record AdminAITeacherSubscribersSummaryOutput(
    bool Found,
    Guid? TeacherId,
    string? DisplayName,
    int? PackageCount,
    AdminAITeacherSubscriberScope? Overall,
    AdminAITeacherSubscriberScope? PackageHierarchy,
    AdminAITeacherSubscriberScope? DirectVideo,
    AdminAITeacherSubscriberScope? DirectExam,
    bool ScopeCountsAreNonAdditive,
    bool UnscopedGlobalVideoTypeGrantsExcluded,
    DateTime DataAsOf);

public sealed class AdminAITeacherSubscribersSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "teacher.subscribers.summary";
    public Type OutputType => typeof(AdminAITeacherSubscribersSummaryOutput);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var teacherId = AdminAIReadArguments.RequireGuid(input, "teacherId");
        var teacher = await db.TeacherProfiles.AsNoTracking()
            .Where(profile => profile.Id == teacherId && !profile.User.IsDeleted)
            .Select(profile => new
            {
                profile.Id,
                profile.User.FullName,
                PackageCount = profile.Packages.Count
            })
            .SingleOrDefaultAsync(ct);
        var asOf = DateTime.UtcNow;
        if (teacher is null)
        {
            var unavailable = new AdminAITeacherSubscribersSummaryOutput(
                false, null, null, null, null, null, null, null, true, true, asOf);
            return new(unavailable, 0, true, false, asOf, ["admin.teachers"]);
        }

        var summary = await new TeacherSubscriberFactSource(db).SummarizeAsync(teacherId, asOf, ct);
        var output = new AdminAITeacherSubscribersSummaryOutput(
            true,
            teacher.Id,
            AdminAIReadArguments.SafeText(teacher.FullName, 120),
            teacher.PackageCount,
            Map(summary.Overall),
            Map(summary.PackageHierarchy),
            Map(summary.DirectVideo),
            Map(summary.DirectExam),
            summary.ScopeCountsAreNonAdditive,
            true,
            asOf);
        return new(output, 1, true, false, asOf, [$"admin.teacher.details:{teacher.Id:D}"]);
    }

    private static AdminAITeacherSubscriberScope Map(TeacherSubscriberScopeCounts counts) =>
        new(Map(counts.Active), Map(counts.NonCancelledHistorical));

    private static AdminAITeacherSubscriberCounts Map(TeacherSubscriberCounts counts) =>
        new(counts.NonGift, counts.GiftOnly, counts.Total);
}
