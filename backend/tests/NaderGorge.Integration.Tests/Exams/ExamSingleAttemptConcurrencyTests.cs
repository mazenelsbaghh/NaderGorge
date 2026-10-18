using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.Exams;

public sealed class ExamSingleAttemptConcurrencyTests
{
    [Fact]
    public async Task ConcurrentStartsOfASingleAttemptExamReturnTheSamePersistedAttempt()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        Guid studentId;
        Guid examId;
        await using (var seed = fixture.CreateDbContext())
        {
            await seed.Database.MigrateAsync();
            var student = new User { FullName = "Attempt concurrency preview", PhoneNumber = "single-preview", PasswordHash = "test" };
            var teacher = new TeacherProfile { User = new User { FullName = "Attempt teacher", PhoneNumber = "single-teacher", PasswordHash = "test" } };
            var bank = new QuestionBankItem
            {
                Text = "Choose", CreatedByTeacher = teacher,
                Subject = new Subject { Name = "Attempt policy", NormalizedName = "ATTEMPT POLICY", Description = "Synthetic test subject" },
                Options = [new QuestionOption { Text = "Correct", IsCorrect = true, Order = 1 }, new QuestionOption { Text = "Wrong", Order = 2 }]
            };
            var exam = new Exam
            {
                Title = "Single attempt concurrency", CreatedByTeacher = teacher, SingleAttemptOnly = true,
                PassingScore = 1, TotalScore = 1, DurationMinutes = 5,
                ExamQuestions = [new ExamQuestion { Question = bank, Points = 1, Order = 1 }]
            };
            seed.Exams.Add(exam);
            seed.UserRoles.Add(new UserRole { User = student, Role = await seed.Roles.SingleAsync(role => role.Type == RoleType.Admin) });
            await seed.SaveChangesAsync();
            studentId = student.Id;
            examId = exam.Id;
        }
        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var starts = await Task.WhenAll(
            new StartExamAttemptCommandHandler(first, new AccessCheckService(first, new AcademicScopeService(first)))
                .Handle(new(examId, studentId), default),
            new StartExamAttemptCommandHandler(second, new AccessCheckService(second, new AcademicScopeService(second)))
                .Handle(new(examId, studentId), default));

        Assert.All(starts, start => Assert.True(start.Success, start.Message));
        Assert.Equal(starts[0].Data!.AttemptId, starts[1].Data!.AttemptId);
        await using var check = fixture.CreateDbContext();
        var attempt = Assert.Single(await check.StudentExamAttempts.Where(a => a.UserId == studentId && a.ExamId == examId).ToListAsync());
        var definition = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", examId);
        attempt.SubmittedAt = DateTime.UtcNow.AddMinutes(-1);
        attempt.DefinitionSnapshotJson = (definition with
        {
            RevisionId = Guid.NewGuid(),
            Revision = new AttemptRevisionPlan([new RevisionAnswer(definition.Questions[0].Id, 1, null, false, RequiresCompletion: true)])
        }).ToJson();
        await check.SaveChangesAsync();

        var completion = await new StartExamAttemptCommandHandler(check, new AccessCheckService(check, new AcademicScopeService(check)))
            .Handle(new(examId, studentId), default);
        Assert.True(completion.Success, completion.Message);
        Assert.Equal(attempt.Id, completion.Data!.AttemptId);
        Assert.NotNull(completion.Data.RevisionId);
    }
}
