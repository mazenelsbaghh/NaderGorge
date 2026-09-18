using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Domain.Entities.Homework;

namespace NaderGorge.Application.Tests;

public class HomeworkMistakeReviewTests
{
    [Fact]
    public void PassingHomeworkStillShowsWrongAndPartiallyCorrectAnswers()
    {
        var wrong = new HomeworkQuestion { Order = 1, PointsActive = 2, CorrectAnswerKey = "B" };
        var partial = new HomeworkQuestion { Order = 2, PointsActive = 4, QuestionType = QuestionType.Essay, WrittenCorrection = "Rubric" };
        var correct = new HomeworkQuestion { Order = 3, PointsActive = 10 };
        var pending = new HomeworkQuestion { Order = 4, PointsActive = 2 };
        var homework = new Homework { TotalScore = 18, PassingScoreThreshold = 9, Questions = [wrong, partial, correct, pending] };
        var submission = new HomeworkSubmission
        {
            Homework = homework, Status = SubmissionStatus.Graded, OverallScore = 12,
            Answers = [new() { QuestionId = wrong.Id, ScoreReceived = 0, ProvidedAnswer = "A" },
                new() { QuestionId = partial.Id, ScoreReceived = 2, ProvidedAnswer = "Partial essay" },
                new() { QuestionId = correct.Id, ScoreReceived = 10 }, new() { QuestionId = pending.Id }]
        };
        var packageId = Guid.NewGuid();
        var review = HomeworkMistakeReview.Create(submission, packageId);
        Assert.Equal(packageId, review.PackageId);
        Assert.Equal(12, review.Score);
        Assert.Collection(review.Items,
            item => { Assert.Equal(wrong.Id, item.QuestionId); Assert.Equal("A", item.YourAnswer); Assert.Equal("B", item.CorrectAnswer); },
            item => { Assert.Equal(partial.Id, item.QuestionId); Assert.Equal(2, item.ScoreReceived); Assert.Equal("Rubric", item.CorrectAnswer); });
    }

    [Fact]
    public void ReviewKeepsOriginalQuestionAndCorrectionAfterLiveHomeworkChanges()
    {
        var question = new HomeworkQuestion { BodyText = "Original", PointsActive = 3, CorrectAnswerKey = "B" };
        var homework = new Homework { Title = "Original homework", TotalScore = 3, Questions = [question] };
        var submission = new HomeworkSubmission
        {
            Homework = homework, DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromHomework(homework).ToJson(),
            TotalScoreSnapshot = 3, Answers = [new() { QuestionId = question.Id, ScoreReceived = 0 }]
        };
        homework.Title = "Changed";
        homework.Questions.Clear();
        var review = HomeworkMistakeReview.Create(submission, null);
        Assert.Equal("Original homework", review.HomeworkTitle);
        Assert.Equal(3, review.TotalScore);
        var mistake = Assert.Single(review.Items);
        Assert.Equal("Original", mistake.QuestionText);
        Assert.Equal("B", mistake.CorrectAnswer);
        Assert.Equal(3, mistake.MaxPoints);
    }
}
