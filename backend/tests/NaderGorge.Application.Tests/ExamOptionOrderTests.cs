using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests;

// 2026-10-17: uploaded choices changed position on entry and when resuming an exam.
public sealed class ExamOptionOrderTests
{
    [Fact]
    public void Saved_attempt_keeps_authored_choice_order_and_correct_option_after_later_edits()
    {
        var (exam, question, first, second) = ExamWithChoices();
        var snapshot = AssessmentDefinitionSnapshot.FromExam(exam);
        first.Order = 2;
        second.Order = 1;
        first.IsCorrect = false;
        second.IsCorrect = true;

        var saved = AssessmentDefinitionSnapshot.ResolveExam(exam, snapshot.ToJson());
        var choices = Assert.Single(saved.ExamQuestions).Question.Options.OrderBy(o => o.Order).ToArray();

        Assert.Equal(new[] { first.Id, second.Id }, choices.Select(o => o.Id));
        Assert.True(choices[0].IsCorrect);
        Assert.False(choices[1].IsCorrect);
        Assert.Equal(question.Id, Assert.Single(saved.ExamQuestions).Id);
    }

    [Fact]
    public void Legacy_attempt_recovers_choice_positions_without_changing_saved_answers()
    {
        var (exam, _, first, second) = ExamWithChoices();
        var legacy = JsonNode.Parse(AssessmentDefinitionSnapshot.FromExam(exam).ToJson())!;
        var options = legacy["Questions"]![0]!["Options"]!.AsArray();
        foreach (var option in options) option!.AsObject().Remove("Order");
        var firstJson = options[0]!.DeepClone();
        var secondJson = options[1]!.DeepClone();
        options.Clear();
        options.Add(secondJson);
        options.Add(firstJson);

        var saved = AssessmentDefinitionSnapshot.ResolveExam(exam, legacy.ToJsonString());
        var choices = Assert.Single(saved.ExamQuestions).Question.Options.OrderBy(o => o.Order).ToArray();

        Assert.Equal(new[] { first.Id, second.Id }, choices.Select(o => o.Id));
        Assert.True(choices[0].IsCorrect);
        Assert.False(choices[1].IsCorrect);
    }

    [Fact]
    public void Editing_choices_persists_the_submitted_positions_without_replacing_option_ids()
    {
        var (exam, _, first, second) = ExamWithChoices();
        var definition = AssessmentDefinitionSnapshot.FromExam(exam);
        var question = definition.Questions.Single();
        definition = definition with { Questions = [question with { Options = question.Options.Reverse().ToArray() }] };

        AssessmentDefinitionWriter.ApplyExam(exam, definition, new(Guid.NewGuid(), Guid.NewGuid()));
        var reloaded = AssessmentDefinitionSnapshot.FromExam(exam).Questions.Single().Options;

        Assert.Equal(new[] { second.Id, first.Id }, reloaded.Select(o => o.Id));
        Assert.Equal(first.Id, Assert.Single(reloaded, o => o.IsCorrect).Id);
    }

    [Fact]
    public async Task Starting_and_resuming_returns_choices_in_uploaded_order_with_selected_identity_unchanged()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Preview student", "901");
        var previewRole = new Role { Name = "Admin", Type = RoleType.Admin };
        db.UserRoles.Add(new UserRole { User = student, Role = previewRole });
        var (exam, question, first, second) = ExamWithChoices();
        var third = new QuestionOption { Text = "C", Order = 3 };
        var fourth = new QuestionOption { Text = "D", Order = 4 };
        question.Question.Options.Add(fourth);
        question.Question.Options.Add(third);
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        var expected = new[] { first.Id, second.Id, third.Id, fourth.Id };
        var handler = new StartExamAttemptCommandHandler(db, new AccessCheckService(db, new AcademicScopeService(db)));

        var started = await handler.Handle(new(exam.Id, student.Id), default);
        Assert.True(started.Success, started.Message);
        Assert.Equal(expected, Assert.Single(started.Data!.Questions).Options.Select(o => o.Id));
        var answer = await db.StudentAnswers.SingleAsync();
        answer.SelectedOptionId = first.Id;
        await db.SaveChangesAsync();
        var resumed = await handler.Handle(new(exam.Id, student.Id), default);

        Assert.True(resumed.Success, resumed.Message);
        Assert.Equal(started.Data.AttemptId, resumed.Data!.AttemptId);
        Assert.Equal(expected, Assert.Single(resumed.Data.Questions).Options.Select(o => o.Id));
        Assert.Equal(first.Id, (await db.StudentAnswers.SingleAsync()).SelectedOptionId);
    }

    private static (Exam Exam, ExamQuestion Question, QuestionOption First, QuestionOption Second) ExamWithChoices()
    {
        var first = new QuestionOption { Id = Guid.Parse("ffffffff-0000-0000-0000-000000000001"), Text = "A", Order = 1, IsCorrect = true };
        var second = new QuestionOption { Id = Guid.Parse("00000001-0000-0000-0000-000000000001"), Text = "B", Order = 2 };
        var bank = new QuestionBankItem { Text = "Choose", Type = QuestionType.MCQ, Options = [second, first] };
        var question = new ExamQuestion { Question = bank, QuestionBankItemId = bank.Id, Points = 1, Order = 1 };
        var exam = new Exam { Title = "Uploaded order", TotalScore = 1, ExamQuestions = [question] };
        return (exam, question, first, second);
    }
}
