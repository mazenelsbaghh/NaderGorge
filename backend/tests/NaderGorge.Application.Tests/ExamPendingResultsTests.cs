using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Exams.Queries;
using NaderGorge.Application.Features.Webhooks.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class ExamPendingResultsTests
{
    [Theory]
    [InlineData(ExamEssayGradingMode.Automatic, true)]
    [InlineData(ExamEssayGradingMode.Automatic, false)]
    [InlineData(ExamEssayGradingMode.Manual, true)]
    [InlineData(ExamEssayGradingMode.Manual, false)]
    public async Task Exam_switch_controls_existing_pending_results_without_changing_the_grade(
        ExamEssayGradingMode mode, bool enabled)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "611");
        var (exam, mcq, essayQuestion, _, _, correct, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        exam.EssayGradingMode = mode;
        exam.ShowResultsWhileEssayPending = !enabled;
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        await db.SaveChangesAsync();

        var submitted = await new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer())
            .Handle(new(exam.Id, attempt.Id, student.Id,
                [new(mcq.Id, correct.Id, null), new(essayQuestion.Id, null, "Saved answer")]), default);

        Assert.True(submitted.Success, submitted.Message);
        Assert.Equal(!enabled, submitted.Data!.PartialResult is not null);
        exam.ShowResultsWhileEssayPending = enabled;
        await db.SaveChangesAsync();
        var afterToggle = await new GetExamAttemptResultQueryHandler(db).Handle(new(attempt.Id, student.Id), default);
        var pending = afterToggle.Data!;
        Assert.Equal(10, pending.TotalScore);
        Assert.False(pending.IsPassed);
        Assert.False(pending.BlocksNextLesson);
        var review = pending.Questions.Single(q => q.ExamQuestionId == essayQuestion.Id);
        Assert.True(review.IsPendingGrading);
        Assert.False(review.IsExcludedFromScore);
        Assert.Equal(enabled ? "A force attracting masses." : null, review.CorrectOptionText);
        Assert.Equal(enabled ? "A force attracting masses." : null, review.WrittenCorrection);
        if (enabled)
        {
            Assert.NotNull(pending.PartialResult);
            Assert.Equal(2, pending.PartialResult.ScoreAchieved);
            Assert.Equal(2, pending.PartialResult.TotalScore);
            Assert.Equal(1, pending.PartialResult.PendingQuestionCount);
        }
        else Assert.Null(pending.PartialResult);
        db.ChangeTracker.Clear();
        var reopened = await new GetExamAttemptResultQueryHandler(db).Handle(new(attempt.Id, student.Id), default);
        Assert.Equal(pending.PartialResult, reopened.Data!.PartialResult);
        Assert.Equal(review.CorrectOptionText, reopened.Data.Questions.Single(q => q.ExamQuestionId == essayQuestion.Id).CorrectOptionText);
        var essay = await db.EssaySubmissions.SingleAsync();
        if (mode == ExamEssayGradingMode.Manual)
        {
            var graded = await new GradeEssayCommandHandler(db, new TeacherAuthorizationService(db))
                .Handle(new(essay.Id, 4, "Reviewed"), default);
            Assert.True(graded.Success, graded.Message);
        }
        else
        {
            var graded = await new WebhookEssayGradedCommandHandler(db).Handle(new(essay.Id, 0.5m, "Reviewed"), default);
            Assert.True(graded.Success, graded.Message);
        }
        var final = await new GetExamAttemptResultQueryHandler(db).Handle(new(attempt.Id, student.Id), default);
        Assert.Equal("Completed", final.Data!.ResultState);
        Assert.Null(final.Data.PartialResult);
        Assert.Equal(6, final.Data.ScoreAchieved);
        Assert.Equal(10, final.Data.TotalScore);
        Assert.False(final.Data.Questions.Single(q => q.ExamQuestionId == essayQuestion.Id).IsPendingGrading);
    }

    [Fact]
    public async Task Pending_result_cannot_reveal_answers_before_submission()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "612");
        var (exam, _, _, _, _, _, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        exam.ShowResultsWhileEssayPending = true;
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);

        var response = await new GetExamAttemptResultQueryHandler(db).Handle(new(attempt.Id, student.Id), default);

        Assert.False(response.Success);
        Assert.Null(response.Data);
    }

    [Theory]
    [InlineData(false, 20, 20)]
    [InlineData(true, 0, 0)]
    public async Task Pending_score_uses_configured_scale_and_handles_essay_only_exams(
        bool essayOnly, decimal earned, decimal maximum)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "613");
        var (exam, mcq, essayQuestion, _, _, correct, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        exam.TotalScore = 100;
        exam.PassingScore = 50;
        exam.ShowResultsWhileEssayPending = true;
        if (essayOnly) mcq.IsRetired = true;
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);

        var response = await new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer())
            .Handle(new(exam.Id, attempt.Id, student.Id,
                [new(mcq.Id, correct.Id, null), new(essayQuestion.Id, null, "Answer")]), default);

        Assert.True(response.Success, response.Message);
        Assert.Equal(earned, response.Data!.PartialResult!.ScoreAchieved);
        Assert.Equal(maximum, response.Data.PartialResult.TotalScore);
        Assert.Equal(100, response.Data.TotalScore);
    }
}
