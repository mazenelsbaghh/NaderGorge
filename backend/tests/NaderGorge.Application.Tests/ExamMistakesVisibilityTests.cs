using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class ExamMistakesVisibilityTests
{
    [Fact]
    public async Task GradeOnlyExamDoesNotLeakAnswersThroughMistakesEvenWhenTheStudentPassed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var owner = new User { FullName = "Teacher", PhoneNumber = "73001", PasswordHash = "test" };
        var student = new User { FullName = "Student", PhoneNumber = "73002", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = owner, Specialization = "History", ContactInfo = "test" };
        var exam = new Exam { CreatedByTeacher = teacher, Title = "Released mistakes", TotalScore = 1, PassingScore = 0, ShowAnswersToStudents = false };
        var question = new QuestionBankItem { Text = "Hidden question", Type = QuestionType.MCQ,
            CreatedByTeacher = teacher, Subject = new Subject { Name = "History", NormalizedName = "VISIBILITY_TEST", Description = "History" } };
        var correct = new QuestionOption { Question = question, Text = "Hidden model answer", IsCorrect = true };
        var wrong = new QuestionOption { Question = question, Text = "Saved student answer", IsCorrect = false };
        question.Options.Add(correct);question.Options.Add(wrong);
        var examQuestion = new ExamQuestion { Exam = exam, Question = question, Order = 1, Points = 1 };
        exam.ExamQuestions.Add(examQuestion);
        var snapshot = AssessmentDefinitionSnapshot.FromExam(exam).ToJson();
        var attempt = new StudentExamAttempt { Exam = exam, User = student, SubmittedAt = DateTime.UtcNow,
            ScoreAchieved = 0, IsPassed = true, Evaluation = "Passed", DefinitionSnapshotJson = snapshot };
        db.AddRange(exam, attempt, new StudentAnswer { Attempt = attempt, ExamQuestion = examQuestion,
            SelectedOption = wrong, IsCorrect = false, PointsAwarded = 0 },
            new StudentProfile { User = student, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.FirstSecondary },
            new StudentFacingAcademicScope { OwnerType = StudentFacingScopeOwnerType.Exam, OwnerId = exam.Id, ScopeLevel = AcademicScopeLevel.PlatformWide });
        await db.SaveChangesAsync();
        var scope = new AcademicScopeService(db);
        var handler = new GetMistakesQueryHandler(db, scope, new AccessCheckService(db, scope), new ContentArchiveAccessService(db));

        var hidden = await handler.Handle(new(student.Id), default);
        Assert.True(hidden.Success, hidden.Message);
        Assert.Equal(0, hidden.Data!.TotalExamMistakes);
        Assert.Equal(0, hidden.Data.ExamsWithMistakes);
        Assert.Empty(hidden.Data.ExamMistakes);
        exam.ShowAnswersToStudents = true;
        await db.SaveChangesAsync();
        var released = await handler.Handle(new(student.Id), default);
        var review = Assert.Single(Assert.Single(released.Data!.ExamMistakes).Items);
        Assert.True(review.CanRevealCorrectAnswer);
        Assert.Equal(correct.Text, review.CorrectAnswer);
        Assert.Equal(wrong.Text, review.YourAnswer);
        exam.ShowAnswersToStudents = false;
        await db.SaveChangesAsync();
        Assert.Empty((await handler.Handle(new(student.Id), default)).Data!.ExamMistakes);
        Assert.Equal(snapshot, (await db.StudentExamAttempts.SingleAsync()).DefinitionSnapshotJson);
        Assert.Equal(0, (await db.StudentExamAttempts.SingleAsync()).ScoreAchieved);
    }
}
