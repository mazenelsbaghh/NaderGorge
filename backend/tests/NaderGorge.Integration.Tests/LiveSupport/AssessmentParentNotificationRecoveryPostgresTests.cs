using System.Net;
using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

[Collection("AssessmentParentRecoveryPostgres")]
public sealed class AssessmentParentNotificationRecoveryPostgresTests
{
    [Fact]
    public async Task PreviewAndApply_SelectOnlyTheIncidentCohort_AndRebuildTheCurrentGradePayload()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await SeedAsync(fixture.Db);
        var eligible = await FailedDeliveryAsync(fixture.Db, seed, AssessmentParentNotificationRecoveryService.IncidentCutoffUtc.AddTicks(-1));
        await FailedDeliveryAsync(fixture.Db, seed, AssessmentParentNotificationRecoveryService.IncidentCutoffUtc.AddTicks(1));
        await FailedDeliveryAsync(fixture.Db, seed, AssessmentParentNotificationRecoveryService.IncidentCutoffUtc.AddTicks(-2), "other");
        var recovery = Recovery(fixture.Db);

        var preview = await recovery.PreviewAsync(seed.Admin.Id, 50, default);
        Assert.Equal(1, preview.EligibleCount);
        Assert.Equal(64, preview.CohortFingerprint.Length);

        var dryRunDeliveries = await fixture.Db.AssessmentParentDeliveries.AsNoTracking().CountAsync();
        var applied = await recovery.ApplyAsync(seed.Admin.Id, new(Guid.NewGuid(), preview.CohortFingerprint, 50), default);
        Assert.Equal(1, applied.EligibleCount);
        Assert.Equal(dryRunDeliveries, await fixture.Db.AssessmentParentDeliveries.AsNoTracking().CountAsync());
        var rebuilt = await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync(x => x.Id == eligible.Id);
        Assert.Equal(AssessmentParentDeliveryStatus.Pending, rebuilt.Status);
        Assert.Null(rebuilt.FailureCode);
        var payload = JsonSerializer.Deserialize<WhatsAppCloudService.TemplateMessageRequest>(
            Protector().Unprotect(rebuilt.Id, rebuilt.ProtectedPayload, rebuilt.PayloadDigest))!;
        Assert.Equal("201012345678", payload.RecipientPhoneNumber);
        Assert.Equal("الكريم", payload.Components[0].Parameters[0]);
        Assert.Contains("طالب الاسترداد", payload.Components.SelectMany(x => x.Parameters));
        Assert.Contains("13", payload.Components.SelectMany(x => x.Parameters));
        Assert.Contains("15", payload.Components.SelectMany(x => x.Parameters));
        Assert.DoesNotContain("20", payload.Components.SelectMany(x => x.Parameters));
        Assert.Single(await fixture.Db.AuditLogs.Where(x => x.EntityId == eligible.Id && x.Action == "AssessmentParentRecoveryRebuilt").ToListAsync());
        Assert.Single(await fixture.Db.OutboxEvents.Where(x => x.Type == "AssessmentParentRecovery").ToListAsync());
    }

    [Fact]
    public async Task Apply_IsBoundedIdempotent_AndRejectsCohortDrift()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await SeedAsync(fixture.Db);
        await FailedDeliveryAsync(fixture.Db, seed, AssessmentParentNotificationRecoveryService.IncidentCutoffUtc.AddTicks(-1));
        await FailedDeliveryAsync(fixture.Db, seed, AssessmentParentNotificationRecoveryService.IncidentCutoffUtc.AddTicks(-2));
        var recovery = Recovery(fixture.Db);
        var preview = await recovery.PreviewAsync(seed.Admin.Id, 10, default);
        var operation = Guid.NewGuid();
        var first = await recovery.ApplyAsync(seed.Admin.Id, new(operation, preview.CohortFingerprint, 10), default);
        Assert.Equal(2, first.EligibleCount);
        var replay = await recovery.ApplyAsync(seed.Admin.Id, new(operation, preview.CohortFingerprint, 10), default);
        Assert.True(replay.AlreadyApplied);
        Assert.Equal(2, await fixture.Db.AuditLogs.CountAsync(x => x.Action == "AssessmentParentRecoveryRebuilt"));
        Assert.Equal(2, await fixture.Db.OutboxEvents.CountAsync(x => x.Type == "AssessmentParentRecovery"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.ApplyAsync(seed.Admin.Id, new(Guid.NewGuid(), new string('0', 64), 10), default));
    }

    [Fact]
    public async Task ConcurrentApply_UsesOneRecoveryOperationAndOneOutboxRecord()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await SeedAsync(fixture.Db);
        var delivery = await FailedDeliveryAsync(fixture.Db, seed, AssessmentParentNotificationRecoveryService.IncidentCutoffUtc.AddTicks(-1));
        var preview = await Recovery(fixture.Db).PreviewAsync(seed.Admin.Id, 10, default);
        var operation = Guid.NewGuid();
        await using var first = Open(fixture);
        await using var second = Open(fixture);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<AssessmentParentRecoveryPreview> Apply(AppDbContext db)
        {
            await start.Task;
            return await Recovery(db).ApplyAsync(seed.Admin.Id, new(operation, preview.CohortFingerprint, 10), default);
        }
        var one = Apply(first);
        var two = Apply(second);
        start.SetResult();
        var results = await Task.WhenAll(one, two);
        Assert.Contains(results, result => result.AlreadyApplied);
        Assert.Equal(1, await fixture.Db.AuditLogs.CountAsync(x => x.Action == "AssessmentParentRecoveryRebuilt" && x.EntityId == delivery.Id));
        Assert.Equal(1, await fixture.Db.OutboxEvents.CountAsync(x => x.Type == "AssessmentParentRecovery"));
        Assert.Equal(AssessmentParentDeliveryStatus.Pending,
            (await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync(x => x.Id == delivery.Id)).Status);
    }

    [Fact]
    public async Task RecoveredRow_OnlyDedicatedEnvelopeCanSend_AndItEnforcesCurrentGradeVersion()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await SeedAsync(fixture.Db);
        var delivery = await FailedDeliveryAsync(fixture.Db, seed, AssessmentParentNotificationRecoveryService.IncidentCutoffUtc.AddTicks(-1));
        var recovery = Recovery(fixture.Db);
        var preview = await recovery.PreviewAsync(seed.Admin.Id, 10, default);
        var operation = Guid.NewGuid();
        await recovery.ApplyAsync(seed.Admin.Id, new(operation, preview.CohortFingerprint), default);
        var handler = new RecordingHandler();
        var dispatcher = Dispatcher(fixture.Db, handler);
        await dispatcher.DispatchAsync(ExamEvent(seed), default);
        Assert.Empty(handler.Requests);
        Assert.Equal(AssessmentParentDeliveryStatus.Pending, (await fixture.Db.AssessmentParentDeliveries.FindAsync(delivery.Id))!.Status);

        seed.Attempt.ScoreAchieved = 14;
        fixture.Db.StudentExamAttempts.Update(seed.Attempt);
        await fixture.Db.SaveChangesAsync();
        var audit = await fixture.Db.AuditLogs.SingleAsync(x => x.EntityId == delivery.Id && x.Action == "AssessmentParentRecoveryRebuilt");
        using var auditJson = JsonDocument.Parse(audit.NewValues!);
        var version = auditJson.RootElement.GetProperty("gradeVersion").GetString()!;
        await dispatcher.DispatchAsync(RecoveryEvent(seed, delivery.Id, version, operation), default);
        Assert.Empty(handler.Requests);
        var skipped = await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync(x => x.Id == delivery.Id);
        Assert.Equal(AssessmentParentDeliveryStatus.Skipped, skipped.Status);
        Assert.Equal("RECOVERY_GRADE_OR_TEMPLATE_CHANGED", skipped.FailureCode);
    }

    [Fact]
    public async Task DedicatedEnvelope_SendsOnce_AndTerminalOrAmbiguousOutcomesNeverResend()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await SeedAsync(fixture.Db);
        var delivery = await RebuildAsync(fixture.Db, seed);
        var audit = await fixture.Db.AuditLogs.SingleAsync(x => x.EntityId == delivery.Id);
        using var json = JsonDocument.Parse(audit.NewValues!);
        var gradeVersion = json.RootElement.GetProperty("gradeVersion").GetString()!;
        var correlation = Guid.ParseExact(audit.CorrelationId!, "N");
        var eventToSend = RecoveryEvent(seed, delivery.Id, gradeVersion, correlation);
        var handler = new RecordingHandler("{}");
        var dispatcher = Dispatcher(fixture.Db, handler);
        await dispatcher.DispatchAsync(eventToSend, default);
        await dispatcher.DispatchAsync(eventToSend, default);
        Assert.Single(handler.Requests);
        Assert.Equal(AssessmentParentDeliveryStatus.Uncertain,
            (await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync(x => x.Id == delivery.Id)).Status);
    }

    [Fact]
    public async Task PostClaimGradeChange_SkipsBeforeProviderSend()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var seed = await SeedAsync(fixture.Db);
        var delivery = await RebuildAsync(fixture.Db, seed);
        var audit = await fixture.Db.AuditLogs.SingleAsync(x => x.EntityId == delivery.Id);
        using var json = JsonDocument.Parse(audit.NewValues!);
        var eventToSend = RecoveryEvent(seed, delivery.Id, json.RootElement.GetProperty("gradeVersion").GetString()!,
            Guid.ParseExact(audit.CorrelationId!, "N"));
        var interceptor = new PostClaimGradeMutation(fixture.ConnectionString, seed.Attempt.Id);
        await using var dispatchDb = Open(fixture, interceptor);
        var handler = new RecordingHandler();
        await Dispatcher(dispatchDb, handler).DispatchAsync(eventToSend, default);
        Assert.True(interceptor.Mutated);
        Assert.Empty(handler.Requests);
        var skipped = await fixture.Db.AssessmentParentDeliveries.AsNoTracking().SingleAsync(x => x.Id == delivery.Id);
        Assert.Equal(AssessmentParentDeliveryStatus.Skipped, skipped.Status);
        Assert.Equal("RECOVERY_PRE_SEND_AUTHORITY_CHANGED", skipped.FailureCode);
    }

    private static async Task<AssessmentParentDelivery> RebuildAsync(AppDbContext db, Seed seed)
    {
        var delivery = await FailedDeliveryAsync(db, seed, AssessmentParentNotificationRecoveryService.IncidentCutoffUtc.AddTicks(-1));
        var service = Recovery(db);
        var preview = await service.PreviewAsync(seed.Admin.Id, 10, default);
        await service.ApplyAsync(seed.Admin.Id, new(Guid.NewGuid(), preview.CohortFingerprint), default);
        return delivery;
    }

    private static AssessmentParentNotificationRecoveryService Recovery(AppDbContext db) => new(db, Protector());
    private static AppDbContext Open(PostgresLiveSupportFixture fixture) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(fixture.ConnectionString).Options);
    private static AppDbContext Open(PostgresLiveSupportFixture fixture, DbCommandInterceptor interceptor) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(fixture.ConnectionString).AddInterceptors(interceptor).Options);
    private static AssessmentParentNotificationDispatcher Dispatcher(AppDbContext db, HttpMessageHandler handler) => new(db,
        new WhatsAppCloudService(new HttpClient(handler), Configuration(), NullLogger<WhatsAppCloudService>.Instance), Protector());
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["WhatsAppCloudApi:AccessToken"] = "test-token", ["WhatsAppCloudApi:PhoneNumberId"] = "test-phone",
      ["WhatsAppCampaigns:HmacKey"] = Convert.ToBase64String(new byte[32]) }).Build();
    private static readonly EphemeralDataProtectionProvider ProtectionProvider = new();
    private static WhatsAppCampaignDataProtector Protector() => new(ProtectionProvider, Configuration());

    private static async Task<Seed> SeedAsync(AppDbContext db)
    {
        var unique = Random.Shared.NextInt64(10000000, 99999999);
        var admin = new User { FullName = "مدير", PasswordHash = "test", PhoneNumber = $"010{unique}" };
        admin.UserRoles.Add(new UserRole { RoleId = await db.Roles.Where(x => x.Type == RoleType.Admin).Select(x => x.Id).FirstAsync() });
        var student = new User { FullName = "طالب الاسترداد", PasswordHash = "test", PhoneNumber = $"011{unique}",
            StudentProfile = new() { ParentPhone = "201012345678", ParentTrackingCode = "TRACK" } };
        student.UserRoles.Add(new UserRole { RoleId = await db.Roles.Where(x => x.Type == RoleType.Student).Select(x => x.Id).FirstAsync() });
        var teacher = new TeacherProfile { User = new() { FullName = "مدرس", PasswordHash = "test", PhoneNumber = $"012{unique}" } };
        var template = await db.LiveSupportWhatsAppTemplates.SingleOrDefaultAsync(x => x.Id == AssessmentParentNotificationRecoveryService.IncidentTemplateId) ?? new LiveSupportWhatsAppTemplate { Id = AssessmentParentNotificationRecoveryService.IncidentTemplateId,
            Name = "incident_result", Language = "ar", Category = "UTILITY", Status = "APPROVED",
            Fingerprint = AssessmentParentNotificationRecoveryService.IncidentTemplateFingerprint,
            ComponentsJson = """[{"type":"HEADER","format":"TEXT","text":"{{1}}"},{"type":"BODY","text":"{{1}} {{2}} {{3}} {{4}} {{5}} {{6}}"}]""" };
        var settings = new AssessmentParentNotificationSettings(true, template.Id, template.Fingerprint,
            [new("Literal", "الكريم"), new("StudentName"), new("AssessmentName"), new("SubjectName"), new("Score"), new("TotalScore"), new("ParentTrackingCode")]);
        var package = new Package { Name = "باقة", Teacher = teacher, Subject = new Subject { Name = "مادة", NormalizedName = Guid.NewGuid().ToString("N") } };
        var lesson = new Lesson { Title = "الحصة", ContentSection = new() { Title = "قسم", Term = new() { Title = "ترم", Package = package } } };
        var exam = await db.Exams.SingleOrDefaultAsync(x => x.Id == AssessmentParentNotificationRecoveryService.IncidentExamId) ?? new Exam { Id = AssessmentParentNotificationRecoveryService.IncidentExamId, Title = "اختبار", TotalScore = 20, CreatedByTeacher = teacher,
            ParentNotificationSettingsJson = settings.ToJson(), ParentNotificationEnabledAt = DateTime.UtcNow.AddHours(-1) };
        lesson.ExamId = exam.Id;
        var attempt = new StudentExamAttempt { Exam = exam, User = student, Evaluation = "ناجح", ScoreAchieved = 13,
            DefinitionSnapshotJson = (AssessmentDefinitionSnapshot.FromExam(exam) with { TotalScore = 15 }).ToJson() };
        db.AddRange(admin, student, lesson, attempt);
        if (db.Entry(template).State == EntityState.Detached) db.Add(template);
        if (db.Entry(exam).State == EntityState.Detached) db.Add(exam);
        await db.SaveChangesAsync();
        return new(admin, student, attempt);
    }

    private static async Task<AssessmentParentDelivery> FailedDeliveryAsync(AppDbContext db, Seed seed, DateTime createdAt, string code = "132005")
    {
        var attemptId = seed.Attempt.Id;
        if (await db.AssessmentParentDeliveries.AnyAsync(item => item.AttemptId == attemptId))
        {
            var extra = new StudentExamAttempt { ExamId = seed.Attempt.ExamId, UserId = seed.Student.Id,
                Evaluation = "ناجح", ScoreAchieved = 13, DefinitionSnapshotJson = seed.Attempt.DefinitionSnapshotJson };
            db.StudentExamAttempts.Add(extra);
            await db.SaveChangesAsync();
            attemptId = extra.Id;
        }
        var id = Guid.NewGuid();
        var old = Protector().Protect(id, JsonSerializer.SerializeToUtf8Bytes(new WhatsAppCloudService.TemplateMessageRequest("201012345678", "old", "ar", [])));
        var delivery = new AssessmentParentDelivery { Id = id, AssessmentKind = "exam", AssessmentId = seed.Attempt.ExamId, AttemptId = attemptId,
            StudentUserId = seed.Student.Id, TemplateId = AssessmentParentNotificationRecoveryService.IncidentTemplateId,
            TemplateFingerprint = AssessmentParentNotificationRecoveryService.IncidentTemplateFingerprint,
            DestinationHash = Protector().DestinationHash("201012345678"), ProtectedPayload = old,
            PayloadDigest = Protector().Digest(id, old), Status = AssessmentParentDeliveryStatus.Failed, FailureCode = code, CreatedAt = createdAt };
        db.AssessmentParentDeliveries.Add(delivery); await db.SaveChangesAsync(); return delivery;
    }

    private static OutboxEvent ExamEvent(Seed seed) => new() { Type = "ExamGraded", TargetUserId = seed.Student.Id.ToString(), PayloadJson = JsonSerializer.Serialize(new { attemptId = seed.Attempt.Id }) };
    private static OutboxEvent RecoveryEvent(Seed seed, Guid deliveryId, string version, Guid operation) => new() { Type = "AssessmentParentRecovery", TargetUserId = seed.Student.Id.ToString(), PayloadJson = JsonSerializer.Serialize(new AssessmentParentRecoveryEnvelope(seed.Attempt.Id, deliveryId, version, operation)) };
    private sealed record Seed(User Admin, User Student, StudentExamAttempt Attempt);
    private sealed class RecordingHandler(string reply = "{\"messages\":[{\"id\":\"wamid.test\"}]}") : HttpMessageHandler
    { public List<string> Requests { get; } = []; protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Requests.Add(await request.Content!.ReadAsStringAsync(ct)); return new(HttpStatusCode.OK) { Content = new StringContent(reply) }; } }
    private sealed class PostClaimGradeMutation(string connectionString, Guid attemptId) : DbCommandInterceptor
    {
        public bool Mutated { get; private set; }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (!Mutated && command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("assessment_parent_deliveries", StringComparison.OrdinalIgnoreCase))
            {
                Mutated = true;
                await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
                var attempt = await db.StudentExamAttempts.SingleAsync(item => item.Id == attemptId, cancellationToken);
                attempt.ScoreAchieved = 14;
                await db.SaveChangesAsync(cancellationToken);
            }
            return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}

[CollectionDefinition("AssessmentParentRecoveryPostgres", DisableParallelization = true)]
public sealed class AssessmentParentRecoveryPostgresCollection;
