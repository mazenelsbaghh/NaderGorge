using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Exams.Queries;
using NaderGorge.Application.Features.Webhooks.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class ExamEssayGradingModeTests
{
    [Theory]
    [InlineData(ExamEssayGradingMode.Automatic, true, "Pending")]
    [InlineData(ExamEssayGradingMode.Automatic, false, "Pending")]
    [InlineData(ExamEssayGradingMode.Manual, true, "PartiallyGraded")]
    [InlineData(ExamEssayGradingMode.Manual, false, "PartiallyGraded")]
    [InlineData(ExamEssayGradingMode.ModelAnswerOnly, true, "Completed")]
    [InlineData(ExamEssayGradingMode.ModelAnswerOnly, false, "Completed")]
    public async Task Submitted_exam_uses_saved_mode_after_live_setting_changes(
        ExamEssayGradingMode mode, bool includeEssay, string expectedState)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "601");
        var (exam, mcq, essayQuestion, _, _, correct, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        exam.EssayGradingMode = mode;
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        attempt.DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam, exam.ExamQuestions).ToJson();
        exam.EssayGradingMode = mode == ExamEssayGradingMode.Automatic
            ? ExamEssayGradingMode.Manual : ExamEssayGradingMode.Automatic;
        await db.SaveChangesAsync();
        List<AnswerSubmissionDto> answers = [new(mcq.Id, correct.Id, null)];
        if (includeEssay) answers.Add(new(essayQuestion.Id, null, "Gravity attracts masses."));

        var submitted = await new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer())
            .Handle(new(exam.Id, attempt.Id, student.Id, answers), default);

        Assert.True(submitted.Success, submitted.Message);
        Assert.Equal(expectedState, submitted.Data!.ResultState);
        db.ChangeTracker.Clear();
        var reopened = await new GetExamAttemptResultQueryHandler(db).Handle(new(attempt.Id, student.Id), default);
        Assert.True(reopened.Success, reopened.Message);
        Assert.Equal(expectedState, reopened.Data!.ResultState);
        var review = reopened.Data.Questions.Single(q => q.ExamQuestionId == essayQuestion.Id);
        var savedAnswer = await db.StudentAnswers.SingleAsync(a => a.StudentExamAttemptId == attempt.Id
            && a.ExamQuestionId == essayQuestion.Id);
        Assert.Equal(includeEssay ? "Gravity attracts masses." : null, savedAnswer.SubmittedText);
        Assert.Equal(mode == ExamEssayGradingMode.ModelAnswerOnly, savedAnswer.IsExcludedFromScore);
        if (mode == ExamEssayGradingMode.ModelAnswerOnly)
        {
            Assert.Equal(2m, reopened.Data.TotalScore);
            Assert.Equal(2m, reopened.Data.ScoreAchieved);
            Assert.True(reopened.Data.IsPassed);
            Assert.True(review.IsExcludedFromScore);
            Assert.False(review.IsCorrect);
            Assert.Equal("A force attracting masses.", review.CorrectOptionText);
            Assert.Empty(await db.EssaySubmissions.ToListAsync());
        }
        else
        {
            Assert.Equal(10m, reopened.Data.TotalScore);
            Assert.False(reopened.Data.IsPassed);
            Assert.Null(review.CorrectOptionText);
            var essay = Assert.Single(await db.EssaySubmissions.ToListAsync());
            Assert.Equal(mode == ExamEssayGradingMode.Manual ? EssaySubmissionStatus.WaitTeacher
                : EssaySubmissionStatus.WaitAI, essay.Status);
        }
        Assert.Equal(mode == ExamEssayGradingMode.Automatic ? 1 : 0,
            await db.OutboxEvents.CountAsync(e => e.Type == "EssayEvaluationQueued"));
    }

    [Fact]
    public async Task Manual_attempt_ignores_late_ai_callback_after_live_mode_changes()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "602");
        var (exam, mcq, essayQuestion, _, _, correct, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        exam.EssayGradingMode = ExamEssayGradingMode.Manual;
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        await new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer())
            .Handle(new(exam.Id, attempt.Id, student.Id, [new(mcq.Id, correct.Id, null), new(essayQuestion.Id, null, "Answer")]), default);
        var essay = await db.EssaySubmissions.SingleAsync();
        // A previously delivered job can return after an operator changes the exam.
        essay.Status = EssaySubmissionStatus.WaitAI;
        exam.EssayGradingMode = ExamEssayGradingMode.Automatic;
        await db.SaveChangesAsync();

        var response = await new WebhookEssayGradedCommandHandler(db).Handle(new(essay.Id, 1, "Correct"), default);

        Assert.True(response.Success);
        db.ChangeTracker.Clear();
        var saved = await db.EssaySubmissions.SingleAsync();
        Assert.Equal(EssaySubmissionStatus.WaitTeacher, saved.Status);
        Assert.Null(saved.TeacherFinalScore);
        Assert.Null(saved.AiInitialScore);
    }

    [Theory]
    [InlineData(PreviousAttemptsPolicy.Preserve, 10, true)]
    [InlineData(PreviousAttemptsPolicy.Regrade, 2, false)]
    public void Model_answer_only_revision_honors_history_policy(
        PreviousAttemptsPolicy history, decimal total, bool needsReview)
    {
        var mcq = new ExamQuestion { Question = new QuestionBankItem(), Points = 2 };
        var essay = new ExamQuestion { Question = new EssayQuestion { Type = QuestionType.Essay }, Points = 8 };
        var exam = new Exam { TotalScore = 10, PassingScore = 5, ExamQuestions = [mcq, essay] };
        var previous = AssessmentDefinitionSnapshot.FromExam(exam);
        var proposed = previous with { EssayGradingMode = ExamEssayGradingMode.ModelAnswerOnly };

        var revision = AssessmentAttemptRegrader.Regrade(previous,
            [new(mcq.Id, null, null, 0, false), new(essay.Id, "Answer", null, null, false)],
            new(previous, proposed, new(history)));

        Assert.Equal(total, revision.Definition.TotalScore);
        Assert.Equal(needsReview, revision.Grades.RequiresReview);
        Assert.Equal(history == PreviousAttemptsPolicy.Regrade,
            revision.Grades.Answers.Single(a => a.QuestionId == essay.Id).Excluded);
    }

    [Theory]
    [InlineData(ExamEssayGradingMode.Automatic, false)]
    [InlineData(ExamEssayGradingMode.Manual, false)]
    [InlineData(ExamEssayGradingMode.Automatic, true)]
    [InlineData(ExamEssayGradingMode.Manual, true)]
    public async Task Reenabling_grading_preserves_unsubmitted_attempts_and_reviews_submitted_essays(
        ExamEssayGradingMode mode, bool submitted)
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "603");
        var (exam, mcq, essayQuestion, _, _, correct, _) = await TestAppDbContextFactory.SeedEssayExamAsync(db);
        exam.EssayGradingMode = ExamEssayGradingMode.ModelAnswerOnly;
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);
        attempt.DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(exam, exam.ExamQuestions).ToJson();
        await db.SaveChangesAsync();
        List<AnswerSubmissionDto> provided = [new(mcq.Id, correct.Id, null), new(essayQuestion.Id, null, "Saved essay")];
        var submit = new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer());
        if (submitted) Assert.True((await submit.Handle(new(exam.Id, attempt.Id, student.Id, provided), default)).Success);
        var previous = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", exam.Id);
        var proposed = AssessmentDefinitionSnapshot.FromExam(exam) with { EssayGradingMode = mode };
        var policy = new AssessmentRevisionPolicy(PreviousAttemptsPolicy.Regrade);
        var recorded = attempt.Answers.Select(a => new RecordedAssessmentAnswer(a.ExamQuestionId,
            a.SubmittedText, a.SelectedOptionId, a.ExamQuestionId == essayQuestion.Id ? null : a.PointsAwarded, false)).ToArray();
        var revised = AssessmentAttemptRegrader.Regrade(previous, recorded, new(previous, proposed, policy));

        new AssessmentRevisionPersistence(db).ApplyExam(attempt, [],
            new(revised, new(proposed, policy, new Dictionary<Guid, Guid>()), student.Id, Guid.NewGuid()));
        await db.SaveChangesAsync();

        if (submitted)
        {
            var essay = Assert.Single(await db.EssaySubmissions.ToListAsync());
            Assert.Equal("Saved essay", essay.AnswerText);
            Assert.Equal(EssaySubmissionStatus.WaitTeacher, essay.Status);
            Assert.Null(essay.TeacherFinalScore);
        }
        else
        {
            Assert.Empty(await db.EssaySubmissions.ToListAsync());
            var response = await submit.Handle(new(exam.Id, attempt.Id, student.Id, provided), default);
            Assert.True(response.Success, response.Message);
            Assert.Equal(mode == ExamEssayGradingMode.Automatic ? "Pending" : "PartiallyGraded", response.Data!.ResultState);
        }
        Assert.False((await db.StudentAnswers.SingleAsync(a => a.StudentExamAttemptId == attempt.Id
            && a.ExamQuestionId == essayQuestion.Id)).IsExcludedFromScore);
    }

    [Fact]
    public void Essay_only_exam_without_grading_has_no_scored_points()
    {
        var essay = new ExamQuestion { Question = new EssayQuestion { Type = QuestionType.Essay }, Points = 8 };
        var exam = new Exam { TotalScore = 100, PassingScore = 50, EssayGradingMode = ExamEssayGradingMode.ModelAnswerOnly,
            ExamQuestions = [essay] };

        var snapshot = AssessmentDefinitionSnapshot.FromExam(exam, [essay]);

        Assert.Equal(0, snapshot.TotalScore);
        Assert.Equal(0, snapshot.PassingScore);
        Assert.Equal(0, Assert.Single(snapshot.Questions).Points);
    }
}
