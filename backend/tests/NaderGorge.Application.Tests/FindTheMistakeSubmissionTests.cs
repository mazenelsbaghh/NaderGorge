using MediatR;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class FindTheMistakeSubmissionTests
{
    [Fact]
    public async Task SubmitExam_GradesFindTheMistakeBySelectedText()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "401");
        var (exam, examQuestion, _, _, _) = await TestAppDbContextFactory.SeedFindTheMistakeExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);

        var handler = new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer());
        var result = await handler.Handle(
            new SubmitExamCommand(exam.Id, attempt.Id, student.Id, new List<AnswerSubmissionDto>
            {
                new(examQuestion.Id, null, null, "rise")
            }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2m, result.Data!.ScoreAchieved);
        Assert.True(result.Data.IsPassed);
        Assert.Single(db.StudentAnswers);
        Assert.Equal("rise", db.StudentAnswers.Single().SubmittedText);
        Assert.True(result.Data.Questions.Single().IsCorrect);
    }

    [Fact]
    public async Task SubmitExam_TreatsMissingFindTheMistakePayloadAsUnanswered()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "402");
        var (exam, examQuestion, _, _, _) = await TestAppDbContextFactory.SeedFindTheMistakeExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);

        var handler = new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer());
        var result = await handler.Handle(
            new SubmitExamCommand(exam.Id, attempt.Id, student.Id, new List<AnswerSubmissionDto>
            {
                new(examQuestion.Id, null, null, null)
            }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0m, result.Data!.ScoreAchieved);
        Assert.False(result.Data.Questions.Single().IsAnswered);
    }
}
