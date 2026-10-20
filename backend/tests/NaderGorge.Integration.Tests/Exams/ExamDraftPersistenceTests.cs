using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Exams.Queries;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.Exams;

public sealed class ExamDraftPersistenceTests
{
    [Fact]
    public async Task DraftSurvivesNewConnection_WithoutSubmittingOrChangingGrades_AndSubmitUsesConfirmedCopy()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var (scope, answers) = await Seed(fixture, essay: true);
        int outboxBefore;
        await using (var db = fixture.CreateDbContext())
        {
            outboxBefore = await db.OutboxEvents.CountAsync();
            var saved = await new SaveExamDraftCommandHandler(db).Handle(new(scope, new(0, Guid.NewGuid(), answers)), default);
            Assert.True(saved.Success, saved.Message);
        }
        await using var reopened = fixture.CreateDbContext();
        var restored = await new GetExamDraftQueryHandler(reopened).Handle(new(scope), default);
        Assert.True(restored.Success);
        Assert.Equal(answers.OrderBy(a => a.Key), restored.Data!.Answers.OrderBy(a => a.Key));
        var attempt = await reopened.StudentExamAttempts.SingleAsync();
        Assert.Null(attempt.SubmittedAt);
        Assert.Null(attempt.Evaluation);
        Assert.Equal(0m, attempt.ScoreAchieved);
        Assert.All(await reopened.StudentAnswers.ToListAsync(), a => { Assert.Null(a.SelectedOptionId); Assert.Null(a.SubmittedText); Assert.Equal(0m, a.PointsAwarded); });
        Assert.Empty(await reopened.EssaySubmissions.ToListAsync());
        Assert.Equal(outboxBefore, await reopened.OutboxEvents.CountAsync());
        Assert.False((await new GetExamAttemptResultQueryHandler(reopened).Handle(new(scope.AttemptId, scope.UserId), default)).Success);

        var command = new SubmitExamCommand(scope.ExamId, scope.AttemptId, scope.UserId, [], DraftVersion: 1);
        var handler = new SubmitExamCommandHandler(reopened, new Effects(), new Effects());
        var stale = await handler.Handle(command with { DraftVersion = 0 }, default);
        Assert.False(stale.Success);
        Assert.Contains("EXAM_DRAFT_CONFLICT", stale.Errors!);
        var submitted = await handler.Handle(command, default);
        Assert.True(submitted.Success, submitted.Message);
        Assert.Equal(1m, submitted.Data!.ScoreAchieved);
        reopened.ChangeTracker.Clear();
        Assert.Equal(answers.Values.Single(a => a.Length > 100), (await reopened.EssaySubmissions.SingleAsync()).AnswerText);
        Assert.NotNull((await reopened.StudentExamAttempts.SingleAsync()).SubmittedAt);
        var before = (await reopened.StudentAnswers.OrderBy(a => a.Id).ToListAsync()).Select(a => (a.Id, a.PointsAwarded, a.SubmittedText)).ToArray();
        var late = await new SaveExamDraftCommandHandler(reopened).Handle(new(scope, new(1, Guid.NewGuid(), [])), default);
        Assert.False(late.Success);
        Assert.Contains("EXAM_DRAFT_CLOSED", late.Errors!);
        var duplicate = await handler.Handle(command with { DraftVersion = 999 }, default);
        Assert.True(duplicate.Success);
        Assert.Equal(submitted.Data.ScoreAchieved, duplicate.Data!.ScoreAchieved);
        reopened.ChangeTracker.Clear();
        Assert.Equal(before, (await reopened.StudentAnswers.OrderBy(a => a.Id).ToListAsync()).Select(a => (a.Id, a.PointsAwarded, a.SubmittedText)).ToArray());
    }

    [Fact]
    public async Task TwoTabsCannotOverwriteEachOther_AndLostAcknowledgementCanBeRetried()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var (scope, answers) = await Seed(fixture);
        var requests = new[] { new SaveExamDraftRequest(0, Guid.NewGuid(), answers), new SaveExamDraftRequest(0, Guid.NewGuid(), []) };
        var results = await Task.WhenAll(requests.Select(async request =>
        {
            await using var db = fixture.CreateDbContext();
            return await new SaveExamDraftCommandHandler(db).Handle(new(scope, request), default);
        }));
        var winner = Array.FindIndex(results, r => r.Success);
        Assert.Single(results, r => r.Success);
        Assert.Contains("EXAM_DRAFT_CONFLICT", results.Single(r => !r.Success).Errors!);
        await using var retryDb = fixture.CreateDbContext();
        var replay = await new SaveExamDraftCommandHandler(retryDb).Handle(new(scope, requests[winner]), default);
        Assert.True(replay.Success);
        Assert.Equal(1, replay.Data!.Version);
        Assert.Equal(requests[winner].Answers.OrderBy(a => a.Key), replay.Data.Answers.OrderBy(a => a.Key));
        Assert.Equal(1, await retryDb.StudentExamDrafts.CountAsync());
        Assert.Null((await retryDb.StudentExamAttempts.SingleAsync()).Evaluation);
    }

    [Fact]
    public async Task SaveRacingSubmit_EitherPreservesSubmittedCopyOrRejectsStaleSubmission()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var (scope, answers) = await Seed(fixture);
        await using (var db = fixture.CreateDbContext())
            Assert.True((await new SaveExamDraftCommandHandler(db).Handle(new(scope,
                new(0, Guid.NewGuid(), new() { [answers.Keys.First()] = answers.Values.First() })), default)).Success);
        await using var savingDb = fixture.CreateDbContext();
        await using var submittingDb = fixture.CreateDbContext();
        var saving = new SaveExamDraftCommandHandler(savingDb).Handle(new(scope, new(1, Guid.NewGuid(), answers)), default);
        var command = new SubmitExamCommand(scope.ExamId, scope.AttemptId, scope.UserId, [], DraftVersion: 1);
        var submitting = new SubmitExamCommandHandler(submittingDb, new Effects(), new Effects()).Handle(command, default);
        await Task.WhenAll(saving, submitting);
        var saved = await saving;
        var submitted = await submitting;
        Assert.NotEqual(saved.Success, submitted.Success);
        if (saved.Success)
        {
            Assert.Contains("EXAM_DRAFT_CONFLICT", submitted.Errors!);
            Assert.Null((await submittingDb.StudentExamAttempts.AsNoTracking().SingleAsync()).SubmittedAt);
            var latest = await new SubmitExamCommandHandler(submittingDb, new Effects(), new Effects()).Handle(command with { DraftVersion = 2 }, default);
            Assert.True(latest.Success, latest.Message);
            Assert.Equal(2m, latest.Data!.ScoreAchieved);
        }
        else
        {
            Assert.Contains("EXAM_DRAFT_CLOSED", saved.Errors!);
            Assert.Equal(1m, submitted.Data!.ScoreAchieved);
        }
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("question")]
    [InlineData("option")]
    [InlineData("expired")]
    [InlineData("revision")]
    public async Task InvalidDraftCannotWriteOrAffectResults(string invalid)
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var (scope, answers) = await Seed(fixture);
        await using var db = fixture.CreateDbContext();
        if (invalid == "owner") scope = scope with { UserId = Guid.NewGuid() };
        if (invalid == "revision") scope = scope with { RevisionId = Guid.NewGuid() };
        if (invalid == "question") answers = new() { [Guid.NewGuid()] = "invalid" };
        if (invalid == "option") answers[answers.Keys.First()] = Guid.NewGuid().ToString();
        if (invalid == "expired")
        {
            var attempt = await db.StudentExamAttempts.SingleAsync();
            attempt.StartedAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }
        var outboxBefore = await db.OutboxEvents.CountAsync();
        var result = await new SaveExamDraftCommandHandler(db).Handle(new(scope, new(0, Guid.NewGuid(), answers)), default);
        Assert.False(result.Success);
        Assert.Empty(await db.StudentExamDrafts.ToListAsync());
        Assert.Null((await db.StudentExamAttempts.SingleAsync()).SubmittedAt);
        Assert.Equal(outboxBefore, await db.OutboxEvents.CountAsync());
    }

    [Fact]
    public async Task RevisionDraftEditsOnlyAddedQuestions_PreservingExistingGrades()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        var (scope, answers) = await Seed(fixture);
        await using var db = fixture.CreateDbContext();
        var attempt = await db.StudentExamAttempts.Include(a => a.Answers).SingleAsync();
        var definition = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", scope.ExamId);
        var kept = definition.Questions[0];
        var added = definition.Questions[1];
        var original = attempt.Answers.Single(a => a.ExamQuestionId == kept.Id);
        original.SelectedOptionId = Guid.Parse(answers[kept.Id]);
        original.SubmittedText = "Correct";
        original.PointsAwarded = 1;
        original.IsCorrect = true;
        scope = scope with { RevisionId = Guid.NewGuid() };
        attempt.DefinitionSnapshotJson = (definition with { RevisionId = scope.RevisionId, CompletionStartedAt = DateTime.UtcNow,
            Revision = new AttemptRevisionPlan([new(kept.Id, 1, 1, false), new(added.Id, 1, null, false, RequiresCompletion: true)]) }).ToJson();
        await db.SaveChangesAsync();
        var handler = new SaveExamDraftCommandHandler(db);
        Assert.False((await handler.Handle(new(scope, new(0, Guid.NewGuid(), answers)), default)).Success);
        var saved = await handler.Handle(new(scope, new(0, Guid.NewGuid(), new() { [added.Id] = answers[added.Id] })), default);
        Assert.True(saved.Success, saved.Message);
        var result = await new SubmitExamCommandHandler(db, new Effects(), new Effects()).Handle(
            new(scope.ExamId, scope.AttemptId, scope.UserId, [], scope.RevisionId, 1), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(2m, result.Data!.ScoreAchieved);
        db.ChangeTracker.Clear();
        original = await db.StudentAnswers.SingleAsync(a => a.ExamQuestionId == kept.Id);
        Assert.Equal(1m, original.PointsAwarded);
        Assert.Equal("Correct", original.SubmittedText);
    }

    private static async Task<(ExamDraftScope, Dictionary<Guid, string>)> Seed(PostgresAdminAIFixture fixture, bool essay = false)
    {
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var student = new User { FullName = "Draft student", PhoneNumber = "01000000781", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = new User { FullName = "Draft teacher", PhoneNumber = "01000000782", PasswordHash = "test" } };
        var subject = new Subject { Name = "Draft history", NormalizedName = "DRAFT_HISTORY" };
        var exam = new Exam { Title = "Draft exam", TotalScore = 2, PassingScore = 1, DurationMinutes = 20,
            CreatedByTeacher = teacher, EssayGradingMode = ExamEssayGradingMode.Manual };
        var attempt = new StudentExamAttempt { User = student, Exam = exam, StartedAt = DateTime.UtcNow };
        var answers = new Dictionary<Guid, string>();
        for (var index = 0; index < 2; index++)
        {
            var isEssay = essay && index == 1;
            var question = new QuestionBankItem { Text = "سؤال حفظ الإجابة", Type = isEssay ? QuestionType.Essay : QuestionType.MCQ, Subject = subject, CreatedByTeacher = teacher };
            var correct = new QuestionOption { Question = question, Text = "Correct", IsCorrect = true };
            if (!isEssay) question.Options.Add(correct);
            var assigned = new ExamQuestion { Exam = exam, Question = question, Points = 1, Order = index + 1 };
            exam.ExamQuestions.Add(assigned);
            db.StudentAnswers.Add(new StudentAnswer { Attempt = attempt, ExamQuestion = assigned });
            answers[assigned.Id] = isEssay ? string.Concat(Enumerable.Repeat("شرح تاريخي كامل للحفظ دون حذف. ", 100)).Trim() : correct.Id.ToString();
        }
        attempt.DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam).ToJson();
        await db.SaveChangesAsync();
        return (new(exam.Id, attempt.Id, student.Id, null), answers);
    }

    private sealed class Effects : IPublisher, IJobEnqueuer
    {
        public Task EnqueueJobAsync<T>(string queueName, string jobName, T data) => Task.CompletedTask;
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<T>(T notification, CancellationToken cancellationToken = default) where T : INotification => Task.CompletedTask;
    }
}
