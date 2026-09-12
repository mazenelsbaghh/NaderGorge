using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Homework.Commands;
using NaderGorge.Application.Features.Homework.Queries;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Exams.Queries;
using NaderGorge.Application.Features.Webhooks.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using HomeworkQuestionType = NaderGorge.Domain.Entities.Homework.QuestionType;
using ExamQuestionType = NaderGorge.Domain.Entities.QuestionType;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class AssessmentReviewPostgresTests
{
    [Theory]
    [InlineData(ExamQuestionType.MCQ, false, 20)]
    [InlineData(ExamQuestionType.MCQ, true, 10)]
    [InlineData(ExamQuestionType.Essay, false, 10)]
    public async Task ExamCompletionKeepsOldAnswerAndGradesOnlyAddedQuestion(ExamQuestionType addedType, bool expired, decimal expectedScore)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var bank = new QuestionBankItem
        {
            Text = "Original MCQ", CreatedByTeacherId = seed.Teacher.Id,
            Subject = new Subject { Name = "Completion", NormalizedName = Guid.NewGuid().ToString("N") },
            Options = [new() { Text = "A", IsCorrect = true }, new() { Text = "B" }]
        };
        var exam = new Exam { Title = "Completion exam", TotalScore = 20, PassingScore = 12,
            DurationMinutes = 10, CreatedByTeacherId = seed.Teacher.Id };
        var question = new ExamQuestion { Question = bank, Points = 4 };
        exam.ExamQuestions.Add(question);
        var attempt = new StudentExamAttempt { Exam = exam, UserId = seed.Submission.StudentId,
            Evaluation = "ناجح", ScoreAchieved = 20, IsPassed = true };
        attempt.Answers.Add(new StudentAnswer { ExamQuestion = question,
            SelectedOption = bank.Options.Single(o => o.IsCorrect), SubmittedText = "A", PointsAwarded = 4, IsCorrect = true });
        fixture.Db.StudentExamAttempts.Add(attempt);
        var lesson = await fixture.Db.Lessons.SingleAsync(l => l.Id == seed.Homework.LessonId);
        lesson.ExamId = exam.Id;
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Exam, exam.Id, Guid.Empty, seed.Teacher.UserId);
        var original = AssessmentDefinitionSnapshot.FromExam(exam);
        var added = original.Questions.Single() with { Id = Guid.NewGuid(), BankQuestionId = Guid.NewGuid(),
            Text = "Added question", Type = (int)addedType, WrittenCorrection = "Model answer",
            Options = addedType == ExamQuestionType.MCQ ? [new(Guid.NewGuid(), "C", true), new(Guid.NewGuid(), "D", false)] : [] };
        var definition = original with { Questions = [original.Questions[0], added] };
        var policy = new AssessmentRevisionPolicy(PreviousAttemptsPolicy.Regrade, AddedQuestions: AddedQuestionPolicy.RequestCompletion);
        var handler = new AssessmentRevisionCommandHandler(fixture.Db, new(fixture.Db));
        var preview = await handler.Handle(new PreviewAssessmentRevisionQuery(target, definition, policy), default);
        Assert.True(preview.Success, preview.Message);
        var saved = await handler.Handle(new SaveAssessmentRevisionCommand(target, definition, policy,
            preview.Data!.RevisionToken, Guid.NewGuid(), ConfirmPreviousAttempts: true), default);
        Assert.True(saved.Success, saved.Message);
        var started = await new StartExamAttemptCommandHandler(fixture.Db, new AccessCheckService(fixture.Db))
            .Handle(new(exam.Id, attempt.UserId), default);
        Assert.True(started.Success, started.Message);
        Assert.Equal(added.Id, Assert.Single(started.Data!.Questions).Id);
        Assert.Equal("RequiresCompletion", (await new GetExamAttemptGradingStatusQueryHandler(fixture.Db)
            .Handle(new(attempt.Id, attempt.UserId), default)).Data!.ResultState);
        Assert.False((await new GetExamAttemptResultQueryHandler(fixture.Db).Handle(new(attempt.Id, attempt.UserId), default)).Success);
        if (expired)
        {
            var running = await fixture.Db.StudentExamAttempts.SingleAsync(a => a.Id == attempt.Id);
            var snapshot = AssessmentDefinitionSnapshot.Read(running.DefinitionSnapshotJson!, "exam", exam.Id);
            running.DefinitionSnapshotJson = (snapshot with { CompletionStartedAt = DateTime.UtcNow.AddHours(-1) }).ToJson();
            await fixture.Db.SaveChangesAsync();
        }
        var completion = new ExamRevisionCompletion(fixture.Db);
        var command = new SubmitExamCommand(exam.Id, attempt.Id, attempt.UserId,
            [new(added.Id, added.Options.FirstOrDefault()?.Id, addedType == ExamQuestionType.Essay ? "شرح الطالب" : null)], started.Data.RevisionId);
        Assert.False((await completion.Submit(command with { RevisionId = Guid.NewGuid() }, default)).Success);
        Assert.False((await completion.Submit(command with { Answers = [new(question.Id, null, "تغيير القديم")] }, default)).Success);
        var completed = await completion.Submit(command, default);
        Assert.True(completed.Success, completed.Message);
        fixture.Db.ChangeTracker.Clear();
        var persisted = await fixture.Db.StudentExamAttempts.Include(a => a.Answers).SingleAsync(a => a.Id == attempt.Id);
        Assert.Equal(expectedScore, persisted.ScoreAchieved);
        Assert.Equal("A", persisted.Answers.Single(a => a.ExamQuestionId == question.Id).SubmittedText);
        Assert.Equal(4, persisted.Answers.Single(a => a.ExamQuestionId == question.Id).PointsAwarded);
        Assert.Equal(expired, persisted.IsTimeExpired);
        Assert.False((await completion.Submit(command, default)).Success);
        if (addedType == ExamQuestionType.Essay)
        {
            Assert.Equal("قيد التصحيح", persisted.Evaluation);
            var essay = await fixture.Db.EssaySubmissions.SingleAsync(e => e.StudentExamAttemptId == attempt.Id);
            Assert.Equal(EssaySubmissionStatus.WaitAI, essay.Status);
            var aiGraded = await new WebhookEssayGradedCommandHandler(fixture.Db)
                .Handle(new(essay.Id, 1, "إجابة صحيحة"), default);
            Assert.True(aiGraded.Success, aiGraded.Message);
            fixture.Db.ChangeTracker.Clear();
            var finished = await fixture.Db.StudentExamAttempts.SingleAsync(a => a.Id == attempt.Id);
            Assert.Equal(20, finished.ScoreAchieved);
            Assert.True(finished.IsPassed);
            Assert.False(AssessmentDefinitionSnapshot.Read(finished.DefinitionSnapshotJson!, "exam", exam.Id).Revision!.RequiresReview);
        }
        else Assert.Equal(!expired, persisted.IsPassed);
    }

    [Theory]
    [InlineData(RemovedQuestionPolicy.KeepPreviousGrade, ScoreDecreasePolicy.Allow, 15)]
    [InlineData(RemovedQuestionPolicy.Exclude, ScoreDecreasePolicy.Allow, 0)]
    [InlineData(RemovedQuestionPolicy.Exclude, ScoreDecreasePolicy.Prevent, 15)]
    [InlineData(RemovedQuestionPolicy.AwardFullPoints, ScoreDecreasePolicy.Prevent, 20)]
    public async Task ConfirmedHomeworkRegradePersistsRemovalPolicyWithoutDeletingAnswers(RemovedQuestionPolicy removedPolicy, ScoreDecreasePolicy scorePolicy, decimal expectedScore)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        seed.Submission.OverallScore = 15;
        seed.Submission.Status = SubmissionStatus.Graded;
        seed.Submission.Answers.Single().ScoreReceived = 3;
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, Guid.Empty, seed.Teacher.UserId);
        var definition = AssessmentDefinitionSnapshot.FromHomework(seed.Homework) with { Questions = [] };
        var policy = new AssessmentRevisionPolicy(PreviousAttemptsPolicy.Regrade, removedPolicy, ScoreDecrease: scorePolicy);
        var handler = new AssessmentRevisionCommandHandler(fixture.Db, new(fixture.Db));
        var preview = await handler.Handle(new PreviewAssessmentRevisionQuery(target, definition, policy), default);
        Assert.Equal(expectedScore, Assert.Single(preview.Data!.Attempts).RevisedScore);
        var operationId = Guid.NewGuid();

        var saved = await handler.Handle(new SaveAssessmentRevisionCommand(target, definition, policy,
            preview.Data!.RevisionToken, operationId, ConfirmPreviousAttempts: true), default);
        Assert.True(saved.Success, saved.Message);
        fixture.Db.ChangeTracker.Clear();
        var submission = await fixture.Db.HomeworkSubmissions.Include(s => s.Answers).SingleAsync(s => s.Id == seed.Submission.Id);
        Assert.Equal(expectedScore, submission.OverallScore);
        Assert.Equal("إجابة الطالب المقالي", Assert.Single(submission.Answers).ProvidedAnswer);
        Assert.Equal(SubmissionStatus.Graded, submission.Status);
        var audit = await fixture.Db.AuditLogs.SingleAsync(a => a.EntityId == submission.Id && a.Action == "AssessmentAttemptRegraded");
        Assert.Equal(operationId.ToString(), audit.CorrelationId);
        Assert.Contains("ScoreReceived", audit.OldValues!);
        Assert.True(await fixture.Db.HomeworkQuestions.AnyAsync(q => q.HomeworkId == seed.Homework.Id && q.IsRetired));
    }

    [Fact]
    public async Task ConfirmedAddedQuestionReopensHomeworkAndKeepsPreviouslyGradedAnswer()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        seed.Submission.AssistantReviewerId = seed.Teacher.UserId;
        seed.Submission.OverallScore = 15;
        seed.Submission.Status = SubmissionStatus.Graded;
        seed.Submission.Answers.Single().ScoreReceived = 3;
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, Guid.Empty, seed.Teacher.UserId);
        var original = AssessmentDefinitionSnapshot.FromHomework(seed.Homework);
        var added = original.Questions.Single() with { Id = Guid.NewGuid(), Type = 0, Text = "سؤال إضافي",
            Options = [new(Guid.Empty, "A", true), new(Guid.Empty, "B", false)], CorrectAnswerKey = "A" };
        var definition = original with { Questions = [original.Questions.Single(), added] };
        var policy = new AssessmentRevisionPolicy(PreviousAttemptsPolicy.Regrade, AddedQuestions: AddedQuestionPolicy.RequestCompletion);
        var handler = new AssessmentRevisionCommandHandler(fixture.Db, new(fixture.Db));
        var preview = await handler.Handle(new PreviewAssessmentRevisionQuery(target, definition, policy), default);
        Assert.True(Assert.Single(preview.Data!.Attempts).RequiresCompletion);
        var command = new SaveAssessmentRevisionCommand(target, definition, policy, preview.Data.RevisionToken, Guid.NewGuid());
        Assert.False((await handler.Handle(command, default)).Success);
        var saved = await handler.Handle(command with { ConfirmPreviousAttempts = true }, default);
        Assert.True(saved.Success, saved.Message);
        fixture.Db.ChangeTracker.Clear();
        var submission = await fixture.Db.HomeworkSubmissions.Include(s => s.Answers).SingleAsync(s => s.Id == seed.Submission.Id);
        Assert.Equal(SubmissionStatus.InProgress, submission.Status);
        Assert.Null(submission.SubmittedAt);
        Assert.Equal(3, submission.Answers.Single(a => a.QuestionId == original.Questions[0].Id).ScoreReceived);
        Assert.Equal("إجابة الطالب المقالي", submission.Answers.Single(a => a.QuestionId == original.Questions[0].Id).ProvidedAnswer);
        Assert.Null(submission.Answers.Single(a => a.QuestionId == added.Id).ScoreReceived);
        var snapshot = AssessmentDefinitionSnapshot.Read(submission.DefinitionSnapshotJson!, "homework", submission.HomeworkId);
        Assert.Equal(added.Id, Assert.Single(snapshot.Revision!.Answers, a => a.RequiresCompletion).QuestionId);

        var started = await new StartHomeworkAttemptQueryHandler(fixture.Db, new AccessCheckService(fixture.Db))
            .Handle(new(seed.Homework.Id, seed.Submission.StudentId), default);
        Assert.True(started.Success, started.Message);
        Assert.Equal(added.Id, Assert.Single(started.Data!.Questions).Id);
        Assert.Equal(command.OperationId, started.Data.RevisionId);
        var completion = new HomeworkRevisionCompletion(fixture.Db);
        var studentCommand = new SubmitHomeworkCommand(seed.Homework.Id, seed.Submission.StudentId,
            [new(added.Id, "A")], started.Data.RevisionId);
        Assert.False((await completion.Submit(studentCommand with { RevisionId = Guid.NewGuid() }, submission.Id, default)).Success);
        Assert.False((await completion.Submit(studentCommand with
        { Answers = [new(original.Questions[0].Id, "تغيير الإجابة القديمة")] }, submission.Id, default)).Success);
        var completed = await completion.Submit(studentCommand, submission.Id, default);
        Assert.True(completed.Success, completed.Message);
        fixture.Db.ChangeTracker.Clear();
        var finished = await fixture.Db.HomeworkSubmissions.Include(s => s.Answers).SingleAsync(s => s.Id == submission.Id);
        Assert.Equal(SubmissionStatus.Graded, finished.Status);
        Assert.Equal(17.5m, finished.OverallScore);
        Assert.Equal(3, finished.Answers.Single(a => a.QuestionId == original.Questions[0].Id).ScoreReceived);
        Assert.Equal("إجابة الطالب المقالي", finished.Answers.Single(a => a.QuestionId == original.Questions[0].Id).ProvidedAnswer);
        Assert.Equal(4, finished.Answers.Single(a => a.QuestionId == added.Id).ScoreReceived);
        Assert.False(AssessmentDefinitionSnapshot.Read(finished.DefinitionSnapshotJson!, "homework", finished.HomeworkId).Revision!.RequiresCompletion);
        Assert.False((await completion.Submit(studentCommand, submission.Id, default)).Success);
        Assert.Equal(1, await fixture.Db.AuditLogs.CountAsync(a => a.EntityId == submission.Id && a.Action == "HomeworkRevisionCompleted"));
        var graded = await new GradeAssessmentCommandHandler(fixture.Db, new(fixture.Db)).Handle(new(
            target with { AttemptId = submission.Id }, [new(original.Questions[0].Id, 4), new(added.Id, 4)], "تمت المراجعة"), default);
        Assert.True(graded.Success, graded.Message);
        fixture.Db.ChangeTracker.Clear();
        var reviewed = await fixture.Db.HomeworkSubmissions.SingleAsync(s => s.Id == submission.Id);
        var reviewedPlan = AssessmentDefinitionSnapshot.Read(reviewed.DefinitionSnapshotJson!, "homework", reviewed.HomeworkId).Revision!;
        Assert.Equal(20, reviewed.OverallScore);
        Assert.All(reviewedPlan.Answers, answer => { Assert.Equal(4, answer.AwardedPoints); Assert.True(answer.ManuallyGraded); });
    }

    [Theory]
    [InlineData(ManualGradePolicy.Preserve, false)]
    [InlineData(ManualGradePolicy.ReturnForReview, true)]
    public async Task RevisionPreviewShowsRealManualGradeImpactWithoutChangingSubmission(ManualGradePolicy manualPolicy, bool requiresReview)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        seed.Submission.AssistantReviewerId = seed.Teacher.UserId;
        seed.Submission.Status = SubmissionStatus.Graded;
        seed.Submission.OverallScore = 15;
        seed.Submission.Answers.Single().ScoreReceived = 3;
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, Guid.Empty, seed.Teacher.UserId);
        var handler = new AssessmentRevisionCommandHandler(fixture.Db, new(fixture.Db));
        var editor = await handler.Handle(new GetAssessmentEditorQuery(target), default);
        var definition = editor.Data!.Definition with
        { Questions = [editor.Data.Definition.Questions.Single() with { Points = 8 }] };
        var preview = await handler.Handle(new PreviewAssessmentRevisionQuery(target, definition,
            new(PreviousAttemptsPolicy.Regrade, ManualGrades: manualPolicy)), default);
        Assert.True(preview.Success, preview.Message);
        var impact = Assert.Single(preview.Data!.Attempts);
        Assert.Equal(seed.Submission.Id, impact.AttemptId);
        Assert.Equal(15, impact.PreviousScore);
        Assert.Equal(requiresReview ? null : (decimal?)15, impact.RevisedScore);
        Assert.Equal(requiresReview, impact.RequiresReview);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(15, (await fixture.Db.HomeworkSubmissions.SingleAsync(s => s.Id == seed.Submission.Id)).OverallScore);
        Assert.Equal(4, (await fixture.Db.HomeworkQuestions.SingleAsync(q => q.HomeworkId == seed.Homework.Id)).PointsActive);
    }

    [Fact]
    public async Task RevisionPreviewAwardsRemovedQuestionOnlyWhenChosenWithoutSavingGrade()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, Guid.Empty, seed.Teacher.UserId);
        var definition = AssessmentDefinitionSnapshot.FromHomework(seed.Homework) with { Questions = [] };
        var preview = await new AssessmentRevisionCommandHandler(fixture.Db, new(fixture.Db)).Handle(
            new PreviewAssessmentRevisionQuery(target, definition,
                new(PreviousAttemptsPolicy.Regrade, RemovedQuestionPolicy.AwardFullPoints)), default);
        Assert.True(preview.Success, preview.Message);
        Assert.Equal(20, Assert.Single(preview.Data!.Attempts).RevisedScore);
        fixture.Db.ChangeTracker.Clear();
        Assert.Null((await fixture.Db.HomeworkAnswers.SingleAsync(a => a.HomeworkSubmissionId == seed.Submission.Id)).ScoreReceived);
        Assert.True(await fixture.Db.HomeworkQuestions.AnyAsync(q => q.HomeworkId == seed.Homework.Id && !q.IsRetired));
    }

    [Fact]
    public async Task RevisionSavePreservesLegacyHomeworkAnswersAndGradesAndRetryIsIdempotent()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var originalQuestion = seed.Homework.Questions.Single();
        seed.Submission.OverallScore = 15;
        seed.Submission.Status = SubmissionStatus.Graded;
        seed.Submission.Answers.Single().ScoreReceived = 3;
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, Guid.Empty, seed.Teacher.UserId);
        var handler = new AssessmentRevisionCommandHandler(fixture.Db, new(fixture.Db));
        var editor = await handler.Handle(new GetAssessmentEditorQuery(target), default);
        var replacement = editor.Data!.Definition.Questions.Single() with
        { Id = Guid.NewGuid(), Text = "السؤال الجديد", Points = 10 };
        var definition = editor.Data.Definition with { Title = "الواجب المعدل", TotalScore = 100, PassingScore = 90,
            DurationMinutes = null, IsActive = false, Questions = [replacement] };
        var preview = await handler.Handle(new PreviewAssessmentRevisionQuery(target, definition, new()), default);
        Assert.True(preview.Success, preview.Message);
        Assert.Equal(1, preview.Data!.AttemptCount);
        Assert.Equal(1, preview.Data.AddedQuestions);
        Assert.Equal(1, preview.Data.RemovedQuestions);
        var command = new SaveAssessmentRevisionCommand(target, definition, new(), preview.Data.RevisionToken, Guid.NewGuid());

        await using var duplicateContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
        var duplicateHandler = new AssessmentRevisionCommandHandler(duplicateContext, new(duplicateContext));
        var saves = await Task.WhenAll(handler.Handle(command, default), duplicateHandler.Handle(command, default));
        Assert.All(saves, saved => Assert.True(saved.Success, saved.Message));
        Assert.True((await handler.Handle(command, default)).Success);
        fixture.Db.ChangeTracker.Clear();
        var submission = await fixture.Db.HomeworkSubmissions.Include(s => s.Answers).Include(s => s.Homework).ThenInclude(h => h.Questions)
            .SingleAsync(s => s.Id == seed.Submission.Id);
        Assert.Equal(15, submission.OverallScore);
        Assert.Equal(10, submission.PassingScoreSnapshot);
        Assert.Equal(20, submission.TotalScoreSnapshot);
        var answer = Assert.Single(submission.Answers);
        Assert.Equal(originalQuestion.Id, answer.QuestionId);
        Assert.Equal(3, answer.ScoreReceived);
        Assert.Equal("إجابة الطالب المقالي", answer.ProvidedAnswer);
        var oldDefinition = AssessmentDefinitionSnapshot.ResolveHomework(submission.Homework, submission.DefinitionSnapshotJson);
        Assert.Equal(originalQuestion.Id, Assert.Single(oldDefinition.Questions).Id);
        Assert.Equal(20, oldDefinition.TotalScore);
        Assert.Equal(30, oldDefinition.DurationMinutes);
        Assert.Null(submission.Homework.DurationMinutes);
        Assert.False(submission.Homework.IsActive);
        Assert.True(submission.Homework.Questions.Single(q => q.Id == originalQuestion.Id).IsRetired);
        Assert.False(submission.Homework.Questions.Single(q => q.Id == replacement.Id).IsRetired);
        Assert.Equal(1, await fixture.Db.AuditLogs.CountAsync(a => a.Id == command.OperationId));
    }

    [Fact]
    public async Task RevisionSaveRejectsStalePreviewWithoutChangingDefinitionOrCapturingAttempts()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, Guid.Empty, seed.Teacher.UserId);
        var handler = new AssessmentRevisionCommandHandler(fixture.Db, new(fixture.Db));
        var editor = await handler.Handle(new GetAssessmentEditorQuery(target), default);
        var definition = editor.Data!.Definition with { Title = "Must not save" };
        var preview = await handler.Handle(new PreviewAssessmentRevisionQuery(target, definition, new()), default);
        seed.Submission.Answers.Single().ScoreReceived = 2;
        await fixture.Db.SaveChangesAsync();
        var operationId = Guid.NewGuid();
        var saved = await handler.Handle(new SaveAssessmentRevisionCommand(target,
            definition, new(), preview.Data!.RevisionToken, operationId), default);
        Assert.False(saved.Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal("الواجب", (await fixture.Db.Homeworks.SingleAsync(h => h.Id == seed.Homework.Id)).Title);
        Assert.Null((await fixture.Db.HomeworkSubmissions.SingleAsync(s => s.Id == seed.Submission.Id)).DefinitionSnapshotJson);
        Assert.False(await fixture.Db.AuditLogs.AnyAsync(a => a.Id == operationId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevisionSaveIsolatesSharedExamQuestionAndKeepsChosenAttemptDefinition(bool regrade)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var essay = await Essay(fixture.Db, seed.Teacher, seed.Submission.StudentId);
        var attempt = await fixture.Db.StudentExamAttempts.Include(a => a.Exam).ThenInclude(e => e.ExamQuestions)
            .ThenInclude(q => q.Question).SingleAsync(a => a.Id == essay.StudentExamAttemptId);
        var otherExam = new Exam { Title = "Shared bank", CreatedByTeacherId = seed.Teacher.Id, TotalScore = 10 };
        otherExam.ExamQuestions.Add(new() { QuestionBankItemId = essay.QuestionId, Points = 4 });
        fixture.Db.Exams.Add(otherExam);
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Exam, attempt.ExamId, Guid.Empty, seed.Teacher.UserId);
        var handler = new AssessmentRevisionCommandHandler(fixture.Db, new(fixture.Db));
        var editor = await handler.Handle(new GetAssessmentEditorQuery(target), default);
        var definition = editor.Data!.Definition with
        { Questions = [editor.Data.Definition.Questions.Single() with { Text = "New rubric question", Points = 8 }] };
        var policy = new AssessmentRevisionPolicy(regrade ? PreviousAttemptsPolicy.Regrade : PreviousAttemptsPolicy.Preserve);
        var preview = await handler.Handle(new PreviewAssessmentRevisionQuery(target, definition, policy), default);

        var saved = await handler.Handle(new SaveAssessmentRevisionCommand(target, definition, policy, preview.Data!.RevisionToken,
            Guid.NewGuid(), ConfirmPreviousAttempts: regrade), default);
        Assert.True(saved.Success, saved.Message);
        fixture.Db.ChangeTracker.Clear();
        var current = await fixture.Db.Exams.Include(e => e.ExamQuestions).ThenInclude(q => q.Question).SingleAsync(e => e.Id == target.AssessmentId);
        Assert.Equal("New rubric question", Assert.Single(current.ExamQuestions).Question.Text);
        Assert.NotEqual(essay.QuestionId, current.ExamQuestions.Single().QuestionBankItemId);
        var shared = await fixture.Db.ExamQuestions.Include(q => q.Question).SingleAsync(q => q.ExamId == otherExam.Id);
        Assert.Equal("علل؟", shared.Question.Text);
        var snapshot = await fixture.Db.StudentExamAttempts.Where(a => a.Id == attempt.Id).Select(a => a.DefinitionSnapshotJson).SingleAsync();
        var oldQuestion = Assert.Single(AssessmentDefinitionSnapshot.ResolveExam(current, snapshot).ExamQuestions);
        Assert.Equal(regrade ? current.ExamQuestions.Single().QuestionBankItemId : essay.QuestionId, oldQuestion.QuestionBankItemId);
        Assert.Equal(regrade ? 8 : 4, oldQuestion.Points);
        var savedEssay = await fixture.Db.EssaySubmissions.SingleAsync(e => e.Id == essay.Id);
        Assert.Equal(regrade ? EssaySubmissionStatus.WaitTeacher : EssaySubmissionStatus.WaitAI, savedEssay.Status);
        Assert.Equal("شرح", savedEssay.AnswerText);
    }

    [Fact]
    public async Task RegradeSharedMcqMapsSelectedOptionToIsolatedBankAndPersistsCorrectedScore()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var bank = new QuestionBankItem
        {
            Text = "Shared MCQ", CreatedByTeacherId = seed.Teacher.Id, Type = ExamQuestionType.MCQ,
            Subject = new Subject { Name = "MCQ", NormalizedName = Guid.NewGuid().ToString("N") },
            Options = [new() { Text = "A", IsCorrect = true }, new() { Text = "B", IsCorrect = false }]
        };
        var exam = new Exam { Title = "MCQ exam", TotalScore = 10, PassingScore = 5, CreatedByTeacherId = seed.Teacher.Id };
        var question = new ExamQuestion { Question = bank, Points = 4 };
        exam.ExamQuestions.Add(question);
        var other = new Exam { Title = "Other MCQ exam", CreatedByTeacherId = seed.Teacher.Id };
        other.ExamQuestions.Add(new ExamQuestion { Question = bank, Points = 4 });
        var attempt = new StudentExamAttempt { Exam = exam, UserId = seed.Submission.StudentId, Evaluation = "راسب" };
        attempt.Answers.Add(new StudentAnswer { ExamQuestion = question, SelectedOption = bank.Options.Single(o => o.Text == "B"), SubmittedText = "B" });
        fixture.Db.StudentExamAttempts.Add(attempt);
        fixture.Db.Exams.Add(other);
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Exam, exam.Id, Guid.Empty, seed.Teacher.UserId);
        var original = AssessmentDefinitionSnapshot.FromExam(exam);
        var definition = original with { Questions = [original.Questions.Single() with
            { Options = original.Questions.Single().Options.Select(o => o with { IsCorrect = o.Text == "B" }).ToArray() }] };
        var policy = new AssessmentRevisionPolicy(PreviousAttemptsPolicy.Regrade);
        var handler = new AssessmentRevisionCommandHandler(fixture.Db, new(fixture.Db));
        var preview = await handler.Handle(new PreviewAssessmentRevisionQuery(target, definition, policy), default);
        Assert.Equal(10, Assert.Single(preview.Data!.Attempts).RevisedScore);
        var saved = await handler.Handle(new SaveAssessmentRevisionCommand(target, definition, policy,
            preview.Data.RevisionToken, Guid.NewGuid(), ConfirmPreviousAttempts: true), default);
        Assert.True(saved.Success, saved.Message);
        fixture.Db.ChangeTracker.Clear();
        var persisted = await fixture.Db.StudentExamAttempts.Include(a => a.Answers).ThenInclude(a => a.SelectedOption)
            .SingleAsync(a => a.Id == attempt.Id);
        Assert.Equal(10, persisted.ScoreAchieved);
        Assert.True(persisted.IsPassed);
        Assert.True(Assert.Single(persisted.Answers).SelectedOption!.IsCorrect);
        Assert.Equal("B", persisted.Answers.Single().SubmittedText);
        Assert.NotEqual(bank.Id, persisted.Answers.Single().SelectedOption!.QuestionBankItemId);
        Assert.Equal("A", (await fixture.Db.QuestionOptions.SingleAsync(o => o.QuestionBankItemId == bank.Id && o.IsCorrect)).Text);
    }

    [Fact]
    public async Task HomeworkReviewShowsAnswerAndManualGradePersistsScaledScoreAndAudit()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, seed.Submission.Id, seed.Teacher.UserId);
        var auth = new TeacherAuthorizationService(fixture.Db);
        var review = await new GetAssessmentReviewQueryHandler(fixture.Db, auth).Handle(new(target), default);
        Assert.Equal("إجابة الطالب المقالي", Assert.Single(review.Data!.Questions).Answer);
        Assert.True(review.Data.CanGrade);
        var graded = await new GradeAssessmentCommandHandler(fixture.Db, auth).Handle(new(target,
            [new(seed.Homework.Questions.Single().Id, 3)], "راجع السبب"), default);
        Assert.True(graded.Success);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.HomeworkSubmissions.Include(s => s.Answers).SingleAsync(s => s.Id == seed.Submission.Id);
        Assert.Equal(15m, saved.OverallScore);
        Assert.Equal(SubmissionStatus.Graded, saved.Status);
        Assert.Equal(3, Assert.Single(saved.Answers).ScoreReceived);
        Assert.Equal(seed.Teacher.UserId, saved.AssistantReviewerId);
        Assert.Equal("راجع السبب", saved.AssistantNotes);
        Assert.True(await fixture.Db.AuditLogs.AnyAsync(a => a.EntityId == saved.Id && a.Action == "AssessmentManuallyGraded"));
    }

    [Fact]
    public async Task ManualGradePersistsZeroForUnansweredHomeworkQuestion()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        fixture.Db.HomeworkAnswers.RemoveRange(await fixture.Db.HomeworkAnswers.Where(a => a.HomeworkSubmissionId == seed.Submission.Id).ToListAsync());
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, seed.Submission.Id, seed.Teacher.UserId);
        var result = await new GradeAssessmentCommandHandler(fixture.Db, new(fixture.Db))
            .Handle(new(target, [new(seed.Homework.Questions.Single().Id, 0)], null), default);
        Assert.True(result.Success);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.HomeworkAnswers.SingleAsync(a => a.HomeworkSubmissionId == seed.Submission.Id);
        Assert.Equal(0, saved.ScoreReceived);
        Assert.Equal("", saved.ProvidedAnswer);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(1.5)]
    public async Task InvalidHomeworkGradeCannotPartiallyChangeAnswers(decimal score)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, seed.Submission.Id, seed.Teacher.UserId);
        var result = await new GradeAssessmentCommandHandler(fixture.Db, new(fixture.Db))
            .Handle(new(target, [new(seed.Homework.Questions.Single().Id, score)], null), default);
        Assert.False(result.Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Null((await fixture.Db.HomeworkAnswers.SingleAsync(a => a.HomeworkSubmissionId == seed.Submission.Id)).ScoreReceived);
    }

    [Fact]
    public async Task OtherTeacherCannotReviewGradeDeleteOrExportHomework()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var other = await Teacher(fixture.Db);
        var auth = new TeacherAuthorizationService(fixture.Db);
        var target = new AssessmentTarget(AssessmentKind.Homework, seed.Homework.Id, seed.Submission.Id, other.UserId);
        Assert.False((await new GetAssessmentReviewQueryHandler(fixture.Db, auth).Handle(new(target), default)).Success);
        Assert.False((await new GradeAssessmentCommandHandler(fixture.Db, auth).Handle(new(target, [new(seed.Homework.Questions.Single().Id, 4)], null), default)).Success);
        Assert.False((await new DeleteHomeworkAttemptCommandHandler(fixture.Db, auth).Handle(new(seed.Homework.Id, seed.Submission.Id, other.UserId), default)).Success);
        Assert.False((await new GetMissingHomeworkStudentsQueryHandler(fixture.Db, auth, new AccessCheckService(fixture.Db)).Handle(new(seed.Homework.Id, other.UserId), default)).Success);
        var revisions = new AssessmentRevisionCommandHandler(fixture.Db, auth);
        Assert.False((await revisions.Handle(new GetAssessmentEditorQuery(target), default)).Success);
        Assert.False((await revisions.Handle(new SaveAssessmentRevisionCommand(target,
            AssessmentDefinitionSnapshot.FromHomework(seed.Homework), new(), "invalid", Guid.NewGuid()), default)).Success);
        Assert.True(await fixture.Db.HomeworkSubmissions.AnyAsync(s => s.Id == seed.Submission.Id));
    }

    [Fact]
    public async Task MissingHomeworkExportIncludesStartedButNotSubmittedAndExcludesExpiredAccess()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var missing = await Student(fixture.Db);
        var expired = await Student(fixture.Db);
        fixture.Db.StudentAccessGrants.Add(new() { UserId = missing.Id, LessonId = seed.Homework.LessonId, GrantType = CodeType.Lesson, IsActive = true });
        fixture.Db.StudentAccessGrants.Add(new() { UserId = expired.Id, LessonId = seed.Homework.LessonId, GrantType = CodeType.Lesson, IsActive = true, ExpiresAt = DateTime.UtcNow.AddDays(-1) });
        fixture.Db.HomeworkSubmissions.Add(new() { HomeworkId = seed.Homework.Id, StudentId = missing.Id, Status = SubmissionStatus.InProgress });
        await fixture.Db.SaveChangesAsync();
        var result = await new GetMissingHomeworkStudentsQueryHandler(fixture.Db, new(fixture.Db), new AccessCheckService(fixture.Db))
            .Handle(new(seed.Homework.Id, seed.Teacher.UserId), default);
        Assert.True(result.Success);
        Assert.Equal(missing.Id, Assert.Single(result.Data!.Students).StudentId);
        Assert.False(result.Data.HasMore);
    }

    [Fact]
    public async Task DeletingHomeworkAttemptRemovesOnlyItsAnswersAndRetainsAudit()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var other = await Student(fixture.Db);
        var retained = new HomeworkSubmission { HomeworkId = seed.Homework.Id, StudentId = other.Id };
        fixture.Db.HomeworkSubmissions.Add(retained);
        await fixture.Db.SaveChangesAsync();
        var result = await new DeleteHomeworkAttemptCommandHandler(fixture.Db, new(fixture.Db))
            .Handle(new(seed.Homework.Id, seed.Submission.Id, seed.Teacher.UserId), default);
        Assert.True(result.Success);
        Assert.False(await fixture.Db.HomeworkAnswers.AnyAsync(a => a.HomeworkSubmissionId == seed.Submission.Id));
        Assert.False(await fixture.Db.HomeworkSubmissions.AnyAsync(s => s.Id == seed.Submission.Id));
        Assert.True(await fixture.Db.HomeworkSubmissions.AnyAsync(s => s.Id == retained.Id));
        Assert.True(await fixture.Db.AuditLogs.AnyAsync(a => a.EntityId == seed.Submission.Id && a.Action == "HomeworkAttemptDeleted"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualEssayGradeWorksBeforeOrAfterAiWithoutDoubleCounting(bool aiFirst)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var essay = await Essay(fixture.Db, seed.Teacher, seed.Submission.StudentId);
        if (aiFirst) Assert.True((await new WebhookEssayGradedCommandHandler(fixture.Db).Handle(new(essay.Id, 1, "AI"), default)).Success);
        var manual = await new GradeEssayCommandHandler(fixture.Db, new(fixture.Db)).Handle(new(essay.Id, 2, "manual", seed.Teacher.UserId), default);
        Assert.True(manual.Success);
        Assert.True((await new WebhookEssayGradedCommandHandler(fixture.Db).Handle(new(essay.Id, 1, "late AI"), default)).Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(5m, (await fixture.Db.StudentExamAttempts.SingleAsync(a => a.Id == essay.StudentExamAttemptId)).ScoreAchieved);
        Assert.Equal("manual", (await fixture.Db.EssaySubmissions.SingleAsync(e => e.Id == essay.Id)).TeacherFeedback);
    }

    [Fact]
    public async Task ConcurrentAiAndManualGradingKeepsTeacherGradeAndDeleteMakesLateCallbackNoOp()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var essay = await Essay(fixture.Db, seed.Teacher, seed.Submission.StudentId);
        await using var second = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
        var teacher = new GradeEssayCommandHandler(fixture.Db, new(fixture.Db)).Handle(new(essay.Id, 1, "teacher wins", seed.Teacher.UserId), default);
        var ai = new WebhookEssayGradedCommandHandler(second).Handle(new(essay.Id, 1, "AI"), default);
        await Task.WhenAll(teacher, ai);
        Assert.True((await teacher).Success);
        Assert.True((await ai).Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(1m, (await fixture.Db.EssaySubmissions.SingleAsync(e => e.Id == essay.Id)).TeacherFinalScore);
        var examId = await fixture.Db.StudentExamAttempts.Where(a => a.Id == essay.StudentExamAttemptId).Select(a => a.ExamId).SingleAsync();
        Assert.True((await new DeleteExamAttemptCommandHandler(fixture.Db, new(fixture.Db)).Handle(new(examId, essay.StudentExamAttemptId, seed.Teacher.UserId), default)).Success);
        var late = await new WebhookEssayGradedCommandHandler(fixture.Db).Handle(new(essay.Id, 1, "late"), default);
        Assert.Equal("Deleted", late.Data!.Status);
    }

    [Fact]
    public async Task ExamReviewAndManualGradeUseOnlyQuestionsAssignedToThisAttempt()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var essay = await Essay(fixture.Db, seed.Teacher, seed.Submission.StudentId);
        var attempt = await fixture.Db.StudentExamAttempts.Include(a => a.Exam).ThenInclude(e => e.ExamQuestions)
            .SingleAsync(a => a.Id == essay.StudentExamAttemptId);
        var assigned = attempt.Exam.ExamQuestions.Single().Id;
        fixture.Db.ExamQuestions.Add(new ExamQuestion
        {
            ExamId = attempt.ExamId, Points = 20, Order = 2, Question = new QuestionBankItem
            {
                Text = "Not assigned to student", CreatedByTeacherId = seed.Teacher.Id, SubjectId = essay.Question.SubjectId
            }
        });
        await fixture.Db.SaveChangesAsync();
        var target = new AssessmentTarget(AssessmentKind.Exam, attempt.ExamId, attempt.Id, seed.Teacher.UserId);
        var auth = new TeacherAuthorizationService(fixture.Db);
        var review = await new GetAssessmentReviewQueryHandler(fixture.Db, auth).Handle(new(target), default);
        Assert.Equal(assigned, Assert.Single(review.Data!.Questions).QuestionId);
        Assert.Equal("شرح", review.Data.Questions[0].Answer);
        var result = await new GradeAssessmentCommandHandler(fixture.Db, auth).Handle(new(target, [new(assigned, 3)], "manual"), default);
        Assert.True(result.Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(7.5m, (await fixture.Db.StudentExamAttempts.SingleAsync(a => a.Id == attempt.Id)).ScoreAchieved);
        Assert.True((await new WebhookEssayGradedCommandHandler(fixture.Db).Handle(new(essay.Id, 1, "late"), default)).Success);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(7.5m, (await fixture.Db.StudentExamAttempts.SingleAsync(a => a.Id == attempt.Id)).ScoreAchieved);
    }

    private static User User(string name) => new() { FullName = name, PasswordHash = "test-only", PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", IsActive = true };
    private static async Task<User> Student(AppDbContext db)
    {
        var student = User("طالب اختبار");
        student.UserRoles.Add(new UserRole { RoleId = await db.Roles.Where(r => r.Type == RoleType.Student).Select(r => r.Id).FirstAsync() });
        db.Users.Add(student); await db.SaveChangesAsync(); return student;
    }
    private static async Task<TeacherProfile> Teacher(AppDbContext db)
    {
        var user = User("مدرس اختبار");
        user.UserRoles.Add(new UserRole { RoleId = await db.Roles.Where(r => r.Type == RoleType.Teacher).Select(r => r.Id).FirstAsync() });
        var teacher = new TeacherProfile { User = user, IsContentVisibleToStudents = true };
        db.TeacherProfiles.Add(teacher); await db.SaveChangesAsync(); return teacher;
    }
    private static async Task<(Homework Homework, HomeworkSubmission Submission, TeacherProfile Teacher)> Seed(AppDbContext db)
    {
        var teacher = await Teacher(db);
        var student = await Student(db);
        var package = new Package { Name = "اختبار", TeacherId = teacher.Id, Subject = new Subject { Name = "اختبار", NormalizedName = Guid.NewGuid().ToString("N") } };
        var lesson = new Lesson { Title = "الحصة", ContentSection = new ContentSection { Title = "الشهر", Term = new Term { Title = "الترم", Package = package } } };
        db.Lessons.Add(lesson); await db.SaveChangesAsync();
        var homework = new Homework { LessonId = lesson.Id, Title = "الواجب", TotalScore = 20, PassingScoreThreshold = 10, IsActive = true };
        var question = new HomeworkQuestion { BodyText = "علل؟", QuestionType = HomeworkQuestionType.Essay, PointsActive = 4, Order = 1 };
        homework.Questions.Add(question);
        var submission = new HomeworkSubmission { Homework = homework, StudentId = student.Id, SubmittedAt = DateTime.UtcNow, Status = SubmissionStatus.PendingReview };
        submission.Answers.Add(new HomeworkAnswer { Question = question, ProvidedAnswer = "إجابة الطالب المقالي" });
        db.HomeworkSubmissions.Add(submission);
        db.StudentAccessGrants.Add(new() { UserId = student.Id, LessonId = lesson.Id, GrantType = CodeType.Lesson, IsActive = true });
        await db.SaveChangesAsync(); return (homework, submission, teacher);
    }
    private static async Task<EssaySubmission> Essay(AppDbContext db, TeacherProfile teacher, Guid studentId)
    {
        var question = new QuestionBankItem { Text = "علل؟", WrittenCorrection = "الإجابة النموذجية", Type = ExamQuestionType.Essay, CreatedByTeacherId = teacher.Id, Subject = new Subject { Name = "Essay subject", NormalizedName = Guid.NewGuid().ToString("N") } };
        var exam = new Exam { Title = "امتحان", TotalScore = 10, PassingScore = 5, CreatedByTeacherId = teacher.Id };
        var examQuestion = new ExamQuestion { Question = question, Points = 4, Order = 1 };
        exam.ExamQuestions.Add(examQuestion);
        var attempt = new StudentExamAttempt { Exam = exam, UserId = studentId, Evaluation = "قيد التصحيح", StartedAt = DateTime.UtcNow };
        attempt.Answers.Add(new StudentAnswer { ExamQuestion = examQuestion, SubmittedText = "شرح" });
        var essay = new EssaySubmission { Attempt = attempt, StudentId = studentId, Question = question, AnswerText = "شرح", Status = EssaySubmissionStatus.WaitAI };
        db.EssaySubmissions.Add(essay); await db.SaveChangesAsync(); return essay;
    }
}
