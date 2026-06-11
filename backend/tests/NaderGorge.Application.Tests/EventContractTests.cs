using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Codes.Commands;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class EventContractTests
{
    [Fact]
    public async Task BulkCodeGeneration_EmitsTargetedExportEventWithoutPlaintextCodes()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin", "700");
        await SeedTeacherAsync(db);
        var handler = new BulkGenerateCodesCommandHandler(db, new NoOpAuditService());

        var response = await handler.Handle(new BulkGenerateCodesCommand(
            GroupName: "Balance Codes",
            CodeType: CodeType.Balance,
            Count: 2,
            CodeLength: 6,
            AdminId: admin.Id,
            BalanceAmount: 100m), CancellationToken.None);

        Assert.True(response.Success);
        var exportEvent = await db.OutboxEvents.SingleAsync(
            outboxEvent => outboxEvent.Type == "CodeGroupExportReady");
        Assert.Equal(admin.Id.ToString(), exportEvent.TargetUserId);
        Assert.Null(exportEvent.TargetGroup);

        using var payload = JsonDocument.Parse(exportEvent.PayloadJson);
        var root = payload.RootElement;
        Assert.True(root.TryGetProperty("codeGroupId", out _));
        Assert.Equal("Ready", root.GetProperty("exportStatus").GetString());
        Assert.False(root.TryGetProperty("codes", out _));
    }

    [Fact]
    public async Task BalanceChanged_HasCorrectPayloadSchema()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "702");
        var balanceService = new BalanceService(db, new TestLogger<BalanceService>());

        await balanceService.AddCredit(user.Id, 150m, "Redemption bonus", Guid.NewGuid(), CancellationToken.None);

        var balanceEvent = await db.OutboxEvents.SingleAsync(
            outboxEvent => outboxEvent.Type == "BalanceChanged");
        Assert.Equal(user.Id.ToString(), balanceEvent.TargetUserId);
        Assert.Null(balanceEvent.TargetGroup);

        using var payload = JsonDocument.Parse(balanceEvent.PayloadJson);
        var root = payload.RootElement;
        Assert.True(root.TryGetProperty("newBalance", out var newBalanceProp));
        Assert.Equal(150m, newBalanceProp.GetDecimal());
        Assert.True(root.TryGetProperty("formattedBalance", out var formattedProp));
        Assert.Equal("150.00 جنيها", formattedProp.GetString());
    }

    [Fact]
    public async Task CodeActivated_And_PackageAccessGranted_HaveCorrectPayloadSchema()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "703");
        var (packageId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Math Package");

        var group = new CodeGroup
        {
            Id = Guid.NewGuid(),
            Name = "Promo Group",
            CodeType = CodeType.Package,
            PackageId = packageId,
            TotalCodes = 1,
            CreatedByUserId = user.Id,
            TeacherId = Guid.NewGuid()
        };
        db.CodeGroups.Add(group);

        var code = new AccessCode
        {
            Id = Guid.NewGuid(),
            CodeGroupId = group.Id,
            CodePlaintext = "MATH123456",
            CodeHash = "hash",
            IsConsumed = false,
            SerialNumber = 1
        };
        db.AccessCodes.Add(code);
        await db.SaveChangesAsync();

        var balanceService = new BalanceService(db, new TestLogger<BalanceService>());
        var handler = new ActivateCodeCommandHandler(db, balanceService);

        var result = await handler.Handle(new ActivateCodeCommand(user.Id, "MATH123456"), CancellationToken.None);
        Assert.True(result.Success);

        var codeActivatedEvent = await db.OutboxEvents.FirstAsync(e => e.Type == "CodeActivated");
        Assert.Equal(user.Id.ToString(), codeActivatedEvent.TargetUserId);
        using var payload1 = JsonDocument.Parse(codeActivatedEvent.PayloadJson);
        var root1 = payload1.RootElement;
        Assert.Equal("Package", root1.GetProperty("codeType").GetString());
        Assert.True(root1.TryGetProperty("referenceId", out _));
        Assert.Equal("تم تفعيل الباكدج بنجاح!", root1.GetProperty("message").GetString());

        var packageAccessEvent = await db.OutboxEvents.FirstAsync(e => e.Type == "PackageAccessGranted");
        Assert.Equal(user.Id.ToString(), packageAccessEvent.TargetUserId);
        using var payload2 = JsonDocument.Parse(packageAccessEvent.PayloadJson);
        var root2 = payload2.RootElement;
        Assert.Equal(user.Id, root2.GetProperty("userId").GetGuid());
        Assert.Equal(packageId, root2.GetProperty("packageId").GetGuid());
    }

    [Fact]
    public async Task ExamSubmitted_Graded_ResultReady_HaveCorrectPayloadSchema()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "704");
        var (exam, examQuestion, question, correctOption, _) = await TestAppDbContextFactory.SeedFindTheMistakeExamAsync(db);
        var attempt = await TestAppDbContextFactory.SeedAttemptAsync(db, exam.Id, student.Id);

        var handler = new SubmitExamCommandHandler(db, new NoOpPublisher(), new FakeJobEnqueuer());
        var result = await handler.Handle(
            new SubmitExamCommand(exam.Id, attempt.Id, student.Id, new List<AnswerSubmissionDto>
            {
                new(examQuestion.Id, null, null, "rise")
            }),
            CancellationToken.None);

        Assert.True(result.Success);

        var submittedEvent = await db.OutboxEvents.FirstAsync(e => e.Type == "ExamSubmitted");
        Assert.Equal(student.Id.ToString(), submittedEvent.TargetUserId);
        using var payloadSub = JsonDocument.Parse(submittedEvent.PayloadJson);
        var rootSub = payloadSub.RootElement;
        Assert.Equal(exam.Id, rootSub.GetProperty("examId").GetGuid());
        Assert.Equal(attempt.Id, rootSub.GetProperty("attemptId").GetGuid());
        Assert.True(rootSub.GetProperty("isPassed").GetBoolean());
        Assert.Equal(2m, rootSub.GetProperty("score").GetDecimal());

        var gradedEvent = await db.OutboxEvents.FirstAsync(e => e.Type == "ExamGraded");
        Assert.Equal(student.Id.ToString(), gradedEvent.TargetUserId);
        using var payloadGraded = JsonDocument.Parse(gradedEvent.PayloadJson);
        var rootGraded = payloadGraded.RootElement;
        Assert.Equal(exam.Id, rootGraded.GetProperty("examId").GetGuid());
        Assert.Equal(attempt.Id, rootGraded.GetProperty("attemptId").GetGuid());
        Assert.True(rootGraded.GetProperty("isPassed").GetBoolean());
        Assert.Equal(2m, rootGraded.GetProperty("score").GetDecimal());
        Assert.True(rootGraded.TryGetProperty("evaluation", out _));

        var resultReadyEvent = await db.OutboxEvents.FirstAsync(e => e.Type == "ExamResultReady");
        Assert.Equal(student.Id.ToString(), resultReadyEvent.TargetUserId);
        using var payloadResult = JsonDocument.Parse(resultReadyEvent.PayloadJson);
        var rootResult = payloadResult.RootElement;
        Assert.Equal(exam.Id, rootResult.GetProperty("examId").GetGuid());
        Assert.Equal(attempt.Id, rootResult.GetProperty("attemptId").GetGuid());
        Assert.True(rootResult.GetProperty("isPassed").GetBoolean());
        Assert.Equal(2m, rootResult.GetProperty("score").GetDecimal());
    }

    private static async Task SeedTeacherAsync(AppDbContext db)
    {
        var teacherUser = new User
        {
            FullName = "Teacher",
            PhoneNumber = "701",
            PasswordHash = "hashed"
        };
        db.Users.Add(teacherUser);
        db.TeacherProfiles.Add(new TeacherProfile
        {
            UserId = teacherUser.Id,
            Bio = "Teacher bio",
            Specialization = "Physics",
            ContactInfo = "701"
        });
        await db.SaveChangesAsync();
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(
            string action,
            string entityType,
            Guid? entityId,
            Guid? userId,
            object? oldValues = null,
            object? newValues = null,
            string? ipAddress = null,
            string? correlationId = null)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {}
    }
}

