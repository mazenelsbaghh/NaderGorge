using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Domain.Entities;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.Exams;

public class ExamSubmissionTimeMigrationTests
{
    [Fact]
    public async Task UpgradeUsesOnlyMatchingSubmissionEventsAndLeavesUnprovenTimesUnknown()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var student = new User { FullName = "Submission time test student", PhoneNumber = "e2e-submit-student", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = new User { FullName = "Submission time teacher", PhoneNumber = "e2e-submit-teacher", PasswordHash = "test" } };
        var exam = new Exam { Title = "Submission time migration", CreatedByTeacher = teacher };
        var submittedAt = new DateTime(2026, 10, 1, 21, 30, 0, DateTimeKind.Utc);
        var submitted = new StudentExamAttempt { User = student, Exam = exam, Evaluation = "جيد", UpdatedAt = submittedAt.AddDays(1) };
        var unknown = new StudentExamAttempt { User = student, Exam = exam, Evaluation = "جيد", UpdatedAt = submittedAt.AddDays(2) };
        db.AddRange(submitted, unknown);
        db.OutboxEvents.AddRange(
            Submission(submitted.Id, exam.Id, submittedAt),
            Submission(submitted.Id, exam.Id, submittedAt.AddMinutes(1)),
            Submission(submitted.Id, Guid.NewGuid(), submittedAt.AddDays(-1)),
            new OutboxEvent { Type = "ExamSubmitted", PayloadJson = "not-json", CreatedAt = submittedAt.AddDays(-2) },
            new OutboxEvent { Type = "ExamGraded", PayloadJson = JsonSerializer.Serialize(new { attemptId = unknown.Id, examId = exam.Id }), CreatedAt = submittedAt });
        await db.SaveChangesAsync();
        var eventIds = await db.OutboxEvents.OrderBy(e => e.Id).Select(e => e.Id).ToListAsync();

        // Only this fixture's disposable database is downgraded to the preceding schema.
        await using (var previousSchema = fixture.CreateDbContext())
            await previousSchema.GetService<IMigrator>().MigrateAsync("20261016215839_PreserveExamOptionOrder");
        await using (var upgraded = fixture.CreateDbContext())
            await upgraded.Database.MigrateAsync();
        db.ChangeTracker.Clear();

        var attempts = await db.StudentExamAttempts.ToDictionaryAsync(a => a.Id);
        Assert.Equal(submittedAt, attempts[submitted.Id].SubmittedAt);
        Assert.Null(attempts[unknown.Id].SubmittedAt);
        Assert.Equal(submittedAt.AddDays(1), attempts[submitted.Id].UpdatedAt);
        Assert.Equal(eventIds, await db.OutboxEvents.OrderBy(e => e.Id).Select(e => e.Id).ToListAsync());
    }

    private static OutboxEvent Submission(Guid attemptId, Guid examId, DateTime occurredAt) => new()
    {
        Type = "ExamSubmitted",
        PayloadJson = JsonSerializer.Serialize(new { attemptId, examId }),
        CreatedAt = occurredAt
    };
}
