using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;
using HomeworkQuestion = NaderGorge.Domain.Entities.Homework.HomeworkQuestion;

namespace NaderGorge.Application.Tests;

public class AssessmentDefinitionWriterTests
{
    [Fact]
    public void CompleteHomeworkChoiceQuestionWithOnePointCanBeSaved()
    {
        var homework = new HomeworkEntity { Title = "واجب", TotalScore = 1 };
        var question = ChoiceQuestion();
        var definition = AssessmentDefinitionSnapshot.FromHomework(homework) with { Questions = [question] };

        AssessmentDefinitionWriter.ApplyHomework(homework, definition);

        var saved = Assert.Single(homework.Questions);
        Assert.Equal(question.Text, saved.BodyText);
        Assert.Equal(1, saved.PointsActive);
        Assert.Equal(Domain.Entities.Homework.QuestionType.MCQ, saved.QuestionType);
        Assert.Equal("منفصلة", saved.CorrectAnswerKey);
    }

    // Incident: preview returned a generic content/type/points error despite the visible question being complete.
    [Theory]
    [InlineData("identity", "التعرف على السؤال")]
    [InlineData("text", "اكتب نص السؤال")]
    [InlineData("type", "اختر نوع السؤال")]
    [InlineData("range", "درجة غير سالبة")]
    [InlineData("precision", "منزلتين عشريتين")]
    [InlineData("fraction", "بدون كسور")]
    [InlineData("options", "30 اختيارًا")]
    public void InvalidQuestionIdentifiesItsPositionAndFieldWithoutChangingTheHomework(string invalidField, string explanation)
    {
        var homework = new HomeworkEntity { Title = "Original", TotalScore = 2 };
        var second = ChoiceQuestion();
        second = invalidField switch
        {
            "identity" => second with { Id = Guid.Empty },
            "text" => second with { Text = "  " },
            "type" => second with { Type = -1 },
            "range" => second with { Points = -1 },
            "precision" => second with { Points = 1.001m },
            "fraction" => second with { Points = 0.5m },
            "options" => second with { Options = Enumerable.Range(0, 31).Select(index => new AssessmentOptionSnapshot(Guid.NewGuid(), $"Choice {index}", index == 0)).ToArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField))
        };
        var definition = AssessmentDefinitionSnapshot.FromHomework(homework) with
        { Title = "Changed", Questions = [ChoiceQuestion(), second] };

        var error = Assert.Throws<ArgumentException>(() => AssessmentDefinitionWriter.ApplyHomework(homework, definition));

        Assert.StartsWith("السؤال 2:", error.Message);
        Assert.Contains(explanation, error.Message);
        Assert.Equal("Original", homework.Title);
        Assert.Empty(homework.Questions);
    }

    [Fact]
    public void ExamQuestionsStillAllowFractionalPoints()
    {
        var exam = new Exam { Title = "Exam", TotalScore = 1 };
        var definition = AssessmentDefinitionSnapshot.FromExam(exam) with
        { Questions = [ChoiceQuestion() with { Points = 0.5m }] };

        AssessmentDefinitionWriter.ApplyExam(exam, definition, new(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(0.5m, Assert.Single(exam.ExamQuestions).Points);
    }

    private static AssessmentQuestionSnapshot ChoiceQuestion() => new(
        Guid.NewGuid(), Guid.NewGuid(), 1, (int)QuestionType.MCQ, "<p>اختر الإجابة الصحيحة</p>", 1,
        null, null, null, null, null, null, null,
        [new(Guid.NewGuid(), "مرتبطة", false), new(Guid.NewGuid(), "مندمجة", false),
            new(Guid.NewGuid(), "منفصلة", true), new(Guid.NewGuid(), "موحدة", false)], "منفصلة");

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
