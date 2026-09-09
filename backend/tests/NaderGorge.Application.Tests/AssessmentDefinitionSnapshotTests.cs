using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using HomeworkEntity = NaderGorge.Domain.Entities.Homework.Homework;
using HomeworkQuestion = NaderGorge.Domain.Entities.Homework.HomeworkQuestion;

namespace NaderGorge.Application.Tests;

public class AssessmentDefinitionSnapshotTests
{
    [Fact]
    public void RevisionSwapReplacesOnlyRequiredQuestionAndRetainsOldGrade()
    {
        var original = new ExamQuestion { Question = new QuestionBankItem(), Points = 4 };
        var added = new ExamQuestion { Question = new QuestionBankItem(), Points = 2 };
        var reserve = new ExamQuestion { Question = new QuestionBankItem(), Points = 3 };
        var exam = new Exam { ExamQuestions = [original, added, reserve] };
        var snapshot = AssessmentDefinitionSnapshot.FromExam(exam, [original, added]) with
        { Revision = new([new(original.Id, 4, 3, true), new(added.Id, 2, null, false, RequiresCompletion: true)]) };

        var swapped = AssessmentDefinitionSnapshot.Read(
            AssessmentDefinitionSnapshot.SwapAssignedQuestion(snapshot.ToJson(), exam.Id, added.Id, reserve.Id), "exam", exam.Id);
        Assert.Equal(3, swapped.Revision!.Answers.Single(a => a.QuestionId == original.Id).AwardedPoints);
        var required = Assert.Single(swapped.Revision.Answers, a => a.RequiresCompletion);
        Assert.Equal(reserve.Id, required.QuestionId);
        Assert.Equal(3, required.MaximumPoints);
        Assert.DoesNotContain(swapped.Questions, q => q.Id == added.Id);
    }

    [Fact]
    public void ManualGradeUpdateDoesNotResolveUnansweredCompletionOrExcludedQuestion()
    {
        var graded = Guid.NewGuid();
        var pending = Guid.NewGuid();
        var excluded = Guid.NewGuid();
        var snapshot = AssessmentDefinitionSnapshot.FromExam(new Exam()) with
        { Revision = new([new(graded, 4, null, false), new(pending, 4, null, false, RequiresCompletion: true),
            new(excluded, 4, 3, true, Excluded: true)]) };
        var updated = snapshot.WithGrades(new Dictionary<Guid, (decimal, bool)>
        { [graded] = (4, true), [pending] = (4, true), [excluded] = (0, true) });
        Assert.True(updated.Revision!.RequiresCompletion);
        Assert.False(updated.Revision.RequiresReview);
        Assert.Equal(4, updated.Revision.AwardedPoints);
        Assert.Null(updated.Revision.Answers.Single(a => a.QuestionId == pending).AwardedPoints);
        Assert.Equal(3, updated.Revision.Answers.Single(a => a.QuestionId == excluded).AwardedPoints);
    }

    [Fact]
    public void EquivalentDatabaseCollectionOrderDoesNotInvalidateRevisionSnapshot()
    {
        var bank = new QuestionBankItem
        { Options = [new() { Text = "A", IsCorrect = true }, new() { Text = "B" }] };
        var exam = new Exam
        {
            ExamQuestions = [new() { Question = bank, Points = 4 },
                new() { Question = new QuestionBankItem(), Points = 2 }]
        };
        var before = AssessmentDefinitionSnapshot.FromExam(exam).ToJson();
        bank.Options = bank.Options.Reverse().ToList();
        exam.ExamQuestions = exam.ExamQuestions.Reverse().ToList();
        Assert.Equal(before, AssessmentDefinitionSnapshot.FromExam(exam).ToJson());
    }

    [Theory]
    [InlineData(QuestionType.MCQ)]
    [InlineData(QuestionType.Essay)]
    [InlineData(QuestionType.FindTheMistake)]
    public void ExamSnapshotRetainsAssignedQuestionAndOriginalRubricAfterLiveEdits(QuestionType type)
    {
        QuestionBankItem bank = type switch
        {
            QuestionType.Essay => new EssayQuestion(),
            QuestionType.FindTheMistake => new FindTheMistakeQuestion
            { BaseText = "original mistake", MistakeStartIndex = 9, MistakeEndIndex = 16 },
            _ => new QuestionBankItem()
        };
        bank.Type = type;
        bank.Text = "Original question";
        bank.WrittenCorrection = "Original rubric";
        bank.ImageUrl = "/original.png";
        bank.Options = [new() { Text = "Original option", IsCorrect = true }];
        var assigned = new ExamQuestion { Question = bank, QuestionBankItemId = bank.Id, Points = 5 };
        var unassigned = new ExamQuestion { Question = new QuestionBankItem(), Points = 8 };
        var exam = new Exam
        {
            Title = "Original exam", TotalScore = 20, PassingScore = 12, DurationMinutes = 30,
            ExamQuestions = [assigned, unassigned]
        };
        var json = AssessmentDefinitionSnapshot.FromExam(exam, [assigned]).ToJson();
        exam.Title = "Changed exam";
        exam.TotalScore = 100;
        exam.PassingScore = 80;
        exam.DurationMinutes = 1;
        bank.Text = "Changed question";
        bank.WrittenCorrection = "Changed rubric";
        bank.Options.First().Text = "Changed option";
        assigned.Points = 50;

        var saved = AssessmentDefinitionSnapshot.ResolveExam(exam, json);
        var question = Assert.Single(saved.ExamQuestions);
        Assert.Equal(assigned.Id, question.Id);
        Assert.Equal(bank.Id, question.Question.Id);
        Assert.Equal(type, question.Question.Type);
        Assert.Equal(5, question.Points);
        Assert.Equal("Original question", question.Question.Text);
        Assert.Equal("Original rubric", question.Question.WrittenCorrection);
        Assert.Equal("Original option", Assert.Single(question.Question.Options).Text);
        Assert.Equal(20, saved.TotalScore);
        Assert.Equal(12, saved.PassingScore);
        Assert.Equal(30, saved.DurationMinutes);
        Assert.Equal("Original exam", saved.Title);
        Assert.Equal(100, exam.TotalScore);
        if (type == QuestionType.FindTheMistake)
            Assert.Equal("original mistake", Assert.IsType<FindTheMistakeQuestion>(question.Question).BaseText);
    }

    [Fact]
    public void HomeworkSnapshotRetainsOptionsAndPassingThresholdWithoutMutatingLiveDefinition()
    {
        var question = new HomeworkQuestion
        {
            BodyText = "Original", PointsActive = 4, CorrectAnswerKey = "A",
            PossibleAnswers = ["A", "B"], WrittenCorrection = "Original correction"
        };
        var homework = new HomeworkEntity
        { Title = "Original homework", TotalScore = 4, PassingScoreThreshold = 2, Questions = [question] };
        var json = AssessmentDefinitionSnapshot.FromHomework(homework).ToJson();
        question.PossibleAnswers[0] = "C";
        question.CorrectAnswerKey = "B";
        homework.PassingScoreThreshold = 4;
        homework.Questions.Clear();

        var saved = AssessmentDefinitionSnapshot.ResolveHomework(homework, json);
        var savedQuestion = Assert.Single(saved.Questions);
        Assert.Equal("A", savedQuestion.CorrectAnswerKey);
        Assert.Equal(new[] { "A", "B" }, savedQuestion.PossibleAnswers);
        Assert.Equal(2, saved.PassingScoreThreshold);
        Assert.Empty(homework.Questions);
    }

    [Fact]
    public void SnapshotFromAnotherAssessmentIsRejectedInsteadOfShowingWrongAnswers()
    {
        var first = new Exam();
        var json = AssessmentDefinitionSnapshot.FromExam(first).ToJson();
        Assert.Throws<InvalidOperationException>(() => AssessmentDefinitionSnapshot.ResolveExam(new Exam(), json));
        Assert.Throws<InvalidOperationException>(() => AssessmentDefinitionSnapshot.ResolveHomework(new HomeworkEntity { Id = first.Id }, json));
    }
}
