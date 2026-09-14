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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LessonList_ReusesStudentPassesWithoutUnlockingUnpassedMandatoryExam(bool passed)
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedDisabledLessonExamAsync(db);
        var exam = await db.Exams.SingleAsync(e => e.Id == fixture.ExamId);
        exam.IsActive = true;
        db.StudentExamAttempts.Add(new StudentExamAttempt
        {
            UserId = fixture.StudentId,
            ExamId = fixture.ExamId,
            IsPassed = passed
        });
        await db.SaveChangesAsync();
        var scope = new AcademicScopeService(db);
        var archives = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, scope, archives);

        var response = await new GetLessonsQueryHandler(db, access, scope, archives)
            .Handle(new GetLessonsQuery(fixture.SectionId, fixture.StudentId), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        foreach (var lessonId in new[] { fixture.FirstLessonId, fixture.SecondLessonId })
        {
            var lesson = Assert.Single(response.Data!, lesson => lesson.Id == lessonId);
            Assert.Equal(!passed, lesson.IsLocked);
            Assert.Equal(passed ? (Guid?)null : fixture.ExamId, lesson.BlockingExamId);
        }
    }

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

    // 2026-09-14 production incident: buying lesson three redirected to an unowned lesson-two exam.
    [Theory]
    [InlineData(CodeType.Lesson, false)]
    [InlineData(CodeType.Month, true)]
    [InlineData(CodeType.Term, true)]
    [InlineData(CodeType.Package, true)]
    public async Task PurchasedLesson_ListsOnlyOwnedPrerequisites(CodeType grantType, bool ownsPrevious)
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedDisabledLessonExamAsync(db);
        await ConfigurePurchaseAsync(db, fixture, grantType);
        var currentExam = await AddCurrentExamAsync(db, fixture);
        var scope = new AcademicScopeService(db);
        var archives = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, scope, archives);

        var list = await new GetLessonsQueryHandler(db, access, scope, archives)
            .Handle(new(fixture.SectionId, fixture.StudentId), default);
        var detail = await new GetLessonDetailQueryHandler(db, access,
                new TeacherAuthorizationService(db), scope, archives)
            .Handle(new(fixture.SecondLessonId, fixture.StudentId), default);

        Assert.True(list.Success, list.Message);
        Assert.True(detail.Success, detail.Message);
        var lesson = Assert.Single(list.Data!, x => x.Id == fixture.SecondLessonId);
        var expectedBlocker = ownsPrevious ? fixture.ExamId : currentExam.Id;
        Assert.True(lesson.IsLocked);
        Assert.Equal(expectedBlocker, lesson.BlockingExamId);
        Assert.True(detail.Data!.IsLocked);
        Assert.Equal(expectedBlocker, detail.Data.BlockingExamId);
        Assert.Equal(ownsPrevious, detail.Data.IsExamLocked);
        Assert.Equal(ownsPrevious, await access.HasAccessToExamAsync(fixture.StudentId, fixture.ExamId));
    }

    [Theory]
    [InlineData(CodeType.Lesson, true)]
    [InlineData(CodeType.Package, false)]
    public async Task PurchasedLesson_StartsOwnExamWithoutGrantingPreviousExam(CodeType grantType, bool allowed)
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedDisabledLessonExamAsync(db);
        await ConfigurePurchaseAsync(db, fixture, grantType);
        var currentExam = await AddCurrentExamAsync(db, fixture);
        var access = new AccessCheckService(db, new AcademicScopeService(db));
        var handler = new StartExamAttemptCommandHandler(db, access);

        var started = await handler.Handle(new(currentExam.Id, fixture.StudentId), default);

        Assert.Equal(allowed, started.Success);
        if (allowed) Assert.Single(started.Data!.Questions);
        Assert.Equal(allowed, await db.StudentExamAttempts.AnyAsync(x => x.ExamId == currentExam.Id));
        if (allowed)
        {
            var denied = await handler.Handle(new(fixture.ExamId, fixture.StudentId), default);
            Assert.False(denied.Success);
            Assert.False(await db.StudentExamAttempts.AnyAsync(x => x.ExamId == fixture.ExamId));
        }
        else
        {
            Assert.Contains("أولاً", started.Message);
        }
    }

    [Theory]
    [InlineData(CodeType.Lesson, true, true)]
    [InlineData(CodeType.Package, false, true)]
    [InlineData(CodeType.Lesson, true, false)]
    [InlineData(CodeType.Package, false, false)]
    public async Task PurchasedLesson_StartsAndSubmitsOwnHomeworkWithoutPreviousPurchase(
        CodeType grantType, bool allowed, bool previousExam)
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedDisabledLessonExamAsync(db);
        await ConfigurePurchaseAsync(db, fixture, grantType);
        var homework = new NaderGorge.Domain.Entities.Homework.Homework
        {
            LessonId = fixture.SecondLessonId, Title = "Purchased lesson homework", TotalScore = 1,
            IsActive = true, IsMandatory = true
        };
        var question = new NaderGorge.Domain.Entities.Homework.HomeworkQuestion
        {
            HomeworkId = homework.Id, BodyText = "Choose A", PointsActive = 1,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ,
            PossibleAnswers = ["A", "B"], CorrectAnswerKey = "A"
        };
        if (!previousExam)
        {
            (await db.Exams.SingleAsync(x => x.Id == fixture.ExamId)).IsActive = false;
            var previousHomework = new NaderGorge.Domain.Entities.Homework.Homework
            {
                LessonId = fixture.FirstLessonId, Title = "Previous homework", TotalScore = 1,
                IsActive = true, IsMandatory = true
            };
            db.Homeworks.Add(previousHomework);
            db.HomeworkQuestions.Add(new NaderGorge.Domain.Entities.Homework.HomeworkQuestion
            {
                HomeworkId = previousHomework.Id, BodyText = "Previous question", PointsActive = 1
            });
        }
        db.Homeworks.Add(homework);
        db.HomeworkQuestions.Add(question);
        await db.SaveChangesAsync();
        var access = new AccessCheckService(db, new AcademicScopeService(db));
        var started = await new NaderGorge.Application.Features.Homework.Queries.StartHomeworkAttemptQueryHandler(db, access)
            .Handle(new(homework.Id, fixture.StudentId), default);
        var submitted = await new NaderGorge.Application.Features.Homework.Commands.SubmitHomeworkCommandHandler(
                db, new NoOpPublisher(), access, new NoOpJobEnqueuer())
            .Handle(new(homework.Id, fixture.StudentId, [new(question.Id, "A")]), default);

        Assert.Equal(allowed, started.Success);
        Assert.Equal(allowed, submitted.Success);
        if (allowed)
        {
            var submission = await db.HomeworkSubmissions.SingleAsync();
            Assert.Equal(NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded, submission.Status);
            Assert.Equal(1, submission.OverallScore);
        }
        else
        {
            Assert.Empty(await db.HomeworkSubmissions.ToListAsync());
        }
    }

    private static async Task ConfigurePurchaseAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db, RegressionFixture fixture, CodeType grantType)
    {
        var grant = await db.StudentAccessGrants.SingleAsync();
        var section = await db.ContentSections.Include(x => x.Term).SingleAsync(x => x.Id == fixture.SectionId);
        grant.GrantType = grantType;
        grant.LessonId = fixture.SecondLessonId;
        grant.ContentSectionId = fixture.SectionId;
        grant.TermId = section.TermId;
        (await db.Exams.SingleAsync(x => x.Id == fixture.ExamId)).IsActive = true;
        await db.SaveChangesAsync();
    }

    private static async Task<Exam> AddCurrentExamAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db, RegressionFixture fixture)
    {
        var previousExam = await db.Exams.SingleAsync(x => x.Id == fixture.ExamId);
        var exam = new Exam
        {
            Title = "Purchased lesson exam", IsActive = true, IsMandatory = true,
            CreatedByTeacherId = previousExam.CreatedByTeacherId, TotalScore = 1, PassingScore = 1
        };
        var question = new QuestionBankItem
        {
            Text = "Choose A", Type = QuestionType.MCQ, DefaultPoints = 1,
            CreatedByTeacherId = previousExam.CreatedByTeacherId,
            Options = [new QuestionOption { Text = "A", IsCorrect = true }, new QuestionOption { Text = "B" }]
        };
        exam.ExamQuestions.Add(new ExamQuestion { Question = question, Points = 1, Order = 1 });
        db.Exams.Add(exam);
        (await db.Lessons.SingleAsync(x => x.Id == fixture.SecondLessonId)).ExamId = exam.Id;
        await db.SaveChangesAsync();
        return exam;
    }

    // Notifications and queue delivery are external side effects, outside the access regression.
    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }

    private sealed class NoOpJobEnqueuer : NaderGorge.Application.Interfaces.IJobEnqueuer
    {
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data) => Task.CompletedTask;
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
