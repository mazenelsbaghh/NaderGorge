using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Application.Features.Reports.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

/// <summary>
/// Production regression coverage for the 2026-09-03 incidents where four-part
/// completion drifted and archived content polluted dashboard totals/resume.
/// </summary>
public sealed class StudentDashboardVideoCompletionTests
{
    [Fact]
    public async Task Dashboard_CountsOnlyVisibleLessonsAndCompletesAfterEveryVisiblePartIsViewed()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedDashboardAsync(db);
        var handler = new GetDashboardQueryHandler(
            db,
            new AcademicScopeService(db),
            new ContentArchiveAccessService(db));

        var afterThreeParts = await handler.Handle(
            new GetDashboardQuery(fixture.StudentId),
            CancellationToken.None);

        Assert.True(afterThreeParts.Success, afterThreeParts.Message);
        Assert.Equal(1, afterThreeParts.Data!.TotalLessonsCompleted);
        Assert.Equal(2, afterThreeParts.Data.TotalLessons);
        Assert.Equal(50, afterThreeParts.Data.OverallProgressPercent);
        var packageAfterThreeParts = Assert.Single(afterThreeParts.Data.ActivePackages);
        Assert.Equal(fixture.VisiblePackageId, packageAfterThreeParts.Id);
        Assert.Equal(1, packageAfterThreeParts.LessonsCompleted);
        Assert.Equal(50, packageAfterThreeParts.ProgressPercent);
        Assert.Equal(fixture.FourPartLessonId, afterThreeParts.Data.ResumePoint?.LessonId);

        db.VideoWatchEvents.Add(new VideoWatchEvent
        {
            UserId = fixture.StudentId,
            LessonVideoId = fixture.FourthActiveVideoId,
            WatchCount = 1
        });
        await db.SaveChangesAsync();

        var afterFourthPart = await handler.Handle(
            new GetDashboardQuery(fixture.StudentId),
            CancellationToken.None);

        Assert.True(afterFourthPart.Success, afterFourthPart.Message);
        Assert.Equal(2, afterFourthPart.Data!.TotalLessonsCompleted);
        Assert.Equal(2, afterFourthPart.Data.TotalLessons);
        Assert.Equal(100, afterFourthPart.Data.OverallProgressPercent);
        var packageAfterFourthPart = Assert.Single(afterFourthPart.Data.ActivePackages);
        Assert.Equal(fixture.VisiblePackageId, packageAfterFourthPart.Id);
        Assert.Equal(2, packageAfterFourthPart.LessonsCompleted);
        Assert.Equal(100, packageAfterFourthPart.ProgressPercent);
        Assert.Null(afterFourthPart.Data.ResumePoint);

        var progress = await new GetProgressQueryHandler(
                db,
                new AcademicScopeService(db),
                new ContentArchiveAccessService(db))
            .Handle(new GetProgressQuery(fixture.StudentId), CancellationToken.None);

        Assert.True(progress.Success, progress.Message);
        Assert.Equal(2, progress.Data!.CompletedLessons);
        Assert.Equal(100, progress.Data.OverallPercent);
        Assert.All(Assert.Single(progress.Data.Packages).Lessons, lesson => Assert.True(lesson.IsCompleted));

        var parentReport = await new GetParentReportQueryHandler(
                db,
                new AcademicScopeService(db),
                new ContentArchiveAccessService(db))
            .Handle(new GetParentReportQuery(fixture.StudentId), CancellationToken.None);

        Assert.True(parentReport.Success, parentReport.Message);
        Assert.Equal(2, parentReport.Data!.CompletedLessonsCount);
    }

    private static async Task<DashboardFixture> SeedDashboardAsync(AppDbContext db)
    {
        var student = new User
        {
            FullName = "Four-part lesson student",
            PhoneNumber = $"dashboard-student-{Guid.NewGuid():N}",
            PasswordHash = "hash"
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

        var teacherUser = new User
        {
            FullName = "Four-part lesson teacher",
            PhoneNumber = $"dashboard-teacher-{Guid.NewGuid():N}",
            PasswordHash = "hash"
        };
        var teacher = new TeacherProfile
        {
            User = teacherUser,
            UserId = teacherUser.Id,
            Bio = "Dashboard regression fixture",
            Specialization = "Secondary",
            ContactInfo = "test@example.invalid",
            IsContentVisibleToStudents = true
        };
        teacherUser.TeacherProfile = teacher;

        var subject = new Subject
        {
            Name = "Dashboard regression subject",
            NormalizedName = $"DASHBOARD_REGRESSION_{Guid.NewGuid():N}"
        };
        var package = new Package
        {
            Name = "Four-part lesson package",
            Description = "Dashboard video completion regression",
            Subject = subject,
            SubjectId = subject.Id,
            Teacher = teacher,
            TeacherId = teacher.Id,
            TargetGrade = "FirstSecondary",
            ArchiveMode = ContentArchiveMode.ActiveSubscribersOnly,
            IsActive = true
        };
        var term = new Term
        {
            Package = package,
            PackageId = package.Id,
            Title = "Dashboard regression term",
            Order = 1
        };
        package.Terms.Add(term);
        var section = new ContentSection
        {
            Term = term,
            TermId = term.Id,
            Title = "Dashboard regression section",
            Order = 1
        };
        term.Sections.Add(section);

        var legacyCompletedLesson = new Lesson
        {
            ContentSection = section,
            ContentSectionId = section.Id,
            Title = "Legacy completed lesson",
            Summary = "Completion remains compatible with LessonProgress",
            Order = 1
        };
        var fourPartLesson = new Lesson
        {
            ContentSection = section,
            ContentSectionId = section.Id,
            Title = "Four-part lesson",
            Summary = "Completes from registered views",
            Order = 2
        };
        var archivedLesson = new Lesson
        {
            ContentSection = section,
            ContentSectionId = section.Id,
            Title = "Archived lesson",
            Summary = "Must not affect dashboard totals or resume",
            Order = 0,
            ArchiveMode = ContentArchiveMode.HiddenFromEveryone
        };
        var academicallyHiddenLesson = new Lesson
        {
            ContentSection = section,
            ContentSectionId = section.Id,
            Title = "Academically hidden lesson",
            Summary = "Must not affect dashboard totals or resume",
            Order = -1
        };
        section.Lessons.Add(academicallyHiddenLesson);
        section.Lessons.Add(archivedLesson);
        section.Lessons.Add(legacyCompletedLesson);
        section.Lessons.Add(fourPartLesson);

        var archivedPackage = new Package
        {
            Name = "Archived dashboard package",
            Description = "Must not appear on dashboard",
            Subject = subject,
            SubjectId = subject.Id,
            Teacher = teacher,
            TeacherId = teacher.Id,
            TargetGrade = "FirstSecondary",
            ArchiveMode = ContentArchiveMode.HiddenFromEveryone,
            IsActive = true
        };
        var archivedTerm = new Term
        {
            Package = archivedPackage,
            PackageId = archivedPackage.Id,
            Title = "Archived term",
            Order = 1
        };
        archivedPackage.Terms.Add(archivedTerm);
        var archivedSection = new ContentSection
        {
            Term = archivedTerm,
            TermId = archivedTerm.Id,
            Title = "Archived section",
            Order = 1
        };
        archivedTerm.Sections.Add(archivedSection);
        var archivedPackageLesson = new Lesson
        {
            ContentSection = archivedSection,
            ContentSectionId = archivedSection.Id,
            Title = "Archived package lesson",
            Order = 0
        };
        archivedSection.Lessons.Add(archivedPackageLesson);

        var videoType = new VideoType
        {
            Name = "شرح",
            NormalizedName = $"DASHBOARD_VIDEO_{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true
        };
        var legacyVideo = CreateVideo(legacyCompletedLesson, videoType, 1, isActive: true);
        legacyCompletedLesson.Videos.Add(legacyVideo);
        academicallyHiddenLesson.Videos.Add(CreateVideo(academicallyHiddenLesson, videoType, 1, isActive: true));
        archivedLesson.Videos.Add(CreateVideo(archivedLesson, videoType, 1, isActive: true));
        archivedPackageLesson.Videos.Add(CreateVideo(archivedPackageLesson, videoType, 1, isActive: true));

        var activeVideos = Enumerable.Range(1, 4)
            .Select(order => CreateVideo(fourPartLesson, videoType, order, isActive: true))
            .ToList();
        foreach (var video in activeVideos)
            fourPartLesson.Videos.Add(video);
        var academicallyHiddenVideo = CreateVideo(fourPartLesson, videoType, 5, isActive: true);
        var archiveHiddenVideo = CreateVideo(fourPartLesson, videoType, 6, isActive: true);
        archiveHiddenVideo.ArchiveMode = ContentArchiveMode.HiddenFromEveryone;
        fourPartLesson.Videos.Add(academicallyHiddenVideo);
        fourPartLesson.Videos.Add(archiveHiddenVideo);
        fourPartLesson.Videos.Add(CreateVideo(fourPartLesson, videoType, 7, isActive: false));

        var packageGrant = new StudentAccessGrant
        {
            User = student,
            UserId = student.Id,
            PackageId = package.Id,
            GrantType = CodeType.Package,
            IsActive = true,
            GrantedAt = DateTime.UtcNow
        };
        var packageScope = new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        };
        var archivedPackageScope = new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = archivedPackage.Id,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        };
        var archivedPackageGrant = new StudentAccessGrant
        {
            User = student,
            UserId = student.Id,
            PackageId = archivedPackage.Id,
            GrantType = CodeType.Package,
            IsActive = true,
            GrantedAt = DateTime.UtcNow
        };
        var hiddenVideoScope = new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.LessonVideo,
            OwnerId = academicallyHiddenVideo.Id,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary
        };
        var hiddenLessonScope = new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Lesson,
            OwnerId = academicallyHiddenLesson.Id,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary
        };
        var legacyProgress = new LessonProgress
        {
            UserId = student.Id,
            LessonId = legacyCompletedLesson.Id,
            IsCompleted = true
        };

        db.AddRange(
            student,
            teacherUser,
            subject,
            package,
            archivedPackage,
            term,
            archivedTerm,
            section,
            archivedSection,
            academicallyHiddenLesson,
            archivedLesson,
            archivedPackageLesson,
            legacyCompletedLesson,
            fourPartLesson,
            videoType,
            packageGrant,
            archivedPackageGrant,
            packageScope,
            archivedPackageScope,
            hiddenLessonScope,
            hiddenVideoScope,
            legacyProgress);
        db.VideoWatchEvents.AddRange(activeVideos.Take(3).Select(video => new VideoWatchEvent
        {
            UserId = student.Id,
            LessonVideoId = video.Id,
            WatchCount = 1
        }));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new DashboardFixture(
            student.Id,
            package.Id,
            fourPartLesson.Id,
            activeVideos[3].Id);
    }

    private static LessonVideo CreateVideo(
        Lesson lesson,
        VideoType videoType,
        int order,
        bool isActive) => new()
    {
        Lesson = lesson,
        LessonId = lesson.Id,
        VideoType = videoType,
        VideoTypeId = videoType.Id,
        Title = $"Part {order}",
        Provider = "youtube",
        ProviderVideoId = $"part-{order}-{Guid.NewGuid():N}",
        Order = order,
        MaxWatchCount = 5,
        IsActive = isActive
    };

    private sealed record DashboardFixture(
        Guid StudentId,
        Guid VisiblePackageId,
        Guid FourPartLessonId,
        Guid FourthActiveVideoId);
}
