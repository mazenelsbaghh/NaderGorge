using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Sales;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Webhooks.Commands;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests;

public class ExamSubmissionTimeTests
{
    // 2026-10-18: public reports presented UpdatedAt as the submission time, which grading can change.
    [Fact]
    public async Task EssayGradingAndDuplicateSubmissionPreserveTheAcceptedSubmissionTime()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "submission-time");
        var (exam, mcq, essayQuestion, _, _, correct, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        var command = new SubmitExamCommand(exam.Id, attempt.Id, student.Id,
            [new(mcq.Id, correct.Id, null), new(essayQuestion.Id, null, "Gravity attracts masses.")]);
        var handler = new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer());
        var before = DateTime.UtcNow;

        var response = await handler.Handle(command, default);

        Assert.True(response.Success, response.Message);
        db.ChangeTracker.Clear();
        var submitted = await db.StudentExamAttempts.SingleAsync(a => a.Id == attempt.Id);
        Assert.NotNull(submitted.SubmittedAt);
        Assert.InRange(submitted.SubmittedAt.Value, before, DateTime.UtcNow);
        var originalSubmission = submitted.SubmittedAt;
        var essay = await db.EssaySubmissions.SingleAsync(e => e.StudentExamAttemptId == attempt.Id);
        var graded = await new WebhookEssayGradedCommandHandler(db).Handle(new(essay.Id, 1, "Correct"), default);
        Assert.True(graded.Success, graded.Message);
        var duplicate = await handler.Handle(command, default);
        Assert.True(duplicate.Success, duplicate.Message);
        Assert.Equal(attempt.Id, duplicate.Data!.AttemptId);

        db.ChangeTracker.Clear();
        Assert.Equal(originalSubmission, (await db.StudentExamAttempts.SingleAsync(a => a.Id == attempt.Id)).SubmittedAt);
        var product = new PublicExamProduct { ExamId = exam.Id, Slug = "graded-submission-time" };
        db.PublicExamProducts.Add(product);
        await db.SaveChangesAsync();
        var report = await new GetPublicExamResultsQueryHandler(db).Handle(new(product.Id), default);
        Assert.True(report.Success, report.Message);
        Assert.Equal(originalSubmission, Assert.Single(report.Data!.Attempts).SubmittedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("جيد")]
    public async Task PublicReportDoesNotInventASubmissionTimeFromCreationOrLastModification(string? evaluation)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "historical-submission-time");
        var (exam, _, _, _, _, _, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        attempt.Evaluation = evaluation;
        attempt.UpdatedAt = DateTime.UtcNow;
        var product = new PublicExamProduct { ExamId = exam.Id, Slug = "submission-time-report" };
        db.PublicExamProducts.Add(product);
        await db.SaveChangesAsync();

        var response = await new GetPublicExamResultsQueryHandler(db).Handle(new(product.Id), default);

        Assert.True(response.Success, response.Message);
        Assert.Null(Assert.Single(response.Data!.Attempts).SubmittedAt);
    }
}
