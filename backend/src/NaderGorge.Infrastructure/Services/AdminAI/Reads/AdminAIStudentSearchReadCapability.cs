using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIStudentSearchCandidate(
    Guid StudentId,
    string DisplayName,
    string StudentCode,
    string PhoneEnding,
    string EducationStage,
    string GradeLevel,
    bool AccountActive);

public sealed record AdminAIStudentSearchOutput(
    string Resolution,
    Guid? ResolvedStudentId,
    IReadOnlyList<AdminAIStudentSearchCandidate> Candidates,
    bool HasMore);

public sealed class AdminAIStudentSearchRead(IAppDbContext db) : IAdminAIReadCapability
{
    private const int CandidateLimit = 5;

    public string Key => "students.search";
    public Type OutputType => typeof(AdminAIStudentSearchOutput);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var query = AdminAIReadArguments.RequireQuery(input);
        var queryId = Guid.TryParseExact(query, "D", out var parsedId) && parsedId != Guid.Empty
            ? parsedId
            : (Guid?)null;
        var normalizedQuery = AdminAIReadArguments.NormalizeArabic(query);
        if (normalizedQuery.Length < 2)
            throw new InvalidOperationException("The normalized student query is too short.");
        var matches = await LoadExactCandidatesAsync(query, normalizedQuery, queryId, ct);
        if (matches.Count == 0 && normalizedQuery.Length >= 3)
            matches = await LoadPartialCandidatesAsync(normalizedQuery, ct);

        var hasMore = matches.Count > CandidateLimit;
        var visibleCandidates = matches.Take(CandidateLimit).Select(MapCandidate).ToArray();
        var isUnique = visibleCandidates.Length == 1 && !hasMore;
        var searchOutput = new AdminAIStudentSearchOutput(
            ResolutionName(visibleCandidates.Length, isUnique),
            isUnique ? visibleCandidates[0].StudentId : null,
            visibleCandidates,
            hasMore);
        var asOf = DateTime.UtcNow;
        return new(searchOutput, visibleCandidates.Length, !hasMore, hasMore, asOf, ["admin.students"]);
    }

    private async Task<List<CandidateRow>> LoadExactCandidatesAsync(
        string query,
        string normalizedQuery,
        Guid? queryId,
        CancellationToken ct) =>
        await LoadCandidates(ExactCandidateIds(query, normalizedQuery, queryId))
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Take(CandidateLimit + 1)
            .Select(user => new CandidateRow(
                user.Id,
                user.FullName,
                user.PhoneNumber,
                user.StudentProfile!.StudentCode,
                user.StudentProfile.EducationStage,
                user.StudentProfile.GradeLevel,
                user.IsActive))
            .ToListAsync(ct);

    private async Task<List<CandidateRow>> LoadPartialCandidatesAsync(
        string normalizedQuery,
        CancellationToken ct) =>
        await LoadCandidates(PartialCandidateIds(normalizedQuery))
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Take(CandidateLimit + 1)
            .Select(user => new CandidateRow(
                user.Id,
                user.FullName,
                user.PhoneNumber,
                user.StudentProfile!.StudentCode,
                user.StudentProfile.EducationStage,
                user.StudentProfile.GradeLevel,
                user.IsActive))
            .ToListAsync(ct);

    private IQueryable<Guid> ExactCandidateIds(
        string query,
        string normalizedQuery,
        Guid? queryId)
    {
        var users = BuildCandidateQuery();
        var profiles = db.StudentProfiles.AsNoTracking().Where(profile =>
            !profile.User.IsDeleted &&
            profile.User.UserRoles.Any(role => role.Role.Type == RoleType.Student));
        var nameIds = UsesPostgres()
            ? users.Where(user => PostgresSearchFunctions.NormalizeArabic(user.FullName) == normalizedQuery)
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Id)
                .Take(CandidateLimit + 1)
                .Select(user => user.Id)
            : users.Where(user => user.FullName.ToLower() == normalizedQuery)
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Id)
                .Take(CandidateLimit + 1)
                .Select(user => user.Id);
        var codeIds = UsesPostgres()
            ? profiles.Where(profile =>
                    profile.StudentCode != null &&
                    PostgresSearchFunctions.NormalizeArabic(profile.StudentCode) == normalizedQuery)
                .OrderBy(profile => profile.User.FullName)
                .ThenBy(profile => profile.UserId)
                .Take(CandidateLimit + 1)
                .Select(profile => profile.UserId)
            : profiles.Where(profile =>
                    profile.StudentCode != null &&
                    profile.StudentCode.ToLower() == normalizedQuery)
                .OrderBy(profile => profile.User.FullName)
                .ThenBy(profile => profile.UserId)
                .Take(CandidateLimit + 1)
                .Select(profile => profile.UserId);
        var phoneIds = users.Where(user => user.PhoneNumber == query).Select(user => user.Id);
        var directIds = queryId.HasValue
            ? users.Where(user => user.Id == queryId.Value).Select(user => user.Id)
            : users.Where(_ => false).Select(user => user.Id);
        return nameIds.Concat(phoneIds).Concat(directIds).Concat(codeIds).Distinct();
    }

    private IQueryable<Guid> PartialCandidateIds(string normalizedQuery)
    {
        var users = BuildCandidateQuery();
        var profiles = db.StudentProfiles.AsNoTracking().Where(profile =>
            !profile.User.IsDeleted &&
            profile.User.UserRoles.Any(role => role.Role.Type == RoleType.Student));
        if (!UsesPostgres())
            return users.Where(user => user.FullName.ToLower().Contains(normalizedQuery))
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Id)
                .Take(CandidateLimit + 1)
                .Select(user => user.Id)
                .Concat(profiles.Where(profile =>
                        profile.StudentCode != null &&
                        profile.StudentCode.ToLower().Contains(normalizedQuery))
                    .OrderBy(profile => profile.User.FullName)
                    .ThenBy(profile => profile.UserId)
                    .Take(CandidateLimit + 1)
                    .Select(profile => profile.UserId))
                .Distinct();

        var pattern = $"%{EscapeLikePattern(normalizedQuery)}%";
        var nameIds = users.Where(user => EF.Functions.ILike(
                PostgresSearchFunctions.NormalizeArabic(user.FullName), pattern, "\\"))
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Take(CandidateLimit + 1)
            .Select(user => user.Id);
        var codeIds = profiles.Where(profile =>
                profile.StudentCode != null &&
                EF.Functions.ILike(
                    PostgresSearchFunctions.NormalizeArabic(profile.StudentCode),
                    pattern,
                    "\\"))
            .OrderBy(profile => profile.User.FullName)
            .ThenBy(profile => profile.UserId)
            .Take(CandidateLimit + 1)
            .Select(profile => profile.UserId);
        return nameIds.Concat(codeIds).Distinct();
    }

    private IQueryable<NaderGorge.Domain.Entities.User> LoadCandidates(IQueryable<Guid> candidateIds) =>
        BuildCandidateQuery().Where(user => candidateIds.Contains(user.Id));

    private static AdminAIStudentSearchCandidate MapCandidate(CandidateRow student) =>
        new(
            student.StudentId,
            AdminAIReadArguments.SafeText(student.DisplayName, 120),
            AdminAIReadArguments.SafeText(student.StudentCode, 100),
            AdminAIReadArguments.MaskPhone(student.PhoneNumber),
            student.EducationStage.ToString(),
            student.GradeLevel.ToString(),
            student.AccountActive);

    private static string ResolutionName(int candidateCount, bool isUnique) =>
        candidateCount == 0 ? "not_found" : isUnique ? "unique" : "ambiguous";

    private IQueryable<NaderGorge.Domain.Entities.User> BuildCandidateQuery() =>
        db.Users.AsNoTracking().Where(user =>
            !user.IsDeleted &&
            user.StudentProfile != null &&
            user.UserRoles.Any(role => role.Role.Type == RoleType.Student));

    private bool UsesPostgres() =>
        db is DbContext context &&
        StringComparer.Ordinal.Equals(context.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL");

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private sealed record CandidateRow(
        Guid StudentId,
        string DisplayName,
        string PhoneNumber,
        string? StudentCode,
        EducationStage EducationStage,
        GradeLevel GradeLevel,
        bool AccountActive);
}
