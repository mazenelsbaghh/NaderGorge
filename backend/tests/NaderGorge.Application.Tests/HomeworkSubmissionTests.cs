using MediatR;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Webhooks.Commands;
using NaderGorge.Application.Services;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Homework.Commands;
using NaderGorge.Application.Features.Homework.Queries;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Tests;

public sealed class HomeworkSubmissionTests
{
    [Theory]
    [InlineData("/uploads/audio/old.webm")]
    [InlineData("https://example.test/uploads/audio/old.mp3")]
    public async Task AudioHomeworkIsRejectedBeforeAnyAnswersOrGradesAreSaved(string audio)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "audio-disabled-homework");
        var lesson = new Lesson { Title = "Lesson", ContentSectionId = Guid.NewGuid() };
        var homework = new Homework { LessonId = lesson.Id, Title = "Work", TotalScore = 10,
            Questions = [new HomeworkQuestion { BodyText = "Explain", PointsActive = 10,
                QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.Essay }] };
        db.Lessons.Add(lesson); db.Homeworks.Add(homework); await db.SaveChangesAsync();
        await new StartHomeworkAttemptQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id), default);
        var response = await new SubmitHomeworkCommandHandler(db, new HomeworkNoOpPublisher(), new HomeworkAllowAccessService(),
            new HomeworkNoOpJobEnqueuer(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id, [new(homework.Questions.Single().Id, audio)]), default);
        Assert.False(response.Success);
        Assert.Contains("الإجابات الصوتية", response.Message);
        db.ChangeTracker.Clear();
        var submission = await db.HomeworkSubmissions.Include(s => s.Answers).SingleAsync();
        Assert.Equal(SubmissionStatus.InProgress, submission.Status);
        Assert.Null(submission.SubmittedAt);
        Assert.Empty(submission.Answers);
        Assert.Empty(await db.OutboxEvents.ToListAsync());
    }

    [Fact]
    public void ExistingHomeworkJobsKeepFingerprintAndFullCreditPolicyAfterUpgrade()
    {
        // This is the callback identity emitted before homework supported exam credit modes.
        const string legacyDefinition = """
        {"SchemaVersion":1,"Kind":"homework","AssessmentId":"00000000-0000-0000-0000-000000000001","Title":"Legacy","Description":null,"TotalScore":10,"PassingScore":5,"DurationMinutes":null,"IsMandatory":true,"IsRandomized":false,"DisplayQuestionCount":null,"Questions":[],"ReserveQuestions":[],"Revision":null,"CompletionStartedAt":null,"RevisionId":null,"IsActive":true,"UsesAssignedQuestionPoints":false,"EssayGradingMode":0,"ShowResultsWhileEssayPending":false,"SingleAttemptOnly":false,"ParentNotification":{"Enabled":false,"TemplateId":null,"TemplateFingerprint":null,"Parameters":[]}}
        """;
        var submission = new HomeworkSubmission
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), HomeworkId = Guid.Parse("00000000-0000-0000-0000-000000000001"), DefinitionSnapshotJson = legacyDefinition,
            Answers = [new HomeworkAnswer { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), QuestionId = Guid.Parse("00000000-0000-0000-0000-000000000004"), ProvidedAnswer = "Explain" }]
        };
        Assert.Equal("2E227FCEF1DB6EFC5D6D820FB94D867613DE80DF5CBDDECFF1287DB6741ED968", HomeworkEvaluationQueue.Fingerprint(submission));
        var definition = AssessmentDefinitionSnapshot.Read(legacyDefinition, "homework", submission.HomeworkId);
        Assert.Equal(NaderGorge.Domain.Enums.ExamEssayGradingMode.AutomaticFullCredit, definition.EffectiveEssayGradingMode);
        submission.Answers.Single().AwardedScore = 1.5m;
        Assert.NotEqual("2E227FCEF1DB6EFC5D6D820FB94D867613DE80DF5CBDDECFF1287DB6741ED968", HomeworkEvaluationQueue.Fingerprint(submission));
    }

    [Theory]
    [InlineData(NaderGorge.Domain.Enums.ExamEssayGradingMode.Automatic, true, 10, "PendingReview")]
    [InlineData(NaderGorge.Domain.Enums.ExamEssayGradingMode.Manual, false, 10, "PendingReview")]
    [InlineData(NaderGorge.Domain.Enums.ExamEssayGradingMode.ModelAnswerOnly, false, 5, "Graded")]
    public async Task HomeworkGradingModeControlsQueueAndExcludesModelOnlyEssayFromPassScale(
        NaderGorge.Domain.Enums.ExamEssayGradingMode mode, bool queued, decimal maximum, string status)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "201000000090");
        var lesson = new Lesson { Title = "Lesson", ContentSectionId = Guid.NewGuid() };
        var homework = new Homework { LessonId = lesson.Id, Title = "Work", TotalScore = 10,
            PassingScoreThreshold = 5, EssayGradingMode = mode };
        var essay = new HomeworkQuestion { BodyText = "Explain", PointsActive = 5,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.Essay };
        var choice = new HomeworkQuestion { BodyText = "Choose", PointsActive = 5,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ, PossibleAnswers = ["A", "B"], CorrectAnswerKey = "A" };
        homework.Questions = [essay, choice];
        db.Lessons.Add(lesson); db.Homeworks.Add(homework);
        await db.SaveChangesAsync();
        await new StartHomeworkAttemptQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id), default);
        var response = await new SubmitHomeworkCommandHandler(db, new HomeworkNoOpPublisher(), new HomeworkAllowAccessService(),
            new HomeworkNoOpJobEnqueuer(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id, [new(essay.Id, "Explanation"), new(choice.Id, "A")]), default);
        Assert.True(response.Success, response.Message);
        var saved = await db.HomeworkSubmissions.Include(s => s.Answers).SingleAsync();
        Assert.Equal(status, saved.Status.ToString());
        Assert.Equal(maximum, saved.TotalScoreSnapshot);
        Assert.Equal(maximum / 2, saved.PassingScoreSnapshot);
        Assert.Equal(queued, await db.OutboxEvents.AnyAsync(e => e.Type == HomeworkEvaluationQueue.EventType));
        if (mode == NaderGorge.Domain.Enums.ExamEssayGradingMode.ModelAnswerOnly)
        {
            Assert.Equal(5, saved.OverallScore);
            Assert.Equal(0, saved.Answers.Single(a => a.QuestionId == essay.Id).AwardedScore);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedHomeworkCanOnlyStartAgainWhenSingleAttemptIsDisabled(bool singleAttempt)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "201000000089");
        var lesson = new Lesson { Title = "Lesson", ContentSectionId = Guid.NewGuid() };
        var homework = new Homework { LessonId = lesson.Id, Title = "Work", TotalScore = 10,
            PassingScoreThreshold = 5, SingleAttemptOnly = singleAttempt };
        var choice = new HomeworkQuestion { BodyText = "Choose", PointsActive = 10, PossibleAnswers = ["A", "B"], CorrectAnswerKey = "A" };
        homework.Questions = [choice]; db.Lessons.Add(lesson); db.Homeworks.Add(homework);
        await db.SaveChangesAsync();
        var starter = new StartHomeworkAttemptQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService());
        var first = await starter.Handle(new(homework.Id, student.Id), default);
        var response = await new SubmitHomeworkCommandHandler(db, new HomeworkNoOpPublisher(), new HomeworkAllowAccessService(),
            new HomeworkNoOpJobEnqueuer(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id, [new(choice.Id, "B")]), default);
        Assert.True(response.Success, response.Message);
        var second = await starter.Handle(new(homework.Id, student.Id), default);
        Assert.True(second.Success, second.Message);
        Assert.Equal(singleAttempt, second.Data!.AlreadyCompleted);
        Assert.Equal(singleAttempt, first.Data!.SubmissionId == second.Data.SubmissionId);
        if (singleAttempt)
        {
            var resubmitted = await new SubmitHomeworkCommandHandler(db, new HomeworkNoOpPublisher(), new HomeworkAllowAccessService(),
                new HomeworkNoOpJobEnqueuer(), new HomeworkAllowArchiveAccessService())
                .Handle(new(homework.Id, student.Id, [new(choice.Id, "A")]), default);
            Assert.False(resubmitted.Success);
            Assert.Equal("B", (await db.HomeworkAnswers.SingleAsync()).ProvidedAnswer);
        }
    }

    [Theory]
    [InlineData("explanation", false, true)]
    [InlineData("", false, true)]
    [InlineData("explanation", true, false)]
    public async Task SubmittedWrittenHomeworkDurablyQueuesAIUnlessExpired(string answer, bool expired, bool queued)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "201000000093");
        var lesson = new Lesson { Title = "Lesson", ContentSectionId = Guid.NewGuid() };
        var homework = new Homework { LessonId = lesson.Id, Title = "Work", TotalScore = 10, DurationMinutes = 5 };
        var question = new HomeworkQuestion { HomeworkId = homework.Id, BodyText = "Explain", PointsActive = 1,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.Essay };
        homework.Questions.Add(question);
        db.Lessons.Add(lesson); db.Homeworks.Add(homework);
        await db.SaveChangesAsync();
        await new StartHomeworkAttemptQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id), default);
        if (expired) { (await db.HomeworkSubmissions.SingleAsync()).StartedAt = DateTime.UtcNow.AddHours(-1); await db.SaveChangesAsync(); }
        var submitted = await new SubmitHomeworkCommandHandler(db, new HomeworkNoOpPublisher(), new HomeworkAllowAccessService(),
            new HomeworkNoOpJobEnqueuer(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id, [new(question.Id, answer)]), default);
        Assert.True(submitted.Success, submitted.Message);
        var events = await db.OutboxEvents.Where(e => e.Type == "HomeworkEvaluationQueued").ToListAsync();
        Assert.Equal(queued ? 1 : 0, events.Count);
        if (queued)
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<NaderGorge.Application.Features.Assessments.HomeworkEvaluationPayload>(events[0].PayloadJson)!;
            db.ChangeTracker.Clear();
            var saved = await db.HomeworkSubmissions.Include(s => s.Answers).SingleAsync();
            Assert.Equal(NaderGorge.Application.Features.Assessments.HomeworkEvaluationQueue.Fingerprint(saved), payload.Fingerprint);
            Assert.Equal(Assert.Single(saved.Answers).Id, Assert.Single(payload.Questions).AnswerId);
        }
    }

    [Theory]
    [InlineData(5, 60, 0)]
    [InlineData(null, 5, 10)]
    public async Task HomeworkTimerUsesAttemptDefinitionNotLaterSettings(int? originalMinutes, int currentMinutes, decimal expectedScore)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Timer student", "201000000092");
        var lesson = new Lesson { Title = "Timer lesson", ContentSectionId = Guid.NewGuid(), Order = 1 };
        var homework = new Homework { LessonId = lesson.Id, Title = "Timed work", TotalScore = 10, DurationMinutes = originalMinutes };
        var question = new HomeworkQuestion { HomeworkId = homework.Id, BodyText = "Choose", PointsActive = 1,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ, PossibleAnswers = ["A", "B"], CorrectAnswerKey = "A" };
        db.Lessons.Add(lesson); db.Homeworks.Add(homework); db.HomeworkQuestions.Add(question);
        await db.SaveChangesAsync();
        var start = new StartHomeworkAttemptQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService());
        var first = await start.Handle(new(homework.Id, student.Id), default);
        Assert.True(first.Success, first.Message);
        var submission = await db.HomeworkSubmissions.SingleAsync();
        submission.StartedAt = DateTime.UtcNow.AddMinutes(-10);
        homework.DurationMinutes = currentMinutes;
        await db.SaveChangesAsync();
        var resumed = await start.Handle(new(homework.Id, student.Id), default);
        Assert.Equal(originalMinutes, resumed.Data!.DurationMinutes);
        Assert.Equal(originalMinutes.HasValue ? 0 : (int?)null, resumed.Data.RemainingSeconds);
        var submitted = await new SubmitHomeworkCommandHandler(db, new HomeworkNoOpPublisher(), new HomeworkAllowAccessService(),
            new HomeworkNoOpJobEnqueuer(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id, [new(question.Id, "A")]), default);
        Assert.True(submitted.Success, submitted.Message);
        Assert.Equal(expectedScore, submission.OverallScore);
        Assert.Equal("A", (await db.HomeworkAnswers.SingleAsync()).ProvidedAnswer);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(null, false)]
    public async Task StartingNextHomeworkUsesPreviousAttemptPassingThreshold(int? savedPassingScore, bool allowed)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Snapshot student", "201000000091");
        var sectionId = Guid.NewGuid();
        var previousLesson = new Lesson { Title = "Previous", ContentSectionId = sectionId, Order = 1 };
        var nextLesson = new Lesson { Title = "Next", ContentSectionId = sectionId, Order = 2 };
        var previous = new Homework { LessonId = previousLesson.Id, Title = "Previous work", TotalScore = 10, PassingScoreThreshold = 9 };
        var next = new Homework { LessonId = nextLesson.Id, Title = "Next work", TotalScore = 10 };
        db.Lessons.AddRange(previousLesson, nextLesson);
        db.Homeworks.AddRange(previous, next);
        db.HomeworkQuestions.AddRange(
            new HomeworkQuestion { HomeworkId = previous.Id, BodyText = "Previous question", PointsActive = 10 },
            new HomeworkQuestion { HomeworkId = next.Id, BodyText = "Next question", PointsActive = 10 });
        db.HomeworkSubmissions.Add(new HomeworkSubmission
        {
            HomeworkId = previous.Id, StudentId = student.Id, Status = SubmissionStatus.Graded,
            OverallScore = 6, PassingScoreSnapshot = savedPassingScore, SubmittedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var response = await new StartHomeworkAttemptQueryHandler(db,
                new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(next.Id, student.Id), CancellationToken.None);

        Assert.Equal(allowed, response.Success);
        Assert.Equal(allowed, await db.HomeworkSubmissions.AnyAsync(s => s.HomeworkId == next.Id));
    }

    [Fact]
    public async Task SubmitHomework_PersistsAnswersForSubmission()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "201000000001");
        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            Title = "Lesson",
            Summary = "Summary",
            ContentSectionId = Guid.NewGuid(),
            Order = 1
        };
        var homework = new Homework
        {
            Id = Guid.NewGuid(),
            LessonId = lesson.Id,
            Title = "Homework",
            TotalScore = 10,
            PassingScoreThreshold = 5
        };
        var firstQuestion = new HomeworkQuestion
        {
            Id = Guid.NewGuid(),
            HomeworkId = homework.Id,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ,
            BodyText = "First",
            CorrectAnswerKey = "A",
            PointsActive = 1,
            AudioUrl = "/uploads/audio/homework-correction.mp3",
            WrittenCorrection = "التصحيح المسجل للسؤال"
        };
        var secondQuestion = new HomeworkQuestion
        {
            Id = Guid.NewGuid(),
            HomeworkId = homework.Id,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ,
            BodyText = "Second",
            CorrectAnswerKey = "B",
            PointsActive = 1
        };
        db.Lessons.Add(lesson);
        db.Homeworks.Add(homework);
        db.HomeworkQuestions.AddRange(firstQuestion, secondQuestion);
        await db.SaveChangesAsync();

        var handler = new SubmitHomeworkCommandHandler(
            db,
            new HomeworkNoOpPublisher(),
            new HomeworkAllowAccessService(),
            new HomeworkNoOpJobEnqueuer(),
            new HomeworkAllowArchiveAccessService());

        var response = await handler.Handle(
            new SubmitHomeworkCommand(
                homework.Id,
                student.Id,
                [
                    new StudentAnswerInput(firstQuestion.Id, "A"),
                    new StudentAnswerInput(secondQuestion.Id, "B")
                ]),
            CancellationToken.None);

        Assert.True(response.Success);
        var submission = await db.HomeworkSubmissions.SingleAsync(s => s.HomeworkId == homework.Id && s.StudentId == student.Id);
        var persistedAnswers = await db.HomeworkAnswers
            .Where(a => a.HomeworkSubmissionId == submission.Id)
            .OrderBy(a => a.QuestionId)
            .ToListAsync();

        Assert.Equal(SubmissionStatus.Graded, submission.Status);
        Assert.Equal(2, persistedAnswers.Count);
        Assert.All(persistedAnswers, answer => Assert.Equal(1, answer.ScoreReceived));
        Assert.Contains(persistedAnswers, answer => answer.QuestionId == firstQuestion.Id && answer.ProvidedAnswer == "A");
        Assert.Contains(persistedAnswers, answer => answer.QuestionId == secondQuestion.Id && answer.ProvidedAnswer == "B");

        var resultHandler = new GetHomeworkResultQueryHandler(
            db,
            new HomeworkAllowAccessService(),
            new HomeworkAllowArchiveAccessService());
        var result = await resultHandler.Handle(
            new GetHomeworkResultQuery(homework.Id, student.Id),
            CancellationToken.None);

        Assert.True(result.Success);
        var questionReview = result.Data!.QuestionReviews.Single(q => q.QuestionId == firstQuestion.Id);
        Assert.Equal("/uploads/audio/homework-correction.mp3", questionReview.AudioUrl);
        Assert.Equal("التصحيح المسجل للسؤال", questionReview.WrittenCorrection);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task PendingHomeworkSwitchAppliesToSavedAttemptUntilFinalGrading(bool enabled, bool manual)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Pending student", "pending-homework");
        var lesson = new Lesson { Title = "Lesson", ContentSectionId = Guid.NewGuid() };
        var homework = new Homework { LessonId = lesson.Id, Title = "Work", TotalScore = 20,
            PassingScoreThreshold = 10, ShowResultsWhileEssayPending = !enabled };
        var mcq = new HomeworkQuestion { HomeworkId = homework.Id, BodyText = "Choose A", PointsActive = 2,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.MCQ, PossibleAnswers = ["A", "B"], CorrectAnswerKey = "A" };
        var essay = new HomeworkQuestion { HomeworkId = homework.Id, BodyText = "Explain", PointsActive = 8,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.Essay, WrittenCorrection = "Model explanation" };
        homework.Questions = [mcq, essay];
        db.Lessons.Add(lesson); db.Homeworks.Add(homework);
        await db.SaveChangesAsync();
        await new StartHomeworkAttemptQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id), default);
        var submitted = await new SubmitHomeworkCommandHandler(db, new HomeworkNoOpPublisher(), new HomeworkAllowAccessService(),
            new HomeworkNoOpJobEnqueuer(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id, [new(mcq.Id, "A"), new(essay.Id, "Saved explanation")]), default);
        Assert.True(submitted.Success, submitted.Message);
        var submission = await db.HomeworkSubmissions.Include(s => s.Answers).SingleAsync();
        var fingerprint = HomeworkEvaluationQueue.Fingerprint(submission);
        homework.ShowResultsWhileEssayPending = enabled;
        // Live visibility must not change the attempt's authored scale or queued evaluation.
        homework.TotalScore = 99;
        essay.WrittenCorrection = "Changed model";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var pending = await new GetHomeworkResultQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id), default);
        Assert.True(pending.Success, pending.Message);
        Assert.Equal("PendingReview", pending.Data!.Status);
        Assert.False(pending.Data.IsPassed);
        Assert.Equal(20, pending.Data.TotalScore);
        if (enabled)
        {
            Assert.Equal(4, pending.Data.PartialResult!.ScoreAchieved);
            Assert.Equal(4, pending.Data.PartialResult.TotalScore);
            Assert.Equal(1, pending.Data.PartialResult.PendingQuestionCount);
            var pendingEssay = Assert.Single(pending.Data.QuestionReviews, q => q.QuestionId == essay.Id);
            Assert.Null(pendingEssay.ScoreReceived);
            Assert.Null(pendingEssay.IsCorrect);
            Assert.Equal("Model explanation", pendingEssay.CorrectAnswer);
        }
        else
        {
            Assert.Null(pending.Data.PartialResult);
            Assert.Empty(pending.Data.QuestionReviews);
        }
        submission = await db.HomeworkSubmissions.Include(s => s.Answers).SingleAsync();
        Assert.Equal(fingerprint, HomeworkEvaluationQueue.Fingerprint(submission));
        Assert.Null(submission.Answers.Single(a => a.QuestionId == essay.Id).ScoreReceived);
        if (manual)
        {
            var reviewer = await TestAppDbContextFactory.SeedUserAsync(db, "Reviewer", "reviewer-homework");
            var adminRole = new Role { Name = "Admin", Type = NaderGorge.Domain.Enums.RoleType.Admin };
            db.Roles.Add(adminRole);
            db.UserRoles.Add(new UserRole { UserId = reviewer.Id, RoleId = adminRole.Id });
            await db.SaveChangesAsync();
            var graded = await new GradeAssessmentCommandHandler(db, new TeacherAuthorizationService(db))
                .Handle(new(new(AssessmentKind.Homework, homework.Id, submission.Id, reviewer.Id),
                    [new(mcq.Id, 2), new(essay.Id, 4)], "Reviewed"), default);
            Assert.True(graded.Success, graded.Message);
        }
        else
        {
            var answer = submission.Answers.Single(a => a.QuestionId == essay.Id);
            var graded = await new WebhookHomeworkGradedCommandHandler(db)
                .Handle(new(submission.Id, fingerprint, [new(answer.Id, 1, "Reviewed")]), default);
            Assert.True(graded.Success, graded.Message);
        }
        var final = await new GetHomeworkResultQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id), default);
        Assert.Equal("Graded", final.Data!.Status);
        Assert.Null(final.Data.PartialResult);
        Assert.Equal(manual ? 12 : 20, final.Data.Score);
        Assert.Equal(20, final.Data.TotalScore);
        Assert.True(final.Data.IsPassed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingHomeworkIncludesUnansweredChoiceInPartialMaximumButNeverEssay(bool essayOnly)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Partial student", "partial-homework");
        var homework = new Homework { LessonId = Guid.NewGuid(), Title = "Work", TotalScore = 10, ShowResultsWhileEssayPending = true };
        var essay = new HomeworkQuestion { HomeworkId = homework.Id, BodyText = "Explain", PointsActive = 8,
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.Essay };
        homework.Questions.Add(essay);
        if (!essayOnly) homework.Questions.Add(new HomeworkQuestion { HomeworkId = homework.Id, BodyText = "Choose", PointsActive = 2 });
        db.Homeworks.Add(homework);
        db.HomeworkSubmissions.Add(new HomeworkSubmission { HomeworkId = homework.Id, StudentId = student.Id,
            Status = SubmissionStatus.PendingReview, SubmittedAt = DateTime.UtcNow,
            Answers = [new HomeworkAnswer { QuestionId = essay.Id, ProvidedAnswer = "Answer" }] });
        await db.SaveChangesAsync();
        var response = await new GetHomeworkResultQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id), default);
        Assert.True(response.Success, response.Message);
        Assert.Equal(0, response.Data!.PartialResult!.ScoreAchieved);
        Assert.Equal(essayOnly ? 0 : 2, response.Data.PartialResult.TotalScore);
        Assert.Equal(1, response.Data.PartialResult.PendingQuestionCount);
        Assert.Equal(essayOnly ? 0 : 1, response.Data.WrongAnswers);
    }

    [Fact]
    public async Task HomeworkSwitchDoesNotRevealModelBeforeSubmission()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "unsubmitted-homework");
        var homework = new Homework { LessonId = Guid.NewGuid(), Title = "Work", ShowResultsWhileEssayPending = true,
            Questions = [new HomeworkQuestion { BodyText = "Explain", WrittenCorrection = "Secret model" }] };
        db.Homeworks.Add(homework);
        db.HomeworkSubmissions.Add(new HomeworkSubmission { HomeworkId = homework.Id, StudentId = student.Id, Status = SubmissionStatus.InProgress });
        await db.SaveChangesAsync();
        var response = await new GetHomeworkResultQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(homework.Id, student.Id), default);
        Assert.False(response.Success);
        Assert.Null(response.Data);
    }

    [Theory]
    [InlineData(true, SubmissionStatus.PendingReview, true)]
    [InlineData(false, SubmissionStatus.PendingReview, false)]
    [InlineData(true, SubmissionStatus.InProgress, false)]
    [InlineData(true, SubmissionStatus.Graded, false)]
    public async Task NextHomeworkCanStartAndSubmitWhilePreviousEssayPendingOnlyWhenEnabled(
        bool enabled, SubmissionStatus status, bool allowed)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "next-homework");
        var section = Guid.NewGuid();
        var previousLesson = new Lesson { ContentSectionId = section, Title = "Previous", Order = 1 };
        var nextLesson = new Lesson { ContentSectionId = section, Title = "Next", Order = 2 };
        var previous = new Homework { LessonId = previousLesson.Id, Title = "Previous", TotalScore = 10,
            PassingScoreThreshold = 9, ShowResultsWhileEssayPending = enabled,
            Questions = [new HomeworkQuestion { BodyText = "Explain", PointsActive = 10, QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.Essay }] };
        var next = new Homework { LessonId = nextLesson.Id, Title = "Next", TotalScore = 1,
            Questions = [new HomeworkQuestion { BodyText = "Choose", PointsActive = 1, CorrectAnswerKey = "A" }] };
        db.Lessons.AddRange(previousLesson, nextLesson); db.Homeworks.AddRange(previous, next);
        db.HomeworkSubmissions.Add(new HomeworkSubmission { HomeworkId = previous.Id, StudentId = student.Id,
            Status = status, OverallScore = 0, SubmittedAt = status == SubmissionStatus.InProgress ? null : DateTime.UtcNow });
        await db.SaveChangesAsync();
        var started = await new StartHomeworkAttemptQueryHandler(db, new HomeworkAllowAccessService(), new HomeworkAllowArchiveAccessService())
            .Handle(new(next.Id, student.Id), default);
        var submitted = await new SubmitHomeworkCommandHandler(db, new HomeworkNoOpPublisher(), new HomeworkAllowAccessService(),
            new HomeworkNoOpJobEnqueuer(), new HomeworkAllowArchiveAccessService())
            .Handle(new(next.Id, student.Id, [new(next.Questions.Single().Id, "A")]), default);
        Assert.Equal(allowed, started.Success);
        Assert.Equal(allowed, submitted.Success);
    }

    private sealed class HomeworkAllowAccessService : IAccessCheckService
    {
        public Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToVideoAsync(Guid userId, Guid lessonVideoId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class HomeworkAllowArchiveAccessService : IContentArchiveAccessService
    {
        public Task<bool> CanViewAsync(Guid userId, NaderGorge.Domain.Enums.ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlySet<Guid>> GetViewableLessonIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(lessonIds.ToHashSet());
        public Task<IReadOnlySet<Guid>> GetViewableLessonVideoIdsAsync(Guid userId, IReadOnlyCollection<Guid> lessonVideoIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(lessonVideoIds.ToHashSet());
        public Task<bool> CanAcquireAsync(NaderGorge.Domain.Enums.ContentArchiveTargetType targetType, Guid targetId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class HomeworkNoOpJobEnqueuer : IJobEnqueuer
    {
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data) => Task.CompletedTask;
    }

    private sealed class HomeworkNoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
