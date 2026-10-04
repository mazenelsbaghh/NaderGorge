using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Features.Homework;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Integration.Tests.LiveSupport;
using Xunit.Abstractions;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;
using HomeworkQuestion = NaderGorge.Domain.Entities.Homework.HomeworkQuestion;

namespace NaderGorge.Integration.Tests.Performance;

// 2026-10-04 lesson-detail investigation: candidates from one lesson repeatedly
// loaded the same access state. Homework archive checks must remain individual.
public sealed class HomeworkReadinessQueryTests(ITestOutputHelper output)
{
    [Fact]
    public async Task RepeatedLessonCandidates_PreserveArchiveSelectionWithLessDatabaseWork()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var graph = await SeedAsync(fixture.Db, 20);
        var small = await ReadAsync(fixture, graph.Student.Id, [graph.Homeworks[^1].Id]);
        var large = await ReadAsync(fixture, graph.Student.Id, graph.Homeworks.Select(h => h.Id).ToArray());

        Assert.Equal(graph.Homeworks[^1].Id, small.HomeworkId);
        Assert.Equal(small.HomeworkId, large.HomeworkId);
        // Each additional hidden homework still needs its own archive policy.
        // Repeating the multi-query lesson-access path exceeds this budget.
        Assert.InRange(large.Commands, 1, small.Commands + 3 * (graph.Homeworks.Count - 1));
        output.WriteLine($"Production SplitQuery: 1 candidate={small.Commands}; 20 candidates={large.Commands} commands");
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("expired", false)]
    [InlineData("exhausted", false)]
    [InlineData("hidden-teacher", false)]
    [InlineData("academic-denied", false)]
    public async Task RepeatedLessonCandidates_KeepAccessAndArchiveDenials(string policy, bool expected)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var graph = await SeedAsync(fixture.Db, 4);
        switch (policy)
        {
            case "expired": graph.Grant.ExpiresAt = DateTime.UtcNow.AddHours(-1); break;
            case "exhausted": graph.Grant.UsesConsumed = graph.Grant.MaxUses!.Value; break;
            case "hidden-teacher": graph.Teacher.IsContentVisibleToStudents = false; break;
            case "academic-denied":
                graph.Scope.ScopeLevel = AcademicScopeLevel.StageWide;
                graph.Scope.EducationStage = EducationStage.Primary;
                break;
        }
        // Draft, retired-question and inactive shells must never become visible.
        var draft = new HomeworkEntity { LessonId = graph.Lessons[0].Id, Title = "00 draft" };
        var retired = new HomeworkEntity
        {
            LessonId = graph.Lessons[0].Id, Title = "00 retired",
            Questions = [new HomeworkQuestion { BodyText = "Retired", IsRetired = true }]
        };
        var inactive = new HomeworkEntity
        {
            LessonId = graph.Lessons[0].Id, Title = "00 inactive", IsActive = false,
            Questions = [new HomeworkQuestion { BodyText = "Inactive" }]
        };
        fixture.Db.Homeworks.AddRange(draft, retired, inactive);
        await fixture.Db.SaveChangesAsync();
        var ids = graph.Homeworks.Select(h => h.Id).Concat([draft.Id, retired.Id, inactive.Id]).ToArray();
        var result = await ReadAsync(fixture, graph.Student.Id, ids);
        Assert.Equal(expected ? graph.Homeworks[^1].Id : (Guid?)null, result.HomeworkId);
    }

    [Fact]
    public async Task DifferentLessonsUsersAndInvocations_DoNotShareAccessDecisions()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var graph = await SeedAsync(fixture.Db, 4);
        var otherLesson = new Lesson { Title = "Other lesson", ContentSection = graph.Lessons[0].ContentSection, Order = 1 };
        var otherHomework = new HomeworkEntity
        {
            LessonId = otherLesson.Id, Title = "99 other lesson",
            Questions = [new HomeworkQuestion { BodyText = "Other homework", PointsActive = 1 }]
        };
        fixture.Db.Lessons.Add(otherLesson);
        fixture.Db.Homeworks.Add(otherHomework);
        graph.Grant.GrantType = CodeType.Lesson;
        graph.Grant.PackageId = null;
        graph.Grant.LessonId = otherLesson.Id;
        var otherStudent = NewUser("Other student");
        fixture.Db.StudentProfiles.Add(Profile(otherStudent));
        await fixture.Db.SaveChangesAsync();

        var ids = graph.Homeworks.Select(h => h.Id).Append(otherHomework.Id).ToArray();
        var counter = new QueryCounter();
        await using var db = Open(fixture, counter);
        var archive = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, new AcademicScopeService(db), archive);
        async Task<Guid?> Read(Guid studentId) => (await db.Homeworks.AsNoTracking()
            .Where(h => ids.Contains(h.Id)).OrderBy(h => h.Title)
            .FirstAccessibleToStudentAsync(studentId, access, archive, default))?.Id;

        Assert.Equal(otherHomework.Id, await Read(graph.Student.Id));
        Assert.Null(await Read(otherStudent.Id));
        graph.Grant.LessonId = graph.Lessons[0].Id;
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(graph.Homeworks[^1].Id, await Read(graph.Student.Id));
    }

    private static async Task<(Guid? HomeworkId, int Commands)> ReadAsync(
        PostgresLiveSupportFixture fixture, Guid studentId, Guid[] homeworkIds)
    {
        var counter = new QueryCounter();
        await using var db = Open(fixture, counter);
        var archive = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, new AcademicScopeService(db), archive);
        var homework = await db.Homeworks.AsNoTracking()
            .Where(h => homeworkIds.Contains(h.Id)).OrderBy(h => h.Title)
            .FirstAccessibleToStudentAsync(studentId, access, archive, default);
        return (homework?.Id, counter.Count);
    }

    private static AppDbContext Open(PostgresLiveSupportFixture fixture, QueryCounter counter) => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString, options => options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .AddInterceptors(counter).Options);

    private static async Task<Graph> SeedAsync(AppDbContext db, int count)
    {
        var student = NewUser("Homework access student");
        var teacher = new TeacherProfile { User = NewUser("Homework access teacher"), IsContentVisibleToStudents = true };
        var package = new Package
        {
            Name = "Homework access", Teacher = teacher,
            Subject = new Subject { Name = "Homework access", NormalizedName = Guid.NewGuid().ToString("N") },
            ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly
        };
        var section = new ContentSection { Title = "Section", Term = new Term { Title = "Term", Package = package } };
        var lesson = new Lesson { Title = "Lesson", ContentSection = section };
        db.Lessons.Add(lesson);
        db.StudentProfiles.Add(Profile(student));
        var scope = new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package, OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        };
        db.StudentFacingAcademicScopes.Add(scope);
        var grant = new StudentAccessGrant
        {
            User = student, GrantType = CodeType.Package, PackageId = package.Id,
            IsActive = true, GrantedAt = DateTime.UtcNow, MaxUses = 1
        };
        db.StudentAccessGrants.Add(grant);
        var homeworks = Enumerable.Range(0, count).Select(index => new HomeworkEntity
        {
            LessonId = lesson.Id, Title = $"Homework {index:D2}",
            ArchiveMode = index == count - 1 ? ContentArchiveMode.ActiveSubscribersOnly : ContentArchiveMode.HiddenFromEveryone,
            Questions = [new HomeworkQuestion { BodyText = "Synthetic question", PointsActive = 1 }]
        }).ToList();
        db.Homeworks.AddRange(homeworks);
        await db.SaveChangesAsync();
        return new(student, teacher, [lesson], homeworks, grant, scope);
    }

    private static StudentProfile Profile(User user) => new()
    {
        User = user, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondSecondary,
        Governorate = "Cairo", Address = "Synthetic fixture", Gender = Gender.Male
    };

    private static User NewUser(string name) => new()
    {
        FullName = name, PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "test-only"
    };

    private sealed record Graph(User Student, TeacherProfile Teacher, List<Lesson> Lessons,
        List<HomeworkEntity> Homeworks, StudentAccessGrant Grant, StudentFacingAcademicScope Scope);

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
