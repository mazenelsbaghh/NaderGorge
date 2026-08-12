using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Essays;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Admin.Essays;

public sealed class GetPendingEssaysCommandTests
{
    [Fact]
    public async Task AdminScope_TransitionsAiScoresAndReturnsAllPendingInCreationOrder()
    {
        await using var db = CreateDb();
        var admin = new User { FullName = "Admin" };
        db.Users.Add(admin);
        var first = Essay(Guid.NewGuid(), EssaySubmissionStatus.AIScored, DateTime.UtcNow.AddMinutes(-1));
        var second = Essay(Guid.NewGuid(), EssaySubmissionStatus.WaitTeacher, DateTime.UtcNow);
        var graded = Essay(Guid.NewGuid(), EssaySubmissionStatus.TeacherGraded, DateTime.UtcNow.AddMinutes(1));
        db.EssaySubmissions.AddRange(first, second, graded);
        await db.SaveChangesAsync();

        var result = await new GetPendingEssaysCommandHandler(db)
            .Handle(new GetPendingEssaysCommand(admin.Id), default);

        Assert.Equal([first.Id, second.Id], result.Select(item => item.Id));
        Assert.Equal(EssaySubmissionStatus.WaitTeacher, first.Status);
        Assert.DoesNotContain(result, item => item.Id == graded.Id);
    }

    [Fact]
    public async Task TeacherScope_DoesNotReadOrMutateAnotherTeachersEssay()
    {
        await using var db = CreateDb();
        var teacherUser = new User { FullName = "Teacher" };
        var teacher = new TeacherProfile { User = teacherUser, UserId = teacherUser.Id };
        teacherUser.TeacherProfile = teacher;
        var role = new Role { Name = "Teacher", Type = RoleType.Teacher };
        teacherUser.UserRoles.Add(new UserRole { User = teacherUser, UserId = teacherUser.Id, Role = role, RoleId = role.Id });
        db.Users.Add(teacherUser);

        var owned = Essay(teacher.Id, EssaySubmissionStatus.AIScored, DateTime.UtcNow);
        var foreign = Essay(Guid.NewGuid(), EssaySubmissionStatus.AIScored, DateTime.UtcNow);
        db.EssaySubmissions.AddRange(owned, foreign);
        await db.SaveChangesAsync();

        var result = await new GetPendingEssaysCommandHandler(db)
            .Handle(new GetPendingEssaysCommand(teacherUser.Id), default);

        Assert.Equal(owned.Id, Assert.Single(result).Id);
        Assert.Equal(EssaySubmissionStatus.WaitTeacher, owned.Status);
        Assert.Equal(EssaySubmissionStatus.AIScored, foreign.Status);
    }

    private static EssaySubmission Essay(Guid creatorId, EssaySubmissionStatus status, DateTime createdAt)
    {
        var question = new EssayQuestion { CreatedByTeacherId = creatorId, Text = "Question" };
        return new EssaySubmission
        {
            StudentId = Guid.NewGuid(),
            Question = question,
            QuestionId = question.Id,
            StudentExamAttemptId = Guid.NewGuid(),
            AnswerText = "Answer",
            Status = status,
            CreatedAt = createdAt
        };
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"pending-essays-{Guid.NewGuid()}").Options);
}
