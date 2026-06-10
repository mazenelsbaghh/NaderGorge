using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Teacher;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.MultiTeacher;

public class TeacherIsolationTests
{
    private async Task<(User User, TeacherProfile Profile)> OnboardTeacherAsync(AppDbContext db, string name, string phone)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, name, phone);
        var role = new Role { Id = Guid.NewGuid(), Name = "Teacher", Type = RoleType.Teacher };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        var profile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Bio = $"{name} Bio",
            Specialization = "Science",
            ContactInfo = $"{name.ToLower()}@test.com"
        };
        db.TeacherProfiles.Add(profile);
        await db.SaveChangesAsync();

        return (user, profile);
    }

    private async Task<(User User, Role Role)> OnboardAdminAsync(AppDbContext db, string name, string phone)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, name, phone);
        var role = new Role { Id = Guid.NewGuid(), Name = "Admin", Type = RoleType.Admin };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
        return (user, role);
    }

    [Fact]
    public async Task CanAccessPackage_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");
        var (admin, _) = await OnboardAdminAsync(db, "Admin User", "01033333333");

        var packageA = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Package A",
            Description = "A description",
            TeacherId = profileA.Id
        };
        db.Packages.Add(packageA);
        await db.SaveChangesAsync();

        var authService = new TeacherAuthorizationService(db);

        // Teacher A should access their own package
        Assert.True(await authService.CanAccessPackageAsync(userA.Id, packageA.Id, CancellationToken.None));

        // Teacher B should NOT access Teacher A's package
        Assert.False(await authService.CanAccessPackageAsync(userB.Id, packageA.Id, CancellationToken.None));

        // Admin should access Teacher A's package
        Assert.True(await authService.CanAccessPackageAsync(admin.Id, packageA.Id, CancellationToken.None));
    }


    [Fact]
    public async Task CanAccessCodeGroup_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var codeGroupA = new CodeGroup
        {
            Id = Guid.NewGuid(),
            Name = "Group A",
            TeacherId = profileA.Id
        };
        db.CodeGroups.Add(codeGroupA);
        await db.SaveChangesAsync();

        var authService = new TeacherAuthorizationService(db);

        Assert.True(await authService.CanAccessCodeGroupAsync(userA.Id, codeGroupA.Id, CancellationToken.None));
        Assert.False(await authService.CanAccessCodeGroupAsync(userB.Id, codeGroupA.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CanAccessExam_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var examA = new Exam
        {
            Id = Guid.NewGuid(),
            Title = "Exam A",
            CreatedByTeacherId = profileA.Id
        };
        db.Exams.Add(examA);
        await db.SaveChangesAsync();

        var authService = new TeacherAuthorizationService(db);

        Assert.True(await authService.CanAccessExamAsync(userA.Id, examA.Id, CancellationToken.None));
        Assert.False(await authService.CanAccessExamAsync(userB.Id, examA.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CreateInlineExam_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var subject = new Subject { Id = Guid.NewGuid(), Name = "Math" };
        db.Subjects.Add(subject);

        // Teacher A has Subject, Package, Term, Section, Lesson
        db.TeacherSubjects.Add(new TeacherSubject { TeacherId = profileA.Id, SubjectId = subject.Id });
        var package = new Package { Id = Guid.NewGuid(), Name = "Package A", TeacherId = profileA.Id, SubjectId = subject.Id, TargetGrade = "3rd Secondary" };
        db.Packages.Add(package);
        var term = new Term { Id = Guid.NewGuid(), Title = "Term A", PackageId = package.Id };
        db.Terms.Add(term);
        var section = new ContentSection { Id = Guid.NewGuid(), Title = "Section A", TermId = term.Id };
        db.ContentSections.Add(section);
        var lesson = new Lesson { Id = Guid.NewGuid(), Title = "Lesson A", ContentSectionId = section.Id };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();

        var auth = new TeacherAuthorizationService(db);
        var handler = new CreateInlineExamCommandHandler(db, auth);

        // Teacher B tries to create inline exam on Teacher A's lesson -> fails
        var cmdB = new CreateInlineExamCommand
        {
            Title = "Exam B",
            Target = new ExamTargetDto { Type = "Lesson", Id = lesson.Id },
            CurrentUserId = userB.Id
        };
        var resultB = await handler.Handle(cmdB, CancellationToken.None);
        Assert.False(resultB.Success);

        // Teacher A tries to create inline exam -> succeeds
        var cmdA = new CreateInlineExamCommand
        {
            Title = "Exam A",
            Target = new ExamTargetDto { Type = "Lesson", Id = lesson.Id },
            CurrentUserId = userA.Id
        };
        var resultA = await handler.Handle(cmdA, CancellationToken.None);
        Assert.True(resultA.Success);
    }

    [Fact]
    public async Task AddQuestionsToExam_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var subject = new Subject { Id = Guid.NewGuid(), Name = "Math" };
        db.Subjects.Add(subject);

        var examA = new Exam { Id = Guid.NewGuid(), Title = "Exam A", CreatedByTeacherId = profileA.Id };
        db.Exams.Add(examA);
        await db.SaveChangesAsync();

        var auth = new TeacherAuthorizationService(db);
        var handler = new AddQuestionsToExamCommandHandler(db, auth);

        // Teacher B tries to add questions to Teacher A's exam -> fails
        var resultB = await handler.Handle(new AddQuestionsToExamCommand
        {
            ExamId = examA.Id,
            CurrentUserId = userB.Id,
            Questions = new List<InlineExamQuestionDto> { new InlineExamQuestionDto { Text = "Q", Points = 1, Order = 1 } }
        }, CancellationToken.None);
        Assert.False(resultB.Success);

        // Teacher A tries to add questions -> succeeds
        var resultA = await handler.Handle(new AddQuestionsToExamCommand
        {
            ExamId = examA.Id,
            CurrentUserId = userA.Id,
            Questions = new List<InlineExamQuestionDto> { new InlineExamQuestionDto { Text = "Q", Points = 1, Order = 1 } }
        }, CancellationToken.None);
        Assert.True(resultA.Success);
    }

    [Fact]
    public async Task GradeEssay_IsIsolatedBetweenTeachers()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var question = new QuestionBankItem { Id = Guid.NewGuid(), CreatedByTeacherId = profileA.Id };
        db.QuestionBankItems.Add(question);

        var submission = new EssaySubmission
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            Status = EssaySubmissionStatus.WaitTeacher,
            StudentExamAttemptId = Guid.NewGuid()
        };
        db.EssaySubmissions.Add(submission);
        await db.SaveChangesAsync();

        var auth = new TeacherAuthorizationService(db);
        var handler = new GradeEssayCommandHandler(db, auth);

        // Teacher B tries to grade Teacher A's essay submission -> fails
        var resultB = await handler.Handle(new GradeEssayCommand(submission.Id, 5m, "FB", userB.Id), CancellationToken.None);
        Assert.False(resultB.Success);

        // Teacher A tries to grade -> succeeds
        var resultA = await handler.Handle(new GradeEssayCommand(submission.Id, 5m, "FB", userA.Id), CancellationToken.None);
        Assert.True(resultA.Success);
    }

    [Fact]
    public async Task GetTeacherActivity_IsIsolatedAndFilteredByTeacherPackages()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (userA, profileA) = await OnboardTeacherAsync(db, "Teacher A", "01011111111");
        var (userB, profileB) = await OnboardTeacherAsync(db, "Teacher B", "01022222222");

        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student A", "01099999999");

        // 1. Create Package A for Teacher A
        var packageA = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Package A",
            TeacherId = profileA.Id,
            SubjectId = Guid.NewGuid(),
            TargetGrade = "3rd Secondary"
        };
        db.Packages.Add(packageA);

        // 2. Create Package B for Teacher B
        var packageB = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Package B",
            TeacherId = profileB.Id,
            SubjectId = Guid.NewGuid(),
            TargetGrade = "3rd Secondary"
        };
        db.Packages.Add(packageB);

        // 3. Create content items for Package A (Term, Section, Lesson, Video, Watch Event)
        var termA = new Term { Id = Guid.NewGuid(), Title = "Term A", PackageId = packageA.Id };
        var sectionA = new ContentSection { Id = Guid.NewGuid(), Title = "Sec A", TermId = termA.Id };
        var lessonA = new Lesson { Id = Guid.NewGuid(), Title = "Lesson A", ContentSectionId = sectionA.Id };
        var videoA = new LessonVideo { Id = Guid.NewGuid(), Title = "Video A", LessonId = lessonA.Id, Provider = "vk", ProviderVideoId = "vA" };
        db.Terms.Add(termA);
        db.ContentSections.Add(sectionA);
        db.Lessons.Add(lessonA);
        db.LessonVideos.Add(videoA);

        // 4. Create content items for Package B
        var termB = new Term { Id = Guid.NewGuid(), Title = "Term B", PackageId = packageB.Id };
        var sectionB = new ContentSection { Id = Guid.NewGuid(), Title = "Sec B", TermId = termB.Id };
        var lessonB = new Lesson { Id = Guid.NewGuid(), Title = "Lesson B", ContentSectionId = sectionB.Id };
        var videoB = new LessonVideo { Id = Guid.NewGuid(), Title = "Video B", LessonId = lessonB.Id, Provider = "vk", ProviderVideoId = "vB" };
        db.Terms.Add(termB);
        db.ContentSections.Add(sectionB);
        db.Lessons.Add(lessonB);
        db.LessonVideos.Add(videoB);

        // 5. Add Video Watch Events
        var watchEventA = new VideoWatchEvent
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            LessonVideoId = videoA.Id,
            WatchCount = 3,
            TimeWatchedInSeconds = 300,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var watchEventB = new VideoWatchEvent
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            LessonVideoId = videoB.Id,
            WatchCount = 5,
            TimeWatchedInSeconds = 500,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.VideoWatchEvents.AddRange(watchEventA, watchEventB);

        // 6. Grant Student Access to Package A & B
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            PackageId = packageA.Id,
            IsActive = true
        });
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            PackageId = packageB.Id,
            IsActive = true
        });

        await db.SaveChangesAsync();

        // 7. Run GetTeacherActivityQuery for Teacher A
        var handler = new GetTeacherActivityQueryHandler(db);
        var result = await handler.Handle(new GetTeacherActivityQuery(userA.Id), CancellationToken.None);

        // 8. Assertions
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        // Active students should only include watch events on Package A (belonging to Teacher A)
        Assert.Single(result.Data.ActiveStudents);
        Assert.Equal("Video A", result.Data.ActiveStudents.First().LastWatchedVideoTitle);
        Assert.Equal("Package A", result.Data.ActiveStudents.First().PackageName);

        // Most watched videos should only include Package A video
        Assert.Single(result.Data.MostWatchedVideos);
        Assert.Equal("Video A", result.Data.MostWatchedVideos.First().VideoTitle);
        Assert.Equal(3, result.Data.MostWatchedVideos.First().TotalWatchCount);
    }
}
