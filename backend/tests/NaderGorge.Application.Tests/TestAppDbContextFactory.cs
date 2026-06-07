using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using ProgramEntity = NaderGorge.Domain.Entities.Program;

namespace NaderGorge.Application.Tests;

internal static class TestAppDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    public static async Task<(Guid PackageId, Guid ProgramId)> SeedPackageAsync(
        AppDbContext db,
        string packageName,
        string? description = null,
        decimal price = 100,
        bool isActive = true)
    {
        var program = new ProgramEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{packageName} Program",
            Description = "Test Program",
            TargetGrade = "3rd Secondary",
        };

        var package = new NaderGorge.Domain.Entities.Package
        {
            Id = Guid.NewGuid(),
            Name = packageName,
            Description = description ?? $"{packageName} description",
            Price = price,
            IsActive = isActive,
            ProgramId = program.Id,
        };

        db.Programs.Add(program);
        db.Packages.Add(package);
        await db.SaveChangesAsync();

        return (package.Id, program.Id);
    }

    public static async Task<User> SeedUserAsync(AppDbContext db, string fullName, string phoneNumber)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            PhoneNumber = phoneNumber,
            PasswordHash = "hashed"
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<CommunityPost> SeedApprovedCommunityPostAsync(AppDbContext db, User author, string body = "Approved post")
    {
        var post = new CommunityPost
        {
            Id = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Body = body,
            Status = CommunityPostStatus.Approved
        };

        db.CommunityPosts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    public static async Task<(Exam Exam, ExamQuestion ExamQuestion, FindTheMistakeQuestion Question, QuestionOption CorrectOption, QuestionOption WrongOption)> SeedFindTheMistakeExamAsync(
        AppDbContext db)
    {
        var question = new FindTheMistakeQuestion
        {
            Id = Guid.NewGuid(),
            Text = "اكتشف الغلطة",
            Type = QuestionType.FindTheMistake,
            DefaultPoints = 2,
            Tags = "grammar",
            BaseText = "The sun rise in the east",
            MistakeStartIndex = 8,
            MistakeEndIndex = 12
        };

        var correctOption = new QuestionOption
        {
            Id = Guid.NewGuid(),
            QuestionBankItemId = question.Id,
            Question = question,
            Text = "rise",
            IsCorrect = true
        };

        var wrongOption = new QuestionOption
        {
            Id = Guid.NewGuid(),
            QuestionBankItemId = question.Id,
            Question = question,
            Text = "Dummy",
            IsCorrect = false
        };

        question.Options.Add(correctOption);
        question.Options.Add(wrongOption);

        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            Title = "Find The Mistake Exam",
            Description = "Test exam",
            PassingScore = 1,
            TotalScore = 2
        };

        var examQuestion = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            ExamId = exam.Id,
            Exam = exam,
            QuestionBankItemId = question.Id,
            Question = question,
            Order = 1,
            Points = 2
        };

        exam.ExamQuestions.Add(examQuestion);
        db.QuestionBankItems.Add(question);
        db.QuestionOptions.AddRange(correctOption, wrongOption);
        db.Exams.Add(exam);
        db.ExamQuestions.Add(examQuestion);
        await db.SaveChangesAsync();

        return (exam, examQuestion, question, correctOption, wrongOption);
    }

    public static async Task<(Exam Exam, ExamQuestion McqExamQuestion, ExamQuestion EssayExamQuestion, QuestionBankItem McqQuestion, QuestionBankItem EssayQuestion, QuestionOption CorrectOption, QuestionOption WrongOption)> SeedEssayExamAsync(
        AppDbContext db)
    {
        var mcqQuestion = new QuestionBankItem
        {
            Id = Guid.NewGuid(),
            Text = "2 + 2 = ?",
            Type = QuestionType.MCQ,
            DefaultPoints = 2,
            Tags = "math"
        };

        var correctOption = new QuestionOption
        {
            Id = Guid.NewGuid(),
            QuestionBankItemId = mcqQuestion.Id,
            Question = mcqQuestion,
            Text = "4",
            IsCorrect = true
        };

        var wrongOption = new QuestionOption
        {
            Id = Guid.NewGuid(),
            QuestionBankItemId = mcqQuestion.Id,
            Question = mcqQuestion,
            Text = "5",
            IsCorrect = false
        };

        mcqQuestion.Options.Add(correctOption);
        mcqQuestion.Options.Add(wrongOption);

        var essayQuestion = new QuestionBankItem
        {
            Id = Guid.NewGuid(),
            Text = "Explain gravity",
            Type = QuestionType.Essay,
            DefaultPoints = 8,
            Tags = "science",
            WrittenCorrection = "A force attracting masses."
        };

        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            Title = "Essay Exam",
            Description = "Essay workflow test",
            PassingScore = 5,
            TotalScore = 10
        };

        var mcqExamQuestion = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            ExamId = exam.Id,
            Exam = exam,
            QuestionBankItemId = mcqQuestion.Id,
            Question = mcqQuestion,
            Order = 1,
            Points = 2
        };

        var essayExamQuestion = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            ExamId = exam.Id,
            Exam = exam,
            QuestionBankItemId = essayQuestion.Id,
            Question = essayQuestion,
            Order = 2,
            Points = 8
        };

        exam.ExamQuestions.Add(mcqExamQuestion);
        exam.ExamQuestions.Add(essayExamQuestion);

        db.QuestionBankItems.AddRange(mcqQuestion, essayQuestion);
        db.QuestionOptions.AddRange(correctOption, wrongOption);
        db.Exams.Add(exam);
        db.ExamQuestions.AddRange(mcqExamQuestion, essayExamQuestion);
        await db.SaveChangesAsync();

        return (exam, mcqExamQuestion, essayExamQuestion, mcqQuestion, essayQuestion, correctOption, wrongOption);
    }

    public static async Task<StudentExamAttempt> SeedAttemptAsync(AppDbContext db, Guid examId, Guid userId)
    {
        var attempt = new StudentExamAttempt
        {
            Id = Guid.NewGuid(),
            ExamId = examId,
            UserId = userId,
            StartedAt = DateTime.UtcNow,
            ScoreAchieved = 0,
            IsPassed = false,
            IsTimeExpired = false
        };

        db.StudentExamAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var questions = await db.ExamQuestions.Where(eq => eq.ExamId == examId).ToListAsync();
        foreach (var q in questions)
        {
            db.StudentAnswers.Add(new StudentAnswer
            {
                Id = Guid.NewGuid(),
                StudentExamAttemptId = attempt.Id,
                ExamQuestionId = q.Id,
                HintUsed = false,
                IsCorrect = false,
                PointsAwarded = 0
            });
        }
        await db.SaveChangesAsync();

        return attempt;
    }
}
