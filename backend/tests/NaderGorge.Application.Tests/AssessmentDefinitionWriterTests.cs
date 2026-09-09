using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;
using HomeworkQuestion = NaderGorge.Domain.Entities.Homework.HomeworkQuestion;

namespace NaderGorge.Application.Tests;

public class AssessmentDefinitionWriterTests
{
    [Fact]
    public void RemovingHomeworkQuestionRetainsItsRowForExistingAnswers()
    {
        var removed = new HomeworkQuestion { BodyText = "Old essay", QuestionType = Domain.Entities.Homework.QuestionType.Essay, PointsActive = 5 };
        var homework = new HomeworkEntity { Title = "Homework", TotalScore = 5, Questions = [removed] };
        var previous = AssessmentDefinitionSnapshot.FromHomework(homework);
        var replacement = previous.Questions.Single() with { Id = Guid.NewGuid(), Text = "New essay" };

        AssessmentDefinitionWriter.ApplyHomework(homework, previous with { Questions = [replacement] });

        Assert.Equal(2, homework.Questions.Count);
        Assert.True(removed.IsRetired);
        var active = Assert.Single(AssessmentDefinitionSnapshot.FromHomework(homework).Questions);
        Assert.Equal(replacement.Id, active.Id);
        Assert.Equal(removed.Id, Assert.Single(AssessmentDefinitionSnapshot.ResolveHomework(homework, previous.ToJson()).Questions).Id);
    }

    [Fact]
    public void ReplacingExamChoiceRetainsOldChoiceIdentityAndSavedReview()
    {
        var oldChoice = new QuestionOption { Text = "Old correct", IsCorrect = true };
        var retained = new QuestionOption { Text = "Other", IsCorrect = false };
        var bank = new QuestionBankItem { Text = "Question", Options = [oldChoice, retained] };
        var question = new ExamQuestion { Question = bank, QuestionBankItemId = bank.Id, Points = 5 };
        var exam = new Exam { Title = "Exam", TotalScore = 5, ExamQuestions = [question] };
        var previous = AssessmentDefinitionSnapshot.FromExam(exam);
        var replacement = new AssessmentOptionSnapshot(Guid.NewGuid(), "New correct", true);
        var changed = previous.Questions.Single() with
        { Options = [replacement, new(retained.Id, retained.Text, false)] };

        AssessmentDefinitionWriter.ApplyExam(exam, previous with { Questions = [changed] }, new(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(3, bank.Options.Count);
        Assert.True(oldChoice.IsRetired);
        Assert.Equal(2, Assert.Single(AssessmentDefinitionSnapshot.FromExam(exam).Questions).Options.Length);
        var oldDefinition = AssessmentDefinitionSnapshot.ResolveExam(exam, previous.ToJson());
        Assert.Contains(Assert.Single(oldDefinition.ExamQuestions).Question.Options, o => o.Id == oldChoice.Id && o.Text == "Old correct");
    }

    [Fact]
    public void InvalidDraftDoesNotPartiallyChangeTrackedDefinition()
    {
        var homework = new HomeworkEntity { Title = "Original", TotalScore = 10 };
        var proposed = AssessmentDefinitionSnapshot.FromHomework(homework) with { Title = "Changed", PassingScore = 11 };

        Assert.Throws<ArgumentException>(() => AssessmentDefinitionWriter.ApplyHomework(homework, proposed));

        Assert.Equal("Original", homework.Title);
        Assert.Equal(10, homework.TotalScore);
    }
}
