using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class AssessmentParentNotificationTests
{
    [Theory]
    [InlineData("FatherSecondary,FatherPrimary,Mother", "01012345678", "201012345678")]
    [InlineData("FatherSecondary,FatherPrimary,Mother", "invalid", "201112345678")]
    [InlineData("Mother,FatherPrimary,FatherSecondary", "01012345678", "201212345678")]
    public void ParentPriorityUsesFirstValidNumber(string order, string additional, string expected) =>
        Assert.Equal(expected, ParentWhatsAppRecipients.Resolve(new StudentProfile
        { SecondaryParentPhone = additional, ParentPhone = "01112345678", MotherPhone = "01212345678" }, order));

    [Theory]
    [InlineData("exam")]
    [InlineData("homework")]
    public async Task FinalResultUsesSnapshotScoreAndSendsOnlyOnce(string kind)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var handler = new RecordingMetaHandler();
        var dispatcher = Dispatcher(fixture.Db, handler);
        var notification = Event(seed, kind);
        await dispatcher.DispatchAsync(notification, default);
        await dispatcher.DispatchAsync(notification, default);
        // A separate grading event for the same attempt is also deduplicated.
        await dispatcher.DispatchAsync(Event(seed, kind), default);
        var sent = Assert.Single(handler.Requests);
        using var body = JsonDocument.Parse(sent);
        Assert.Equal("201012345678", body.RootElement.GetProperty("to").GetString());
        var parameters = body.RootElement.GetProperty("template").GetProperty("components")[0].GetProperty("parameters");
        Assert.Equal("8", parameters[0].GetProperty("text").GetString());
        Assert.Equal("10", parameters[1].GetProperty("text").GetString());
        var record = await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal(AssessmentParentDeliveryStatus.Sent, record.Status);
        Assert.Equal("wamid.test", record.MetaMessageId);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("disabled")]
    [InlineData("old_event")]
    [InlineData("opted_out")]
    [InlineData("template_changed")]
    public async Task IneligibleResultDoesNotContactParent(string reason)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var notification = Event(seed, "exam");
        if (reason == "pending") seed.Attempt.Evaluation = "قيد التصحيح";
        if (reason == "disabled") seed.Attempt.Exam.ParentNotificationSettingsJson = null;
        if (reason == "old_event") notification.CreatedAt = DateTime.UtcNow.AddDays(-1);
        if (reason == "template_changed") seed.Template.Fingerprint = new string('b', 64);
        if (reason == "opted_out") fixture.Db.WhatsAppContactPreferences.Add(new()
        {
            DestinationHash = Protector().DestinationHash("201012345678"),
            Category = WhatsAppContactPreferenceCategory.All, State = WhatsAppContactPreferenceState.OptedOut,
            EffectiveAt = DateTime.UtcNow.AddMinutes(-1), Source = "test"
        });
        await fixture.Db.SaveChangesAsync();
        var handler = new RecordingMetaHandler();
        await Dispatcher(fixture.Db, handler).DispatchAsync(notification, default);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AmbiguousProviderReplyIsNotResent()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var handler = new RecordingMetaHandler("{}");
        var dispatcher = Dispatcher(fixture.Db, handler);
        await dispatcher.DispatchAsync(Event(seed, "exam"), default);
        await dispatcher.DispatchAsync(Event(seed, "exam"), default);
        Assert.Single(handler.Requests);
        Assert.Equal(AssessmentParentDeliveryStatus.Uncertain,
            (await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync()).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task September30ProviderFailureIsRecordedWhetherReceiptPrecedesOrFollowsAcceptance(bool receiptFirst)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        await fixture.Db.LiveSupportWhatsAppPendingReceipts.Where(item => item.MetaMessageId == "wamid.test").ExecuteDeleteAsync();
        var seed = await Seed(fixture.Db);
        var handler = new RecordingMetaHandler();
        var dispatcher = Dispatcher(fixture.Db, handler);
        var receipt = new LiveSupportWhatsAppPendingReceipt
        {
            MetaMessageId = "wamid.test", Status = "Failed", ProviderTimestamp = DateTime.UtcNow,
            FailureCode = "WHATSAPP_CLOUD_131042", Version = 1
        };
        if (receiptFirst)
        {
            fixture.Db.LiveSupportWhatsAppPendingReceipts.Add(receipt);
            await fixture.Db.SaveChangesAsync();
        }
        await dispatcher.DispatchAsync(Event(seed, "exam"), default);
        if (!receiptFirst)
        {
            fixture.Db.LiveSupportWhatsAppPendingReceipts.Add(receipt);
            await fixture.Db.SaveChangesAsync();
            await AssessmentParentDeliveryReceipts.ReconcileAsync(fixture.Db, "wamid.test", default);
        }

        var failed = await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal(AssessmentParentDeliveryStatus.Failed, failed.Status);
        Assert.Equal("WHATSAPP_CLOUD_131042", failed.FailureCode);
        await dispatcher.DispatchAsync(Event(seed, "exam"), default);
        Assert.Single(handler.Requests);

        receipt.Status = "Read";
        receipt.FailureCode = null;
        receipt.Version++;
        receipt.ReadAt = DateTime.UtcNow;
        receipt.DeliveredAt = receipt.ReadAt;
        await fixture.Db.SaveChangesAsync();
        await AssessmentParentDeliveryReceipts.ReconcileAsync(fixture.Db, "wamid.test", default);
        var confirmed = await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal(AssessmentParentDeliveryStatus.Sent, confirmed.Status);
        Assert.Null(confirmed.FailureCode);
    }

    [Fact]
    public async Task ConcurrentGradingEventsClaimOneProviderSend()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        await using var secondDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString).Options);
        var handler = new RecordingMetaHandler();
        await Task.WhenAll(Dispatcher(fixture.Db, handler).DispatchAsync(Event(seed, "exam"), default),
            Dispatcher(secondDb, handler).DispatchAsync(Event(seed, "exam"), default));
        Assert.Single(handler.Requests);
        Assert.Equal(AssessmentParentDeliveryStatus.Sent,
            (await secondDb.AssessmentParentDeliveries.AsNoTracking().SingleAsync()).Status);
    }

    [Theory]
    [InlineData("exam")]
    [InlineData("homework")]
    public async Task CampaignCanSelectSubmittedWorkWithoutDateRangeAndExcludesOpenedOnly(string kind)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var openedOnly = Student();
        openedOnly.UserRoles.Add(new() { RoleId = await fixture.Db.Roles.Where(role => role.Type == RoleType.Student).Select(role => role.Id).FirstAsync() });
        fixture.Db.StudentExamAttempts.Add(new() { Exam = seed.Attempt.Exam, User = openedOnly, StartedAt = DateTime.UtcNow });
        fixture.Db.HomeworkSubmissions.Add(new() { Homework = seed.Submission.Homework, Student = openedOnly });
        await fixture.Db.SaveChangesAsync();
        var filters = kind == "exam" ? new WhatsAppCampaignAudienceFilterDto(
            ExamIds: [seed.Attempt.ExamId], HasExamAttempt: true, ContactRoles: ["StudentPrimary"])
            : new WhatsAppCampaignAudienceFilterDto(HomeworkIds: [seed.Submission.HomeworkId], HasHomeworkSubmission: true, ContactRoles: ["StudentPrimary"]);
        var preview = await new WhatsAppCampaignService(fixture.Db, Protector(), Configuration()).PreviewAsync(
            new(seed.Template.Id, filters, [new("BODY", 1, "Literal", "score", ComponentIndex: 0),
                new("BODY", 2, "Literal", "total", ComponentIndex: 0)]), default);
        Assert.Equal(1, preview.EligibleCount);
    }

    [Fact]
    public async Task SettingsExamCatalogSearchesTeacherAndTitlePagesLatestFirstAndCountsOnlyFinalResults()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var admin = await SeedAdmin(fixture.Db);
        seed.Attempt.Exam.Title = "آخر امتحان نادر";
        seed.Attempt.Exam.CreatedByTeacher.User.FullName = "نادر";
        fixture.Db.StudentExamAttempts.AddRange(RetryAttempt(seed, "ممتاز"), RetryAttempt(seed, "قيد التصحيح"));
        for (var index = 0; index < 21; index++)
            fixture.Db.Exams.Add(new Exam { Title = $"امتحان قديم {index}",
                CreatedByTeacherId = seed.Attempt.Exam.CreatedByTeacherId,
                CreatedAt = DateTime.UtcNow.AddDays(-index - 1) });
        await fixture.Db.SaveChangesAsync();
        var service = new ExamParentMessageRetryService(fixture.Db);
        var first = await service.ListAsync(admin.Id, " نادر ", 1, default);
        Assert.Equal(22, first.TotalCount);
        Assert.Equal(20, first.Items.Count);
        Assert.Equal(seed.Attempt.ExamId, first.Items[0].ExamId);
        Assert.Equal(2, first.Items[0].FinalResultCount);
        var second = await service.ListAsync(admin.Id, "نادر", 2, default);
        Assert.Equal(2, second.Items.Count);
        Assert.Empty(first.Items.Select(exam => exam.ExamId).Intersect(second.Items.Select(exam => exam.ExamId)));
        Assert.Single((await service.ListAsync(admin.Id, "آخر امتحان", 1, default)).Items);
        Assert.Empty((await service.ListAsync(admin.Id, "%", 1, default)).Items);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync(admin.Id, null, 0, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListAsync(seed.Attempt.UserId, null, 1, default));
    }

    [Fact]
    public async Task RetryButtonQueuesFailedAndPreviouslyUnsentResultsOnceAndExcludesDeliveredUncertainAndPendingGrades()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var admin = await SeedAdmin(fixture.Db);
        var unsent = RetryAttempt(seed, "ناجح");
        var delivered = RetryAttempt(seed, "ناجح");
        var uncertain = RetryAttempt(seed, "ناجح");
        var pendingGrade = RetryAttempt(seed, "قيد التصحيح");
        fixture.Db.StudentExamAttempts.AddRange(unsent, delivered, uncertain, pendingGrade);
        fixture.Db.AssessmentParentDeliveries.AddRange(
            RetryDelivery(seed.Attempt, seed.Template, AssessmentParentDeliveryStatus.Failed),
            RetryDelivery(delivered, seed.Template, AssessmentParentDeliveryStatus.Sent, "wamid.delivered"),
            RetryDelivery(uncertain, seed.Template, AssessmentParentDeliveryStatus.Uncertain));
        fixture.Db.LiveSupportWhatsAppPendingReceipts.Add(new()
        { MetaMessageId = "wamid.delivered", Status = "Read", ProviderTimestamp = DateTime.UtcNow });
        foreach (var attempt in new[] { seed.Attempt, unsent, delivered, uncertain, pendingGrade })
            fixture.Db.OutboxEvents.Add(PersistedGrade(attempt));
        await fixture.Db.SaveChangesAsync();
        var service = new ExamParentMessageRetryService(fixture.Db);
        var summary = await service.SummaryAsync(admin.Id, seed.Attempt.ExamId, default);
        Assert.Equal(2, summary.RetryableCount);
        Assert.Equal(1, summary.DeliveredCount);
        Assert.Equal(1, summary.UncertainCount);
        var operationId = Guid.NewGuid();
        var first = await service.QueueAsync(admin.Id, seed.Attempt.ExamId, new(operationId), default);
        var repeated = await service.QueueAsync(admin.Id, seed.Attempt.ExamId, new(operationId), default);
        var anotherClick = await service.QueueAsync(admin.Id, seed.Attempt.ExamId, new(Guid.NewGuid()), default);
        Assert.Equal(2, first.QueuedCount);
        Assert.True(repeated.AlreadyQueued);
        Assert.Equal(0, anotherClick.QueuedCount);
        var queued = await fixture.Db.OutboxEvents.AsNoTracking().Where(item => item.Type == ExamParentMessageRetryService.EventType).ToListAsync();
        Assert.Equal(2, queued.Count);
        var handler = new RecordingMetaHandler("""{"messages":[{"id":"wamid.retry-button"}]}""");
        var dispatcher = Dispatcher(fixture.Db, handler);
        foreach (var notification in queued)
        {
            await dispatcher.DispatchAsync(notification, default);
            await dispatcher.DispatchAsync(notification, default);
        }
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(AssessmentParentDeliveryStatus.Sent,
            (await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync(item => item.AttemptId == unsent.Id)).Status);
    }

    [Fact]
    public async Task FailedOnlyButtonLeavesUnsentResultsForTheSendGradesButtonAndBindsIdempotencyToTheFilter()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var admin = await SeedAdmin(fixture.Db);
        var unsent = RetryAttempt(seed, "ناجح");
        fixture.Db.StudentExamAttempts.Add(unsent);
        fixture.Db.AssessmentParentDeliveries.Add(RetryDelivery(seed.Attempt, seed.Template, AssessmentParentDeliveryStatus.Failed));
        fixture.Db.OutboxEvents.AddRange(PersistedGrade(seed.Attempt), PersistedGrade(unsent));
        await fixture.Db.SaveChangesAsync();
        var service = new ExamParentMessageRetryService(fixture.Db);
        var operation = Guid.NewGuid();
        var result = await service.QueueAsync(admin.Id, seed.Attempt.ExamId, new(operation, FailedOnly: true), default);
        Assert.Equal(1, result.QueuedCount);
        var queued = await fixture.Db.OutboxEvents.AsNoTracking()
            .SingleAsync(item => item.Type == ExamParentMessageRetryService.EventType);
        Assert.Equal(seed.Attempt.Id, JsonSerializer.Deserialize<ExamParentMessageRetryEnvelope>(queued.PayloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AttemptId);
        Assert.True((await service.QueueAsync(admin.Id, seed.Attempt.ExamId, new(operation, FailedOnly: true), default)).AlreadyQueued);
        await Assert.ThrowsAsync<ArgumentException>(() => service.QueueAsync(admin.Id, seed.Attempt.ExamId, new(operation), default));
        var summary = await service.SummaryAsync(admin.Id, seed.Attempt.ExamId, default);
        Assert.Equal(1, summary.PendingCount);
        Assert.Equal(1, summary.NotSentCount);
        Assert.Equal(1, summary.RetryableCount);
        Assert.Equal(1, (await service.QueueAsync(admin.Id, seed.Attempt.ExamId, new(Guid.NewGuid()), default)).QueuedCount);
    }

    [Fact]
    public async Task RetryButtonUsesFailedProviderReceiptAndDoesNotResendWhenTheSameQueuedEventIsReplayed()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var admin = await SeedAdmin(fixture.Db);
        const string beforeId = "wamid.retry-before";
        const string afterId = "wamid.retry-after";
        await fixture.Db.LiveSupportWhatsAppPendingReceipts.Where(item => item.MetaMessageId == beforeId || item.MetaMessageId == afterId).ExecuteDeleteAsync();
        fixture.Db.AssessmentParentDeliveries.Add(RetryDelivery(seed.Attempt, seed.Template, AssessmentParentDeliveryStatus.Sent, beforeId));
        fixture.Db.LiveSupportWhatsAppPendingReceipts.Add(new()
        { MetaMessageId = beforeId, Status = "Failed", FailureCode = "WHATSAPP_CLOUD_131042", ProviderTimestamp = DateTime.UtcNow });
        await fixture.Db.SaveChangesAsync();
        var service = new ExamParentMessageRetryService(fixture.Db);
        var summary = await service.SummaryAsync(admin.Id, seed.Attempt.ExamId, default);
        Assert.Equal(1, summary.FailedCount);
        Assert.True(Assert.Single(summary.Attempts).CanRetry);
        var queued = await service.QueueAsync(admin.Id, seed.Attempt.ExamId, new(Guid.NewGuid()), default);
        Assert.Equal(1, queued.QueuedCount);
        var notification = await fixture.Db.OutboxEvents.AsNoTracking().SingleAsync(item => item.Type == ExamParentMessageRetryService.EventType);
        var handler = new RecordingMetaHandler("""{"messages":[{"id":"wamid.retry-after"}]}""");
        var dispatcher = Dispatcher(fixture.Db, handler);
        await dispatcher.DispatchAsync(notification, default);
        fixture.Db.LiveSupportWhatsAppPendingReceipts.Add(new()
        { MetaMessageId = afterId, Status = "Failed", FailureCode = "WHATSAPP_CLOUD_131042", ProviderTimestamp = DateTime.UtcNow });
        await fixture.Db.SaveChangesAsync();
        await AssessmentParentDeliveryReceipts.ReconcileAsync(fixture.Db, afterId, default);
        await dispatcher.DispatchAsync(notification, default);
        Assert.Single(handler.Requests);
        var delivery = await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal(AssessmentParentDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(afterId, delivery.MetaMessageId);
    }

    [Fact]
    public async Task ConcurrentRetryClicksQueueOneMessageAndNonAdminCannotQueue()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var admin = await SeedAdmin(fixture.Db);
        fixture.Db.OutboxEvents.Add(PersistedGrade(seed.Attempt));
        await fixture.Db.SaveChangesAsync();
        var service = new ExamParentMessageRetryService(fixture.Db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.QueueAsync(seed.Attempt.UserId, seed.Attempt.ExamId, new(Guid.NewGuid()), default));
        await using var secondDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
        var results = await Task.WhenAll(service.QueueAsync(admin.Id, seed.Attempt.ExamId, new(Guid.NewGuid()), default),
            new ExamParentMessageRetryService(secondDb).QueueAsync(admin.Id, seed.Attempt.ExamId, new(Guid.NewGuid()), default));
        Assert.Equal(1, results.Sum(item => item.QueuedCount));
        Assert.Single(await fixture.Db.OutboxEvents.Where(item => item.Type == ExamParentMessageRetryService.EventType).ToListAsync());
    }

    private static StudentExamAttempt RetryAttempt(SeedData seed, string evaluation) => new()
    {
        Exam = seed.Attempt.Exam, User = seed.Attempt.User, Evaluation = evaluation,
        DefinitionSnapshotJson = seed.Attempt.DefinitionSnapshotJson, ScoreAchieved = 8
    };

    [Theory]
    [InlineData("disabled")]
    [InlineData("pending")]
    public async Task QueuedRetryRechecksCurrentNotificationConsentAndFinalGradingBeforeSending(string change)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await Seed(fixture.Db);
        var admin = await SeedAdmin(fixture.Db);
        fixture.Db.OutboxEvents.Add(PersistedGrade(seed.Attempt));
        await fixture.Db.SaveChangesAsync();
        var service = new ExamParentMessageRetryService(fixture.Db);
        Assert.Equal(1, (await service.QueueAsync(admin.Id, seed.Attempt.ExamId, new(Guid.NewGuid()), default)).QueuedCount);
        if (change == "disabled") seed.Attempt.Exam.ParentNotificationSettingsJson = null;
        else seed.Attempt.Evaluation = "قيد التصحيح";
        await fixture.Db.SaveChangesAsync();
        var handler = new RecordingMetaHandler();
        var notification = await fixture.Db.OutboxEvents.AsNoTracking().SingleAsync(item => item.Type == ExamParentMessageRetryService.EventType);
        await Dispatcher(fixture.Db, handler).DispatchAsync(notification, default);
        Assert.Empty(handler.Requests);
        Assert.Empty(await fixture.Db.AssessmentParentDeliveries.ToListAsync());
    }

    private static AssessmentParentDelivery RetryDelivery(StudentExamAttempt attempt, LiveSupportWhatsAppTemplate template,
        AssessmentParentDeliveryStatus status, string? messageId = null) => new()
    {
        AssessmentKind = "exam", AssessmentId = attempt.ExamId, AttemptId = attempt.Id, StudentUserId = attempt.UserId,
        TemplateId = template.Id, TemplateFingerprint = template.Fingerprint, Status = status, MetaMessageId = messageId,
        FailureCode = status == AssessmentParentDeliveryStatus.Failed ? "132005" : null
    };

    private static OutboxEvent PersistedGrade(StudentExamAttempt attempt) => new()
    {
        Type = "ExamGraded", TargetUserId = attempt.UserId.ToString(), ProcessedAt = DateTime.UtcNow,
        PayloadJson = JsonSerializer.Serialize(new { examId = attempt.Exam.Id, attemptId = attempt.Id })
    };

    private static async Task<User> SeedAdmin(AppDbContext db)
    {
        var admin = new User { FullName = "أدمن الاختبار", PasswordHash = "test-only", PhoneNumber = $"015{Random.Shared.NextInt64(10000000, 99999999)}" };
        admin.UserRoles.Add(new() { RoleId = await db.Roles.Where(role => role.Type == RoleType.Admin).Select(role => role.Id).FirstAsync() });
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        return admin;
    }

    private static AssessmentParentNotificationDispatcher Dispatcher(AppDbContext db, RecordingMetaHandler handler) => new(db,
        new WhatsAppCloudService(new HttpClient(handler), Configuration(), NullLogger<WhatsAppCloudService>.Instance), Protector());
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["WhatsAppCloudApi:AccessToken"] = "test-token", ["WhatsAppCloudApi:PhoneNumberId"] = "test-phone",
        ["WhatsAppCampaigns:HmacKey"] = Convert.ToBase64String(new byte[32])
    }).Build();
    private static readonly EphemeralDataProtectionProvider ProtectionProvider = new();
    private static WhatsAppCampaignDataProtector Protector() => new(ProtectionProvider, Configuration());
    private static User Student() => new()
    {
        FullName = "طالب اختبار", PasswordHash = "test-only", IsActive = true,
        PhoneNumber = $"010{Random.Shared.NextInt64(10000000, 99999999)}",
        StudentProfile = new() { SecondaryParentPhone = "01012345678", ParentPhone = "01112345678", MotherPhone = "01212345678" }
    };
    private sealed record SeedData(StudentExamAttempt Attempt, HomeworkSubmission Submission, LiveSupportWhatsAppTemplate Template);
    private static async Task<SeedData> Seed(AppDbContext db)
    {
        var parentHash = Protector().DestinationHash("201012345678");
        await db.WhatsAppContactPreferences.Where(preference => preference.DestinationHash == parentHash).ExecuteDeleteAsync();
        var student = Student();
        student.UserRoles.Add(new() { RoleId = await db.Roles.Where(role => role.Type == RoleType.Student).Select(role => role.Id).FirstAsync() });
        var teacher = new TeacherProfile { User = new() { FullName = "المدرس", PasswordHash = "test-only", PhoneNumber = $"011{Random.Shared.NextInt64(10000000, 99999999)}" } };
        var lesson = new Lesson { Title = "الحصة", ContentSection = new() { Title = "الشهر", Term = new() { Title = "الترم",
            Package = new() { Name = "باقة", Teacher = teacher, Subject = new() { Name = "مادة", NormalizedName = Guid.NewGuid().ToString("N") } } } } };
        var template = new LiveSupportWhatsAppTemplate { MetaTemplateId = Guid.NewGuid().ToString("N"), Name = "result_" + Guid.NewGuid().ToString("N"), Language = "ar", Category = "UTILITY", Status = "APPROVED",
            Fingerprint = new string('a', 64), ComponentsJson = """[{"type":"BODY","text":"الدرجة {{1}} من {{2}}"}]""" };
        var settings = new AssessmentParentNotificationSettings(true, template.Id, template.Fingerprint, [new("Score"), new("TotalScore")]);
        var exam = new Exam { Title = "الامتحان", CreatedByTeacher = teacher, TotalScore = 50,
            ParentNotificationSettingsJson = settings.ToJson(), ParentNotificationEnabledAt = DateTime.UtcNow.AddHours(-1) };
        lesson.ExamId = exam.Id;
        var attempt = new StudentExamAttempt { Exam = exam, User = student, Evaluation = "ناجح", ScoreAchieved = 8,
            DefinitionSnapshotJson = (AssessmentDefinitionSnapshot.FromExam(exam) with { TotalScore = 10 }).ToJson() };
        db.Lessons.Add(lesson);
        var homework = new Homework { LessonId = lesson.Id, Title = "الواجب", TotalScore = 50,
            ParentNotificationSettingsJson = settings.ToJson(), ParentNotificationEnabledAt = DateTime.UtcNow.AddHours(-1) };
        homework.Questions.Add(new HomeworkQuestion { BodyText = "سؤال", PointsActive = 1 });
        db.StudentFacingAcademicScopes.Add(new() { OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = lesson.ContentSection.Term.Package.Id, ScopeLevel = AcademicScopeLevel.PlatformWide });
        var submission = new HomeworkSubmission { Homework = homework, Student = student, Status = SubmissionStatus.Graded,
            OverallScore = 8, TotalScoreSnapshot = 10, SubmittedAt = DateTime.UtcNow };
        db.StudentExamAttempts.Add(attempt); db.HomeworkSubmissions.Add(submission); db.LiveSupportWhatsAppTemplates.Add(template);
        await db.SaveChangesAsync();
        return new(attempt, submission, template);
    }
    private static OutboxEvent Event(SeedData seed, string kind) => new()
    {
        Type = kind == "exam" ? "ExamGraded" : "HomeworkGraded", TargetUserId = seed.Attempt.UserId.ToString(),
        PayloadJson = kind == "exam" ? JsonSerializer.Serialize(new { attemptId = seed.Attempt.Id })
            : JsonSerializer.Serialize(new { submissionId = seed.Submission.Id })
    };
    private sealed class RecordingMetaHandler(string response = "{\"messages\":[{\"id\":\"wamid.test\"}]}") : HttpMessageHandler
    {
        public System.Collections.Concurrent.ConcurrentBag<string> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(ct));
            return new(HttpStatusCode.OK) { Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
