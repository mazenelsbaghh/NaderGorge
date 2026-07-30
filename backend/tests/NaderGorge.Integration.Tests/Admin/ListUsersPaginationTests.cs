using System.Text.Json;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Integration.Tests.LiveSupport;

namespace NaderGorge.Integration.Tests.Admin;

public sealed class ListUsersPaginationTests
{
    [Fact]
    public async Task OversizedRequest_IsClampedStableFilteredAndPayloadBounded()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var roleName = $"PerformanceStudent-{Guid.NewGuid():N}";
        await SeedUsersAsync(fixture.Db, roleName);
        var handler = new ListUsersQueryHandler(fixture.Db);

        var response = await handler.Handle(
            new ListUsersQuery(
                Page: 0,
                PageSize: 1_000,
                Search: " perf-user ",
                EducationStage: nameof(EducationStage.Secondary),
                GradeLevel: nameof(GradeLevel.SecondSecondary),
                StudyTrack: nameof(StudyTrack.Science),
                Gender: nameof(Gender.Female),
                Governorate: "Cairo",
                Role: roleName),
            CancellationToken.None);

        var page = Assert.IsType<PagedResult<AdminUserListDto>>(response.Data);
        Assert.Equal(1, page.Page);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(125, page.TotalCount);
        Assert.Equal(100, page.Items.Count);
        Assert.Equal(
            page.Items.Select(user => user.Id).Order().ToArray(),
            page.Items.Select(user => user.Id).ToArray());
        Assert.All(page.Items, user =>
        {
            Assert.Contains(roleName, user.Roles);
            Assert.Equal(nameof(EducationStage.Secondary), user.EducationStage);
            Assert.Equal(nameof(GradeLevel.SecondSecondary), user.Grade);
            Assert.Equal(nameof(StudyTrack.Science), user.Track);
            Assert.Equal(nameof(Gender.Female), user.Gender);
            Assert.Contains("Cairo", user.Governorate);
        });
        Assert.True(
            JsonSerializer.SerializeToUtf8Bytes(response).Length <= 102_400,
            "The representative 100-row admin search payload exceeded 100 KiB.");
    }

    private static async Task SeedUsersAsync(
        AppDbContext db,
        string roleName)
    {
        var matchingRole = new Role
        {
            Name = roleName,
            Type = RoleType.Student,
            AllowedDomain = "student"
        };
        var otherRole = new Role
        {
            Name = $"{roleName}-other",
            Type = RoleType.Assistant,
            AllowedDomain = "assistant"
        };
        db.Roles.AddRange(matchingRole, otherRole);

        var createdAt = DateTime.UtcNow.AddDays(-1);
        var matchingSeed = MatchingSeed(matchingRole);
        var matchingUsers = Enumerable.Range(1, 125)
            .Select(index => NewUser(index, createdAt, matchingSeed));
        var distractorSeeds = new[]
        {
            matchingSeed with { Role = otherRole },
            matchingSeed with { EducationStage = EducationStage.Preparatory },
            matchingSeed with { GradeLevel = GradeLevel.FirstSecondary },
            matchingSeed with { StudyTrack = StudyTrack.Arts },
            matchingSeed with { Gender = Gender.Male },
            matchingSeed with { Governorate = "Giza" },
            matchingSeed with { NamePrefix = "excluded" }
        };
        var distractors = distractorSeeds
            .Select((seed, index) => NewUser(index + 126, createdAt, seed));
        db.Users.AddRange(matchingUsers.Concat(distractors));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static User NewUser(
        int index,
        DateTime createdAt,
        UserSeed seed)
    {
        var user = new User
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{index:D12}"),
            FullName = $"{seed.NamePrefix}-{index:D3}",
            PhoneNumber = $"010{index:D8}",
            PasswordHash = "integration",
            CreatedAt = createdAt,
            IsProfileComplete = true
        };
        user.StudentProfile = new StudentProfile
        {
            User = user,
            StudentCode = $"{seed.NamePrefix}-code-{index:D3}",
            DateOfBirth = new DateTime(2008, 1, 1),
            Gender = seed.Gender,
            Governorate = seed.Governorate,
            Address = "Representative integration address",
            EducationStage = seed.EducationStage,
            GradeLevel = seed.GradeLevel,
            StudyTrack = seed.StudyTrack
        };
        user.StudentBalance = new StudentBalance
        {
            User = user,
            CurrentBalance = index
        };
        user.UserRoles.Add(new UserRole { User = user, Role = seed.Role });
        return user;
    }

    private static UserSeed MatchingSeed(Role role) =>
        new(
            role,
            "perf-user",
            Gender.Female,
            "Cairo",
            EducationStage.Secondary,
            GradeLevel.SecondSecondary,
            StudyTrack.Science);

    private sealed record UserSeed(
        Role Role,
        string NamePrefix,
        Gender Gender,
        string Governorate,
        EducationStage EducationStage,
        GradeLevel GradeLevel,
        StudyTrack? StudyTrack);
}
