using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests;

/// <summary>
/// Production regression coverage for the 2026-08-29 incident where a disabled
/// mandatory exam remained visible to students and continued blocking progress.
/// </summary>
public sealed class ExamDeactivationProductionRegressionTests
{
    [Fact]
    public async Task DisabledMandatoryExam_IsHiddenFromDashboardAndProgress()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedDisabledLessonExamAsync(db);
        var academicScope = new AcademicScopeService(db);
        var archiveAccess = new ContentArchiveAccessService(db);

        var dashboard = await new GetDashboardQueryHandler(db, academicScope, archiveAccess)
            .Handle(new GetDashboardQuery(fixture.StudentId), CancellationToken.None);
        var progress = await new GetProgressQueryHandler(db, academicScope, archiveAccess)
            .Handle(new GetProgressQuery(fixture.StudentId), CancellationToken.None);

        Assert.True(dashboard.Success, dashboard.Message);
        Assert.DoesNotContain(dashboard.Data!.UpcomingExams, item => item.ExamId == fixture.ExamId);

        Assert.True(progress.Success, progress.Message);
        var packageProgress = Assert.Single(progress.Data!.Packages);
        var firstLesson = Assert.Single(packageProgress.Lessons, item => item.Id == fixture.FirstLessonId);
        var secondLesson = Assert.Single(packageProgress.Lessons, item => item.Id == fixture.SecondLessonId);
        Assert.False(firstLesson.HasExam);
        Assert.False(firstLesson.IsLocked);
        Assert.False(secondLesson.IsLocked);
    }

    [Fact]
    public async Task DisabledMandatoryExam_IsOmittedFromLessonDetailAndDoesNotLockFollowingLesson()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedDisabledLessonExamAsync(db);
        var academicScope = new AcademicScopeService(db);
        var archiveAccess = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, academicScope, archiveAccess);

        var lessons = await new GetLessonsQueryHandler(db, access, academicScope, archiveAccess)
            .Handle(new GetLessonsQuery(fixture.SectionId, fixture.StudentId), CancellationToken.None);
        var detailHandler = new GetLessonDetailQueryHandler(
            db,
            access,
            new TeacherAuthorizationService(db),
            academicScope,
            archiveAccess);
        var hiddenExamDetail = await detailHandler
            .Handle(new GetLessonDetailQuery(fixture.FirstLessonId, fixture.StudentId), CancellationToken.None);
        var followingLessonDetail = await detailHandler
            .Handle(new GetLessonDetailQuery(fixture.SecondLessonId, fixture.StudentId), CancellationToken.None);

        Assert.True(lessons.Success, lessons.Message);
        Assert.NotNull(lessons.Data);
        var firstLesson = Assert.Single(lessons.Data, item => item.Id == fixture.FirstLessonId);
        var secondLesson = Assert.Single(lessons.Data, item => item.Id == fixture.SecondLessonId);
        Assert.False(firstLesson.IsLocked);
        Assert.Null(firstLesson.BlockingExamId);
        Assert.False(secondLesson.IsLocked);
        Assert.Null(secondLesson.BlockingExamId);

        Assert.True(hiddenExamDetail.Success, hiddenExamDetail.Message);
        Assert.Null(hiddenExamDetail.Data!.ExamId);
        Assert.Null(hiddenExamDetail.Data.ExamStatus);
        Assert.False(hiddenExamDetail.Data.IsExamLocked);

        Assert.True(followingLessonDetail.Success, followingLessonDetail.Message);
        Assert.False(followingLessonDetail.Data!.IsLocked);
        Assert.Null(followingLessonDetail.Data.BlockingExamId);
    }

    [Fact]
    public async Task DisabledExam_DirectStartIsRejectedWithoutCreatingAttempt()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedDisabledLessonExamAsync(db);
        var academicScope = new AcademicScopeService(db);
        var archiveAccess = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, academicScope, archiveAccess);

        var result = await new StartExamAttemptCommandHandler(db, access)
            .Handle(new StartExamAttemptCommand(fixture.ExamId, fixture.StudentId), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Empty(await db.StudentExamAttempts.ToListAsync());
    }

    [Fact]
    public async Task DisabledVideoExam_IsOmittedFromLessonDetail()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedDisabledLessonExamAsync(db);
        var academicScope = new AcademicScopeService(db);
        var archiveAccess = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, academicScope, archiveAccess);

        var detail = await new GetLessonDetailQueryHandler(
                db,
                access,
                new TeacherAuthorizationService(db),
                academicScope,
                archiveAccess)
            .Handle(new GetLessonDetailQuery(fixture.FirstLessonId, fixture.StudentId), CancellationToken.None);

        Assert.True(detail.Success, detail.Message);
        var video = Assert.Single(detail.Data!.Videos, item => item.Id == fixture.VideoId);
        Assert.Null(video.ExamId);
        Assert.Empty(video.Exams);
        Assert.False(video.IsExamLocked);
    }

    [Fact]
    public async Task DisabledVideoExam_DoesNotBlockVideoSession()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedDisabledLessonExamAsync(db);
        var academicScope = new AcademicScopeService(db);
        var archiveAccess = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, academicScope, archiveAccess);

        var result = await new CreateVideoSessionCommandHandler(
                db,
                access,
                new VideoEncryptionService())
            .Handle(new CreateVideoSessionCommand(fixture.VideoId, fixture.StudentId), CancellationToken.None);

        Assert.True(result.Success, $"{result.Message}: {string.Join(",", result.Errors ?? [])}");
        Assert.Equal(fixture.VideoId, Assert.Single(await db.VideoPlaybackSessions.ToListAsync()).LessonVideoId);
    }

    private static async Task<RegressionFixture> SeedDisabledLessonExamAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var student = new User
        {
            FullName = "Production regression student",
            PhoneNumber = "20000000001",
            PasswordHash = "hashed"
        };
        var studentProfile = new StudentProfile
        {
            User = student,
            UserId = student.Id,
            DateOfBirth = new DateTime(2008, 1, 1),
            Governorate = "Cairo",
            Address = "Test address",
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        };
        student.StudentProfile = studentProfile;

        var studentRole = new Role
        {
            Name = $"Student-{Guid.NewGuid():N}",
            Type = RoleType.Student
        };
        student.UserRoles.Add(new UserRole
        {
            User = student,
            UserId = student.Id,
            Role = studentRole,
            RoleId = studentRole.Id
        });

        var teacherUser = new User
        {
            FullName = "Production regression teacher",
            PhoneNumber = "20000000002",
            PasswordHash = "hashed"
        };
        var teacher = new TeacherProfile
        {
            User = teacherUser,
            UserId = teacherUser.Id,
            Bio = "Regression fixture",
            Specialization = "Secondary",
            ContactInfo = "test@example.invalid",
            IsContentVisibleToStudents = true
        };
        teacherUser.TeacherProfile = teacher;

        var subject = new Subject
        {
            Name = "Production regression subject",
            NormalizedName = $"REGRESSION_{Guid.NewGuid():N}"
        };
        var package = new Package
        {
            Name = "Production regression package",
            Description = "Disabled exam behavior",
            Subject = subject,
            SubjectId = subject.Id,
            Teacher = teacher,
            TeacherId = teacher.Id,
            TargetGrade = "FirstSecondary"
        };
        var term = new Term
        {
            Package = package,
            PackageId = package.Id,
            Title = "Regression term",
            Order = 1
        };
        package.Terms.Add(term);
        var section = new ContentSection
        {
            Term = term,
            TermId = term.Id,
            Title = "Regression section",
            Order = 1
        };
        term.Sections.Add(section);

        var exam = new Exam
        {
            Title = "Disabled mandatory exam",
            Description = "Must not remain student-facing",
            PassingScore = 5,
            TotalScore = 10,
            IsMandatory = true,
            IsActive = false,
            CreatedByTeacher = teacher,
            CreatedByTeacherId = teacher.Id
        };
        teacher.Exams.Add(exam);

        var firstLesson = new Lesson
        {
            ContentSection = section,
            ContentSectionId = section.Id,
            Title = "Lesson with disabled exam",
            Summary = "Regression fixture",
            Order = 1,
            ExamId = exam.Id
        };
        var secondLesson = new Lesson
        {
            ContentSection = section,
            ContentSectionId = section.Id,
            Title = "Following lesson",
            Summary = "Must remain unlocked",
            Order = 2
        };
        section.Lessons.Add(firstLesson);
        section.Lessons.Add(secondLesson);

        var videoType = new VideoType
        {
            Name = "Production regression video type",
            NormalizedName = $"REGRESSION_VIDEO_{Guid.NewGuid():N}",
            SortOrder = 1
        };
        var video = new LessonVideo
        {
            Lesson = firstLesson,
            LessonId = firstLesson.Id,
            VideoType = videoType,
            VideoTypeId = videoType.Id,
            Title = "Video with disabled exam",
            Provider = "youtube",
            ProviderVideoId = "disabled-exam-video",
            Order = 1,
            MaxWatchCount = 3,
            IsActive = true
        };
        var videoExam = new Exam
        {
            Title = "Disabled mandatory video exam",
            Description = "Must not hide or lock its video",
            PassingScore = 5,
            TotalScore = 10,
            IsMandatory = true,
            IsActive = false,
            CreatedByTeacher = teacher,
            CreatedByTeacherId = teacher.Id,
            LessonVideoId = video.Id
        };
        video.ExamId = videoExam.Id;
        firstLesson.Videos.Add(video);
        teacher.Exams.Add(videoExam);

        db.Users.AddRange(student, teacherUser);
        db.Packages.Add(package);
        db.VideoTypes.Add(videoType);
        db.Exams.AddRange(exam, videoExam);
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            User = student,
            UserId = student.Id,
            PackageId = package.Id,
            GrantType = CodeType.Package,
            IsActive = true,
            GrantedAt = DateTime.UtcNow
        });
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new RegressionFixture(
            student.Id,
            section.Id,
            firstLesson.Id,
            secondLesson.Id,
            exam.Id,
            video.Id);
    }

    private sealed record RegressionFixture(
        Guid StudentId,
        Guid SectionId,
        Guid FirstLessonId,
        Guid SecondLessonId,
        Guid ExamId,
        Guid VideoId);
}
