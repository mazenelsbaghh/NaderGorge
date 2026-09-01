using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Features.Homework;
using NaderGorge.Application.Features.Homework.Commands;
using NaderGorge.Application.Features.Homework.Queries;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;

namespace NaderGorge.Application.Tests;

public sealed class HomeworkComingSoonTests
{
    [Fact]
    public async Task ComingSoonDate_CanBeSetSurfacedInCockpitAndCleared()
    {
        await using var db = TestAppDbContextFactory.Create();
        var lesson = await SeedLessonAsync(db);
        var authorization = new TeacherAuthorizationService(db);
        var commandHandler = new SetLessonHomeworkComingSoonCommandHandler(db, authorization);
        var cockpitHandler = new GetLessonCockpitQueryHandler(db, authorization);
        var tomorrow = CairoTime.GetCurrentDate().AddDays(1);

        var setResult = await commandHandler.Handle(
            new SetLessonHomeworkComingSoonCommand(lesson.Id, tomorrow),
            CancellationToken.None);

        Assert.True(setResult.Success, setResult.Message);
        db.ChangeTracker.Clear();
        Assert.Equal(
            tomorrow,
            await db.Lessons
                .Where(item => item.Id == lesson.Id)
                .Select(item => item.HomeworkComingSoonOn)
                .SingleAsync());

        var cockpitWithDate = await cockpitHandler.Handle(
            new GetLessonCockpitQuery(lesson.Id),
            CancellationToken.None);

        Assert.True(cockpitWithDate.Success, cockpitWithDate.Message);
        Assert.Equal(tomorrow, cockpitWithDate.Data!.HomeworkComingSoonOn);

        var clearResult = await commandHandler.Handle(
            new SetLessonHomeworkComingSoonCommand(lesson.Id, null),
            CancellationToken.None);

        Assert.True(clearResult.Success, clearResult.Message);
        db.ChangeTracker.Clear();

        var cockpitWithoutDate = await cockpitHandler.Handle(
            new GetLessonCockpitQuery(lesson.Id),
            CancellationToken.None);

        Assert.True(cockpitWithoutDate.Success, cockpitWithoutDate.Message);
        Assert.Null(cockpitWithoutDate.Data!.HomeworkComingSoonOn);
    }

    [Fact]
    public async Task EmptyHomeworkSchedule_CanBeSavedAndClearedWithoutPublishedEvent()
    {
        await using var db = TestAppDbContextFactory.Create();
        var lesson = await SeedLessonAsync(db);
        var tomorrow = CairoTime.GetCurrentDate().AddDays(1);
        var handler = new AttachHomeworkCommandHandler(db, new TeacherAuthorizationService(db));

        var result = await handler.Handle(
            new AttachHomeworkCommand(
                LessonId: lesson.Id,
                Title: "واجب الحصة",
                Instructions: "سيتم نشر الأسئلة غدًا.",
                IsMandatory: true,
                IsRandomized: false,
                RequiredPointsToPass: 5,
                TotalScore: 10,
                Questions: [],
                HomeworkComingSoonOn: tomorrow),
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        db.ChangeTracker.Clear();

        var homework = await db.Homeworks
            .Include(item => item.Questions)
            .SingleAsync(item => item.Id == result.Data);
        var persistedDate = await db.Lessons
            .Where(item => item.Id == lesson.Id)
            .Select(item => item.HomeworkComingSoonOn)
            .SingleAsync();

        Assert.False(homework.IsActive);
        Assert.Empty(homework.Questions);
        Assert.Equal(tomorrow, persistedDate);
        Assert.DoesNotContain(
            await db.OutboxEvents.ToListAsync(),
            item => item.Type == "HomeworkPublished");

        var clearResult = await handler.Handle(
            new AttachHomeworkCommand(
                LessonId: lesson.Id,
                Title: "واجب الحصة",
                Instructions: "لا يوجد موعد معلن حاليًا.",
                IsMandatory: true,
                IsRandomized: false,
                RequiredPointsToPass: 5,
                TotalScore: 10,
                Questions: [],
                HomeworkComingSoonOn: null),
            CancellationToken.None);

        Assert.True(clearResult.Success, clearResult.Message);
        Assert.Equal(result.Data, clearResult.Data);
        db.ChangeTracker.Clear();
        Assert.Null(await db.Lessons
            .Where(item => item.Id == lesson.Id)
            .Select(item => item.HomeworkComingSoonOn)
            .SingleAsync());
        Assert.DoesNotContain(
            await db.OutboxEvents.ToListAsync(),
            item => item.Type == "HomeworkPublished");
    }

    [Fact]
    public async Task DraftWithQuestions_PublishesOnActivationAndRejectsLiveEdits()
    {
        await using var db = TestAppDbContextFactory.Create();
        var lesson = await SeedLessonAsync(db);
        var tomorrow = CairoTime.GetCurrentDate().AddDays(1);
        var authorization = new TeacherAuthorizationService(db);
        var attachHandler = new AttachHomeworkCommandHandler(db, authorization);

        var draftResult = await attachHandler.Handle(
            CreateAttachCommand(lesson.Id, [], tomorrow),
            CancellationToken.None);
        var questionResult = await attachHandler.Handle(
            CreateAttachCommand(
                lesson.Id,
                [new AttachHomeworkQuestionDto("Prepared question", 1, 10, "Essay")],
                null),
            CancellationToken.None);

        Assert.True(draftResult.Success, draftResult.Message);
        Assert.True(questionResult.Success, questionResult.Message);
        Assert.Equal(draftResult.Data, questionResult.Data);
        Assert.False(await db.Homeworks
            .Where(item => item.Id == draftResult.Data)
            .Select(item => item.IsActive)
            .SingleAsync());
        Assert.Equal(tomorrow, await db.Lessons
            .Where(item => item.Id == lesson.Id)
            .Select(item => item.HomeworkComingSoonOn)
            .SingleAsync());
        Assert.Empty(await db.OutboxEvents
            .Where(item => item.Type == "HomeworkPublished")
            .ToListAsync());

        var activationResult = await new SetHomeworkActiveStatusCommandHandler(db, authorization)
            .Handle(
                new SetHomeworkActiveStatusCommand(draftResult.Data, IsActive: true),
                CancellationToken.None);

        Assert.True(activationResult.Success, activationResult.Message);
        Assert.Null(await db.Lessons
            .Where(item => item.Id == lesson.Id)
            .Select(item => item.HomeworkComingSoonOn)
            .SingleAsync());
        Assert.Single(await db.OutboxEvents
            .Where(item => item.Type == "HomeworkPublished")
            .ToListAsync());

        var repeatedActivationResult = await new SetHomeworkActiveStatusCommandHandler(db, authorization)
            .Handle(
                new SetHomeworkActiveStatusCommand(draftResult.Data, IsActive: true),
                CancellationToken.None);

        Assert.True(repeatedActivationResult.Success, repeatedActivationResult.Message);
        Assert.Single(await db.OutboxEvents
            .Where(item => item.Type == "HomeworkPublished")
            .ToListAsync());

        var editResult = await attachHandler.Handle(
            CreateAttachCommand(
                lesson.Id,
                [new AttachHomeworkQuestionDto("Edited published question", 1, 10, "Essay")],
                null),
            CancellationToken.None);

        Assert.False(editResult.Success);
        Assert.Contains("HOMEWORK_DEACTIVATE_BEFORE_EDITING", editResult.Errors!);
        Assert.Single(await db.OutboxEvents
            .Where(item => item.Type == "HomeworkPublished")
            .ToListAsync());
        Assert.Equal("Prepared question", await db.HomeworkQuestions
            .Where(item => item.HomeworkId == draftResult.Data)
            .Select(item => item.BodyText)
            .SingleAsync());
    }

    [Fact]
    public async Task HomeworkWithStudentWork_RejectsQuestionReplacementAndPreservesAnswers()
    {
        await using var db = TestAppDbContextFactory.Create();
        var lesson = await SeedLessonAsync(db);
        var student = await TestAppDbContextFactory.SeedUserAsync(
            db,
            "Homework history student",
            "201000000093");
        var homework = CreateHomework(lesson.Id, "Historical homework", isActive: false);
        var question = CreateQuestion(homework.Id, "Historical question");
        var submission = new HomeworkSubmission
        {
            HomeworkId = homework.Id,
            StudentId = student.Id,
            Status = SubmissionStatus.Graded,
            OverallScore = 10,
            SubmittedAt = DateTime.UtcNow
        };
        var answer = new HomeworkAnswer
        {
            HomeworkSubmissionId = submission.Id,
            QuestionId = question.Id,
            ProvidedAnswer = "Historical answer",
            ScoreReceived = 10
        };
        db.AddRange(homework, question, submission, answer);
        await db.SaveChangesAsync();

        var editResult = await new AttachHomeworkCommandHandler(
                db,
                new TeacherAuthorizationService(db))
            .Handle(
                CreateAttachCommand(
                    lesson.Id,
                    [new AttachHomeworkQuestionDto("Replacement question", 1, 10, "Essay")],
                    null),
                CancellationToken.None);

        Assert.False(editResult.Success);
        Assert.Contains("HOMEWORK_HAS_SUBMISSIONS", editResult.Errors!);
        Assert.Equal("Historical question", await db.HomeworkQuestions
            .Where(item => item.Id == question.Id)
            .Select(item => item.BodyText)
            .SingleAsync());
        Assert.Equal("Historical answer", await db.HomeworkAnswers
            .Where(item => item.Id == answer.Id)
            .Select(item => item.ProvidedAnswer)
            .SingleAsync());
    }

    [Fact]
    public async Task ActivatingHomeworkWithoutQuestions_IsRejectedAndRemainsInactive()
    {
        await using var db = TestAppDbContextFactory.Create();
        var lesson = await SeedLessonAsync(db);
        var homework = new HomeworkEntity
        {
            LessonId = lesson.Id,
            Title = "واجب بلا أسئلة",
            IsActive = false,
            TotalScore = 10
        };
        db.Homeworks.Add(homework);
        await db.SaveChangesAsync();
        var handler = new SetHomeworkActiveStatusCommandHandler(db, new TeacherAuthorizationService(db));

        var result = await handler.Handle(
            new SetHomeworkActiveStatusCommand(homework.Id, IsActive: true),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("HOMEWORK_QUESTIONS_REQUIRED", result.Errors!);
        db.ChangeTracker.Clear();
        Assert.False(await db.Homeworks
            .Where(item => item.Id == homework.Id)
            .Select(item => item.IsActive)
            .SingleAsync());
    }

    [Fact]
    public async Task ReadyForStudents_ReturnsOnlyActiveHomeworkWithAtLeastOneQuestion()
    {
        await using var db = TestAppDbContextFactory.Create();
        var lessonId = Guid.NewGuid();
        var inactiveWithQuestion = CreateHomework(lessonId, "Inactive", isActive: false);
        var activeWithoutQuestions = CreateHomework(lessonId, "Empty", isActive: true);
        var ready = CreateHomework(lessonId, "Ready", isActive: true);

        db.Homeworks.AddRange(inactiveWithQuestion, activeWithoutQuestions, ready);
        db.HomeworkQuestions.AddRange(
            CreateQuestion(inactiveWithQuestion.Id, "Hidden question"),
            CreateQuestion(ready.Id, "Visible question"));
        await db.SaveChangesAsync();

        var readyIds = await db.Homeworks
            .ReadyForStudents()
            .Select(item => item.Id)
            .ToListAsync();

        Assert.Equal([ready.Id], readyIds);
    }

    [Fact]
    public async Task MandatoryDraft_IsAnnouncementOnlyAndDoesNotAppearOrLockStudentSurfaces()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedStudentHomeworkDraftAsync(db);
        var academicScope = new AcademicScopeService(db);
        var archiveAccess = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, academicScope, archiveAccess);

        var detail = await new GetLessonDetailQueryHandler(
                db,
                access,
                new TeacherAuthorizationService(db),
                academicScope,
                archiveAccess)
            .Handle(
                new GetLessonDetailQuery(fixture.DraftLessonId, fixture.StudentId),
                CancellationToken.None);
        var dashboard = await new GetDashboardQueryHandler(db, academicScope, archiveAccess)
            .Handle(new GetDashboardQuery(fixture.StudentId), CancellationToken.None);
        var lessons = await new GetLessonsQueryHandler(db, access, academicScope, archiveAccess)
            .Handle(new GetLessonsQuery(fixture.SectionId, fixture.StudentId), CancellationToken.None);
        var progress = await new GetProgressQueryHandler(db, academicScope, archiveAccess)
            .Handle(new GetProgressQuery(fixture.StudentId), CancellationToken.None);

        db.HomeworkSubmissions.Add(new HomeworkSubmission
        {
            HomeworkId = fixture.HomeworkId,
            StudentId = fixture.StudentId,
            Status = SubmissionStatus.InProgress,
            StartedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var pending = await new GetPendingHomeworkQueryHandler(db, archiveAccess)
            .Handle(new GetPendingHomeworkQuery(fixture.StudentId), CancellationToken.None);

        Assert.True(detail.Success, detail.Message);
        Assert.Null(detail.Data!.HomeworkId);
        Assert.Null(detail.Data.Homework);
        Assert.Null(detail.Data.HomeworkStatus);
        Assert.Equal(fixture.ExpectedOn, detail.Data.HomeworkComingSoonOn);

        Assert.True(dashboard.Success, dashboard.Message);
        Assert.DoesNotContain(
            dashboard.Data!.UpcomingHomeworks,
            item => item.HomeworkId == fixture.HomeworkId);

        Assert.True(pending.Success, pending.Message);
        Assert.DoesNotContain(
            pending.Data!,
            item => item.Id == fixture.HomeworkId);

        Assert.True(lessons.Success, lessons.Message);
        var followingLesson = Assert.Single(
            lessons.Data!,
            item => item.Id == fixture.FollowingLessonId);
        Assert.False(followingLesson.IsLocked);
        Assert.Null(followingLesson.BlockingHomeworkLessonId);

        Assert.True(progress.Success, progress.Message);
        var packageProgress = Assert.Single(progress.Data!.Packages);
        var followingLessonProgress = Assert.Single(
            packageProgress.Lessons,
            item => item.Id == fixture.FollowingLessonId);
        Assert.False(followingLessonProgress.IsLocked);
    }

    [Fact]
    public async Task DirectDraftStartAndSubmit_AreRejectedWithoutCreatingStudentWork()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedStudentHomeworkDraftAsync(db);
        var academicScope = new AcademicScopeService(db);
        var archiveAccess = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, academicScope, archiveAccess);

        var startResult = await new StartHomeworkAttemptQueryHandler(db, access, archiveAccess)
            .Handle(
                new StartHomeworkAttemptQuery(fixture.HomeworkId, fixture.StudentId),
                CancellationToken.None);
        var submitResult = await new SubmitHomeworkCommandHandler(
                db,
                new HomeworkComingSoonNoOpPublisher(),
                access,
                new HomeworkComingSoonNoOpJobEnqueuer(),
                archiveAccess)
            .Handle(
                new SubmitHomeworkCommand(
                    fixture.HomeworkId,
                    fixture.StudentId,
                    [new StudentAnswerInput(Guid.NewGuid(), "Direct draft answer")]),
                CancellationToken.None);
        var resultLookup = await new GetHomeworkResultQueryHandler(db, access)
            .Handle(
                new GetHomeworkResultQuery(fixture.HomeworkId, fixture.StudentId),
                CancellationToken.None);

        Assert.False(startResult.Success);
        Assert.Contains("غير متاح", startResult.Message!);
        Assert.False(submitResult.Success);
        Assert.Contains("غير متاح", submitResult.Message!);
        Assert.False(resultLookup.Success);
        Assert.Contains("غير متاح", resultLookup.Message!);
        Assert.Empty(await db.HomeworkSubmissions.ToListAsync());
        Assert.Empty(await db.HomeworkAnswers.ToListAsync());
    }

    [Fact]
    public async Task ArchivedHomework_HidesResultAndDoesNotLockFollowingLesson()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedStudentHomeworkDraftAsync(db);
        var homework = await db.Homeworks.SingleAsync(item => item.Id == fixture.HomeworkId);
        var question = CreateQuestion(homework.Id, "Archived question");
        homework.IsActive = true;
        homework.ArchiveMode = ContentArchiveMode.HiddenFromEveryone;
        var lesson = await db.Lessons.SingleAsync(item => item.Id == fixture.DraftLessonId);
        lesson.HomeworkComingSoonOn = null;
        var submission = new HomeworkSubmission
        {
            HomeworkId = homework.Id,
            StudentId = fixture.StudentId,
            Status = SubmissionStatus.Graded,
            OverallScore = 10,
            SubmittedAt = DateTime.UtcNow
        };
        db.AddRange(
            question,
            submission,
            new HomeworkAnswer
            {
                HomeworkSubmissionId = submission.Id,
                QuestionId = question.Id,
                ProvidedAnswer = "Archived answer",
                ScoreReceived = 10
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var academicScope = new AcademicScopeService(db);
        var archiveAccess = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, academicScope, archiveAccess);
        var result = await new GetHomeworkResultQueryHandler(db, access, archiveAccess)
            .Handle(
                new GetHomeworkResultQuery(fixture.HomeworkId, fixture.StudentId),
                CancellationToken.None);
        var detail = await new GetLessonDetailQueryHandler(
                db,
                access,
                new TeacherAuthorizationService(db),
                academicScope,
                archiveAccess)
            .Handle(
                new GetLessonDetailQuery(fixture.FollowingLessonId, fixture.StudentId),
                CancellationToken.None);
        var lessons = await new GetLessonsQueryHandler(db, access, academicScope, archiveAccess)
            .Handle(new GetLessonsQuery(fixture.SectionId, fixture.StudentId), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("CONTENT_ARCHIVED", result.Errors!);
        Assert.True(detail.Success, detail.Message);
        Assert.False(detail.Data!.IsLocked);
        Assert.Null(detail.Data.BlockingHomeworkLessonId);
        Assert.True(lessons.Success, lessons.Message);
        var followingLesson = Assert.Single(
            lessons.Data!,
            item => item.Id == fixture.FollowingLessonId);
        Assert.False(followingLesson.IsLocked);
        Assert.Null(followingLesson.BlockingHomeworkLessonId);
    }

    [Fact]
    public async Task IneligiblePreviousLessonHomework_DoesNotLockEligibleLesson()
    {
        await using var db = TestAppDbContextFactory.Create();
        var fixture = await SeedStudentHomeworkDraftAsync(db);
        var homework = await db.Homeworks.SingleAsync(item => item.Id == fixture.HomeworkId);
        homework.IsActive = true;
        db.HomeworkQuestions.Add(CreateQuestion(homework.Id, "Out-of-scope question"));
        var subjectId = await db.Lessons
            .Where(item => item.Id == fixture.DraftLessonId)
            .Select(item => item.ContentSection.Term.Package.SubjectId)
            .SingleAsync();
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Lesson,
            OwnerId = fixture.DraftLessonId,
            ScopeLevel = AcademicScopeLevel.Exact,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondaryGrade3,
            SubjectId = subjectId
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var academicScope = new AcademicScopeService(db);
        var archiveAccess = new ContentArchiveAccessService(db);
        var access = new AccessCheckService(db, academicScope, archiveAccess);
        var lessons = await new GetLessonsQueryHandler(db, access, academicScope, archiveAccess)
            .Handle(new GetLessonsQuery(fixture.SectionId, fixture.StudentId), CancellationToken.None);

        Assert.True(lessons.Success, lessons.Message);
        Assert.DoesNotContain(lessons.Data!, item => item.Id == fixture.DraftLessonId);
        var followingLesson = Assert.Single(
            lessons.Data!,
            item => item.Id == fixture.FollowingLessonId);
        Assert.False(followingLesson.IsLocked);
        Assert.Null(followingLesson.BlockingHomeworkLessonId);
    }

    private static HomeworkEntity CreateHomework(Guid lessonId, string title, bool isActive) => new()
    {
        LessonId = lessonId,
        Title = title,
        IsActive = isActive,
        TotalScore = 10
    };

    private static HomeworkQuestion CreateQuestion(Guid homeworkId, string text) => new()
    {
        HomeworkId = homeworkId,
        BodyText = text,
        QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.Essay,
        PointsActive = 10
    };

    private static AttachHomeworkCommand CreateAttachCommand(
        Guid lessonId,
        List<AttachHomeworkQuestionDto> questions,
        DateOnly? expectedOn) => new(
        LessonId: lessonId,
        Title: "واجب الحصة",
        Instructions: "تعليمات الواجب",
        IsMandatory: true,
        IsRandomized: false,
        RequiredPointsToPass: 5,
        TotalScore: 10,
        Questions: questions,
        HomeworkComingSoonOn: expectedOn);

    private static async Task<Lesson> SeedLessonAsync(AppDbContext db)
    {
        var (packageId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Homework coming soon package");
        var term = new Term
        {
            Title = "Term",
            PackageId = packageId,
            Order = 1
        };
        var section = new ContentSection
        {
            Title = "Section",
            TermId = term.Id,
            Order = 1
        };
        var lesson = new Lesson
        {
            Title = "Lesson",
            Summary = "Summary",
            ContentSectionId = section.Id,
            Order = 1
        };

        db.AddRange(term, section, lesson);
        await db.SaveChangesAsync();
        return lesson;
    }

    private static async Task<StudentHomeworkDraftFixture> SeedStudentHomeworkDraftAsync(AppDbContext db)
    {
        var student = new User
        {
            FullName = "Homework draft student",
            PhoneNumber = "201000000091",
            PasswordHash = "hashed"
        };
        var profile = new StudentProfile
        {
            User = student,
            UserId = student.Id,
            DateOfBirth = new DateTime(2008, 1, 1),
            Gender = Gender.Male,
            Governorate = "Cairo",
            Address = "Test address",
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        };
        student.StudentProfile = profile;

        var teacherUser = new User
        {
            FullName = "Homework draft teacher",
            PhoneNumber = "201000000092",
            PasswordHash = "hashed"
        };
        var teacher = new TeacherProfile
        {
            User = teacherUser,
            UserId = teacherUser.Id,
            Bio = "Homework regression fixture",
            Specialization = "Secondary",
            ContactInfo = "test@example.invalid",
            IsContentVisibleToStudents = true
        };
        teacherUser.TeacherProfile = teacher;

        var subject = new Subject
        {
            Name = "Homework regression subject",
            NormalizedName = $"HOMEWORK_REGRESSION_{Guid.NewGuid():N}",
            Description = "Homework regression subject"
        };
        var package = new Package
        {
            Name = "Homework regression package",
            Description = "Homework coming-soon behavior",
            Subject = subject,
            SubjectId = subject.Id,
            Teacher = teacher,
            TeacherId = teacher.Id,
            TargetGrade = "FirstSecondary",
            IsActive = true
        };
        var term = new Term
        {
            Package = package,
            PackageId = package.Id,
            Title = "Homework regression term",
            Order = 1
        };
        package.Terms.Add(term);
        var section = new ContentSection
        {
            Term = term,
            TermId = term.Id,
            Title = "Homework regression section",
            Order = 1
        };
        term.Sections.Add(section);

        var expectedOn = CairoTime.GetCurrentDate().AddDays(1);
        var draftLesson = new Lesson
        {
            ContentSection = section,
            ContentSectionId = section.Id,
            Title = "Lesson with draft homework",
            Summary = "Shows only a coming-soon announcement",
            Order = 1,
            HomeworkComingSoonOn = expectedOn
        };
        var followingLesson = new Lesson
        {
            ContentSection = section,
            ContentSectionId = section.Id,
            Title = "Following lesson",
            Summary = "Must remain unlocked",
            Order = 2
        };
        section.Lessons.Add(draftLesson);
        section.Lessons.Add(followingLesson);

        var draft = new HomeworkEntity
        {
            LessonId = draftLesson.Id,
            Title = "Mandatory draft homework",
            IsMandatory = true,
            IsActive = false,
            PassingScoreThreshold = 5,
            TotalScore = 10
        };
        var grant = new StudentAccessGrant
        {
            User = student,
            UserId = student.Id,
            PackageId = package.Id,
            GrantType = CodeType.Package,
            IsActive = true,
            GrantedAt = DateTime.UtcNow
        };
        var scope = new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        };

        db.AddRange(
            student,
            teacherUser,
            subject,
            package,
            term,
            section,
            draftLesson,
            followingLesson,
            draft,
            grant,
            scope);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new StudentHomeworkDraftFixture(
            student.Id,
            section.Id,
            draftLesson.Id,
            followingLesson.Id,
            draft.Id,
            expectedOn);
    }

    private sealed record StudentHomeworkDraftFixture(
        Guid StudentId,
        Guid SectionId,
        Guid DraftLessonId,
        Guid FollowingLessonId,
        Guid HomeworkId,
        DateOnly ExpectedOn);

    private sealed class HomeworkComingSoonNoOpJobEnqueuer : IJobEnqueuer
    {
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data) =>
            Task.CompletedTask;
    }

    private sealed class HomeworkComingSoonNoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            Task.CompletedTask;
    }
}
