using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class ExamLifelineSnapshotTests
{
    [Fact]
    public async Task SwapAfterTeacherEditUsesOriginalReserveQuestionAndKeepsOriginalDuration()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (exam, attempt, assigned, reserve) = await SeedAttempt(db);
        var oldText = reserve.Question.Text;
        reserve.Question.Text = "Revised reserve question";
        reserve.IsRetired = true;
        exam.DurationMinutes = 1;
        await db.SaveChangesAsync();

        var response = await new SwapQuestionCommandHandler(db).Handle(
            new(exam.Id, attempt.Id, assigned.Id, attempt.UserId), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        Assert.Equal(reserve.Id, response.Data!.Id);
        var saved = AssessmentDefinitionSnapshot.ResolveExam(exam, attempt.DefinitionSnapshotJson);
        Assert.Equal(oldText, Assert.Single(saved.ExamQuestions).Question.Text);
        Assert.Equal(30, saved.DurationMinutes);
        Assert.Equal(assigned.Id, Assert.Single(AssessmentDefinitionSnapshot.ResolveSwapCandidates(
            exam, attempt.DefinitionSnapshotJson)).Id);
        Assert.Equal(reserve.Id, (await db.StudentAnswers.SingleAsync()).ExamQuestionId);
        Assert.False(attempt.IsTimeExpired);
    }

    [Fact]
    public async Task FiftyFiftyAfterCorrectAnswerEditNeverRemovesOriginalCorrectOption()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (exam, attempt, assigned, _) = await SeedAttempt(db);
        var originalCorrectId = assigned.Question.Options.Single(o => o.IsCorrect).Id;
        foreach (var option in assigned.Question.Options) option.IsCorrect = option.Id != originalCorrectId;
        exam.DurationMinutes = 1;
        await db.SaveChangesAsync();

        var response = await new UseFiftyFiftyCommandHandler(db).Handle(
            new(exam.Id, attempt.Id, assigned.Id, attempt.UserId), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        Assert.Equal(2, response.Data!.Count);
        Assert.DoesNotContain(originalCorrectId, response.Data);
        Assert.True((await db.StudentAnswers.SingleAsync()).HintUsed);
        Assert.False(attempt.IsTimeExpired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FiftyFiftyRejectsUnassignedOrSubmittedQuestionWithoutChangingAnswers(bool submitted)
    {
        await using var db = TestAppDbContextFactory.Create();
        var (exam, attempt, assigned, reserve) = await SeedAttempt(db);
        // The legacy no-snapshot path must enforce assignment too.
        attempt.DefinitionSnapshotJson = null;
        attempt.Evaluation = submitted ? "مقبول" : null;
        await db.SaveChangesAsync();

        var response = await new UseFiftyFiftyCommandHandler(db).Handle(
            new(exam.Id, attempt.Id, submitted ? assigned.Id : reserve.Id, attempt.UserId), CancellationToken.None);

        Assert.False(response.Success);
        var answer = await db.StudentAnswers.SingleAsync();
        Assert.Equal(assigned.Id, answer.ExamQuestionId);
        Assert.False(answer.HintUsed);
    }

    private static async Task<(Exam, StudentExamAttempt, ExamQuestion, ExamQuestion)> SeedAttempt(AppDbContext db)
    {
        var assigned = CreateQuestion("Assigned");
        var reserve = CreateQuestion("Reserve");
        var exam = new Exam
        {
            Title = "Snapshot lifelines", DurationMinutes = 30, TotalScore = 10,
            DisplayQuestionCount = 1, ExamQuestions = [assigned, reserve]
        };
        var attempt = new StudentExamAttempt
        {
            Exam = exam, ExamId = exam.Id, UserId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam, [assigned]).ToJson()
        };
        attempt.Answers.Add(new StudentAnswer
        { StudentExamAttemptId = attempt.Id, ExamQuestionId = assigned.Id, ExamQuestion = assigned });
        db.StudentExamAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return (exam, attempt, assigned, reserve);
    }

    private static ExamQuestion CreateQuestion(string text) => new()
    {
        Points = 10,
        Question = new QuestionBankItem
        {
            Type = QuestionType.MCQ, Text = text,
            Options = [new() { Text = "Correct", IsCorrect = true }, new() { Text = "Wrong A" },
                new() { Text = "Wrong B" }, new() { Text = "Wrong C" }]
        }
    };
}
