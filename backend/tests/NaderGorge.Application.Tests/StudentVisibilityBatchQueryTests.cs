using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class StudentVisibilityBatchQueryTests
{
    [Fact]
    public async Task VisibleActiveVideoLookup_UsesConstantQueryCountAsPartCountGrows()
    {
        var onePartQueryCount = await MeasureVisiblePartQueriesAsync(1);
        var oneHundredPartQueryCount = await MeasureVisiblePartQueriesAsync(100);

        Assert.Equal(onePartQueryCount, oneHundredPartQueryCount);
        Assert.InRange(oneHundredPartQueryCount, 1, 8);
    }

    [Fact]
    public async Task VisibleLessonLookup_UsesConstantQueryCountAsLessonCountGrows()
    {
        var oneLessonQueryCount = await MeasureVisibleLessonQueriesAsync(1);
        var oneHundredLessonQueryCount = await MeasureVisibleLessonQueriesAsync(100);

        Assert.Equal(oneLessonQueryCount, oneHundredLessonQueryCount);
        Assert.InRange(oneHundredLessonQueryCount, 1, 7);
    }

    private static async Task<int> MeasureVisiblePartQueriesAsync(int partCount)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var counter = new QueryCounterInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var student = new User
        {
            FullName = "Visibility student",
            PhoneNumber = Guid.NewGuid().ToString("N")[..11],
            PasswordHash = "hashed"
        };
        var profile = new StudentProfile
        {
            User = student,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary,
            Gender = Gender.Male,
            Governorate = "Cairo",
            Address = "Address"
        };
        var subject = new Subject
        {
            Name = "Batch subject",
            NormalizedName = $"BATCH_{Guid.NewGuid():N}"
        };
        var teacherUser = new User
        {
            FullName = "Visibility teacher",
            PhoneNumber = Guid.NewGuid().ToString("N")[..11],
            PasswordHash = "hashed"
        };
        var teacher = new TeacherProfile
        {
            User = teacherUser,
            Specialization = "Testing"
        };
        var package = new Package
        {
            Name = "Batch package",
            Description = "Batch visibility",
            Subject = subject,
            Teacher = teacher,
            ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly
        };
        var term = new Term { Title = "Batch term", Package = package };
        var section = new ContentSection { Title = "Batch section", Term = term };
        var lesson = new Lesson { Title = "Batch lesson", ContentSection = section };
        var videoType = new VideoType
        {
            Name = "شرح",
            NormalizedName = $"EXPLANATION_{Guid.NewGuid():N}"
        };
        var videos = Enumerable.Range(1, partCount)
            .Select(index => new LessonVideo
            {
                Title = $"Part {index}",
                Provider = "youtube",
                ProviderVideoId = $"video-{index}",
                Lesson = lesson,
                VideoType = videoType,
                IsActive = true
            })
            .ToList();
        db.AddRange(profile, subject, teacher, package, term, section, lesson, videoType);
        db.LessonVideos.AddRange(videos);
        db.AcademicSubjectEligibilities.Add(new AcademicSubjectEligibility
        {
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary,
            Subject = subject,
            IsActive = true
        });
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.Exact,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary,
            Subject = subject
        });
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = student.Id,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true,
            GrantedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        counter.Reset();

        var visibleVideoIds = await StudentLessonCompletionReader.GetVisibleActiveVideoIdsAsync(
            new StudentLessonCompletionContext(db, student.Id, [lesson.Id]),
            new AcademicScopeService(db),
            new ContentArchiveAccessService(db),
            CancellationToken.None);

        Assert.Equal(partCount, visibleVideoIds.Count);
        return counter.QueryCount;
    }

    private static async Task<int> MeasureVisibleLessonQueriesAsync(int lessonCount)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var counter = new QueryCounterInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var student = new User
        {
            FullName = "Lesson visibility student",
            PhoneNumber = Guid.NewGuid().ToString("N")[..11],
            PasswordHash = "hashed"
        };
        var profile = new StudentProfile
        {
            User = student,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary,
            Gender = Gender.Male,
            Governorate = "Cairo",
            Address = "Address"
        };
        var subject = new Subject
        {
            Name = "Lesson batch subject",
            NormalizedName = $"LESSON_BATCH_{Guid.NewGuid():N}"
        };
        var teacherUser = new User
        {
            FullName = "Lesson visibility teacher",
            PhoneNumber = Guid.NewGuid().ToString("N")[..11],
            PasswordHash = "hashed"
        };
        var teacher = new TeacherProfile
        {
            User = teacherUser,
            Specialization = "Testing"
        };
        var package = new Package
        {
            Name = "Lesson batch package",
            Description = "Lesson batch visibility",
            Subject = subject,
            Teacher = teacher,
            ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly
        };
        var term = new Term { Title = "Lesson batch term", Package = package };
        var section = new ContentSection { Title = "Lesson batch section", Term = term };
        var lessons = Enumerable.Range(1, lessonCount)
            .Select(index => new Lesson
            {
                Title = $"Lesson {index}",
                ContentSection = section
            })
            .ToList();

        db.AddRange(profile, subject, teacher, package, term, section);
        db.Lessons.AddRange(lessons);
        db.AcademicSubjectEligibilities.Add(new AcademicSubjectEligibility
        {
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary,
            Subject = subject,
            IsActive = true
        });
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.Exact,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary,
            Subject = subject
        });
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = student.Id,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true,
            GrantedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        counter.Reset();

        var academicScope = new AcademicScopeService(db);
        var academicallyEligibleLessonIds = await academicScope.GetEligibleLessonIdsForStudentAsync(
            lessons.Select(lesson => lesson.Id).ToArray(),
            student.Id,
            CancellationToken.None);
        var visibleLessonIds = await new ContentArchiveAccessService(db).GetViewableLessonIdsAsync(
            student.Id,
            academicallyEligibleLessonIds,
            CancellationToken.None);

        Assert.Equal(lessonCount, visibleLessonIds.Count);
        return counter.QueryCount;
    }

    private sealed class QueryCounterInterceptor : DbCommandInterceptor
    {
        public int QueryCount { get; private set; }

        public void Reset() => QueryCount = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            QueryCount++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            QueryCount++;
            return ValueTask.FromResult(result);
        }
    }
}
