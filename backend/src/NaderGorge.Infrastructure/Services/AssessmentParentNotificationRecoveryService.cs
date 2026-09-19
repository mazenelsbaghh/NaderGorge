using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services;

public sealed class AssessmentParentNotificationRecoveryService(
    AppDbContext db, IWhatsAppCampaignDataProtector protector)
    : IAssessmentParentNotificationRecoveryService
{
    internal static readonly Guid IncidentExamId = Guid.Parse("12162cd5-423e-4db5-994a-fad8d344ce95");
    internal static readonly Guid IncidentTemplateId = Guid.Parse("1481cf87-7060-449e-81cc-0862370341f5");
    internal const string IncidentTemplateFingerprint = "0ece48dfd32131964e558c0c92b80f6b63a286b986f0a9fec6a0c08c629ed2d8";
    internal static readonly DateTime IncidentCutoffUtc = new DateTime(
        2026, 9, 19, 16, 14, 38, 894, DateTimeKind.Utc).AddTicks(2760);
    private const string IncidentFailureCode = "132005";
    private const string AuditAction = "AssessmentParentRecoveryRebuilt";
    private static readonly AssessmentResultParameter[] ExpectedParameters =
    [
        new("Literal", "الكريم"), new("StudentName"), new("AssessmentName"),
        new("SubjectName"), new("Score"), new("TotalScore"), new("ParentTrackingCode")
    ];

    public async Task<AssessmentParentRecoveryPreview> PreviewAsync(Guid actorId, int maxBatchSize, CancellationToken ct)
    {
        await RequireActiveAdminAsync(actorId, ct);
        maxBatchSize = Math.Clamp(maxBatchSize, 1, 10);
        var candidates = await CandidateRows(ct);
        var eligible = new List<Candidate>();
        var excluded = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var delivery in candidates)
        {
            var candidate = await HydrateAsync(delivery, ct);
            if (candidate is null) Add(excluded, "CURRENT_AUTHORITY_MISMATCH");
            else eligible.Add(candidate);
            if (eligible.Count == maxBatchSize) break;
        }
        return new(eligible.Count, CohortFingerprint(eligible), excluded);
    }

    public async Task<AssessmentParentRecoveryPreview> ApplyAsync(
        Guid actorId, AssessmentParentRecoveryRequest request, CancellationToken ct)
        => await SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ChangeTracker.Clear();
            return await ApplyOnceAsync(actorId, request, retryCt);
        }, ct);

    public async Task<AssessmentParentRecoveryStatus> StatusAsync(
        Guid actorId, Guid operationId, CancellationToken ct)
    {
        await RequireActiveAdminAsync(actorId, ct);
        if (operationId == Guid.Empty) throw new ArgumentException("Operation id is required.");
        var correlation = operationId.ToString("N");
        var deliveryIds = await db.AuditLogs.AsNoTracking()
            .Where(item => item.Action == AuditAction && item.EntityType == "AssessmentParentDelivery"
                && item.CorrelationId == correlation && item.EntityId != null)
            .Select(item => item.EntityId!.Value).Distinct().ToArrayAsync(ct);
        var statuses = await db.AssessmentParentDeliveries.AsNoTracking()
            .Where(item => deliveryIds.Contains(item.Id)).Select(item => item.Status).ToArrayAsync(ct);
        int Count(AssessmentParentDeliveryStatus status) => statuses.Count(item => item == status);
        return new(operationId, statuses.Length, Count(AssessmentParentDeliveryStatus.Pending),
            Count(AssessmentParentDeliveryStatus.Sending), Count(AssessmentParentDeliveryStatus.Sent),
            Count(AssessmentParentDeliveryStatus.Failed), Count(AssessmentParentDeliveryStatus.Skipped),
            Count(AssessmentParentDeliveryStatus.Uncertain));
    }

    private async Task<AssessmentParentRecoveryPreview> ApplyOnceAsync(
        Guid actorId, AssessmentParentRecoveryRequest request, CancellationToken ct)
    {
        if (request.OperationId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Operation and actor ids are required.");
        await RequireActiveAdminAsync(actorId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var correlation = request.OperationId.ToString("N");
        var prior = await db.AuditLogs.AsNoTracking().CountAsync(x => x.Action == AuditAction
            && x.CorrelationId == correlation, ct);
        if (prior > 0)
        {
            var recorded = await db.AuditLogs.AsNoTracking().Where(x => x.Action == AuditAction
                    && x.CorrelationId == correlation).Select(x => x.NewValues).ToListAsync(ct);
            var fingerprints = recorded.Select(ReadCohortFingerprint).Distinct().ToArray();
            if (fingerprints.Length != 1 || !FixedEquals(fingerprints[0], request.ExpectedCohortFingerprint))
                throw new InvalidOperationException("Operation id was already used for another recovery cohort.");
            await tx.CommitAsync(ct);
            return new(prior, fingerprints[0],
                new Dictionary<string, int>(), AlreadyApplied: true);
        }

        var rows = await CandidateRows(ct);
        var selected = new List<Candidate>();
        foreach (var row in rows)
        {
            var hydrated = await HydrateAsync(row, ct);
            if (hydrated is not null) selected.Add(hydrated);
            if (selected.Count == Math.Clamp(request.MaxBatchSize, 1, 10)) break;
        }
        var fingerprint = CohortFingerprint(selected);
        if (!FixedEquals(fingerprint, request.ExpectedCohortFingerprint))
            throw new InvalidOperationException("Recovery cohort changed; preview again.");

        var applied = 0;
        foreach (var candidate in selected)
        {
            var delivery = await db.AssessmentParentDeliveries.SingleOrDefaultAsync(x => x.Id == candidate.Delivery.Id
                && x.Status == AssessmentParentDeliveryStatus.Failed
                && x.FailureCode == IncidentFailureCode && x.PayloadDigest == candidate.Delivery.PayloadDigest, ct);
            if (delivery is null) throw new InvalidOperationException("Recovery cohort changed during apply.");
            var fresh = await HydrateAsync(delivery, ct);
            if (fresh is null || fresh.GradeVersion != candidate.GradeVersion)
                throw new InvalidOperationException("Recovery grade changed during apply.");
            var oldDigest = delivery.PayloadDigest;
            delivery.ProtectedPayload = protector.Protect(delivery.Id,
                JsonSerializer.SerializeToUtf8Bytes(fresh.Request));
            delivery.PayloadDigest = protector.Digest(delivery.Id, delivery.ProtectedPayload);
            delivery.Status = AssessmentParentDeliveryStatus.Pending;
            delivery.FailureCode = null;
            delivery.ClaimedAt = null;
            delivery.MetaMessageId = null;
            delivery.SentAt = null;
            db.AuditLogs.Add(new AuditLog
            {
                Action = AuditAction, EntityType = "AssessmentParentDelivery", EntityId = delivery.Id,
                PerformedByUserId = actorId, ActorType = "Admin", CorrelationId = correlation,
                Reason = "Authorized correction of failed exam result notification payload.",
                OldValues = JsonSerializer.Serialize(new { status = "Failed", failureCode = IncidentFailureCode,
                    payloadDigest = oldDigest, attemptCount = delivery.AttemptCount }),
                NewValues = JsonSerializer.Serialize(new { recoveryVersion = 1, gradeVersion = fresh.GradeVersion,
                    templateFingerprint = delivery.TemplateFingerprint, payloadDigest = delivery.PayloadDigest,
                    score = fresh.Result.Score, total = fresh.Result.Total, cohortFingerprint = fingerprint })
            });
            db.OutboxEvents.Add(new OutboxEvent
            {
                Type = "AssessmentParentRecovery", TargetUserId = delivery.StudentUserId.ToString(),
                PayloadJson = JsonSerializer.Serialize(new AssessmentParentRecoveryEnvelope(
                    delivery.AttemptId, delivery.Id, fresh.GradeVersion, request.OperationId))
            });
            applied++;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(applied, fingerprint, new Dictionary<string, int>());
    }

    private async Task<List<AssessmentParentDelivery>> CandidateRows(CancellationToken ct) =>
        await db.AssessmentParentDeliveries.AsNoTracking()
            .Where(x => x.AssessmentKind == "exam" && x.AssessmentId == IncidentExamId
                && x.Status == AssessmentParentDeliveryStatus.Failed && x.FailureCode == IncidentFailureCode
                && x.TemplateId == IncidentTemplateId && x.TemplateFingerprint == IncidentTemplateFingerprint
                && x.CreatedAt < IncidentCutoffUtc && !db.AuditLogs.Any(a => a.Action == AuditAction
                    && a.EntityType == "AssessmentParentDelivery" && a.EntityId == x.Id))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);

    private async Task RequireActiveAdminAsync(Guid actorId, CancellationToken ct)
    {
        if (actorId == Guid.Empty || !await db.Users.AsNoTracking().AnyAsync(user => user.Id == actorId
            && user.IsActive && !user.IsDeleted
            && user.UserRoles.Any(role => role.Role.Type == NaderGorge.Domain.Enums.RoleType.Admin), ct))
            throw new UnauthorizedAccessException("Active administrator required.");
    }

    private async Task<Candidate?> HydrateAsync(AssessmentParentDelivery delivery, CancellationToken ct)
    {
        var notification = new OutboxEvent { Type = "ExamGraded", TargetUserId = delivery.StudentUserId.ToString(),
            PayloadJson = JsonSerializer.Serialize(new { attemptId = delivery.AttemptId }) };
        var result = await AssessmentParentResultReader.ReadAsync(db, notification, ct);
        if (result is null || result.AssessmentId != IncidentExamId || !ExactSettings(result.Settings)) return null;
        var template = await db.LiveSupportWhatsAppTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == IncidentTemplateId, ct);
        if (template is null || template.Status != "APPROVED" || template.Category != "UTILITY"
            || template.Fingerprint != IncidentTemplateFingerprint) return null;
        var phone = ParentWhatsAppRecipients.Resolve(result.Student.StudentProfile,
            await ParentWhatsAppRecipients.ReadPriorityAsync(db, ct));
        if (phone is null || protector.DestinationHash(phone) != delivery.DestinationHash) return null;
        var preferences = await db.WhatsAppContactPreferences.AsNoTracking()
            .Where(x => x.DestinationHash == delivery.DestinationHash && x.EffectiveAt <= DateTime.UtcNow).ToListAsync(ct);
        if (!WhatsAppCampaignService.DestinationAllowsCampaign(preferences, "UTILITY")) return null;
        var parameters = await AssessmentParentResultReader.ParametersAsync(db, result, ct);
        var validated = WhatsAppDirectTemplatePolicy.Validate(template, parameters);
        if (validated is null) return null;
        var request = new WhatsAppCloudService.TemplateMessageRequest(phone, template.Name, template.Language,
            validated.ProviderComponents);
        var version = await ComputeGradeVersionAsync(db, delivery.AttemptId, result, ct);
        return new(delivery, result, request, version);
    }

    internal static async Task<string> ComputeGradeVersionAsync(
        AppDbContext database, Guid attemptId, AssessmentParentResult result, CancellationToken ct)
    {
        var attempt = await database.StudentExamAttempts.AsNoTracking().Include(x => x.Answers)
            .SingleAsync(x => x.Id == attemptId, ct);
        return Hash(JsonSerializer.Serialize(new {
            attempt.Id, attempt.DefinitionSnapshotJson, result.Score, result.Total, attempt.IsPassed,
            attempt.IsTimeExpired, result.Evaluation,
            Answers = attempt.Answers.OrderBy(x => x.ExamQuestionId)
                .Select(x => new { x.ExamQuestionId, x.PointsAwarded }).ToArray()
        }));
    }

    internal static bool ExactSettings(AssessmentParentNotificationSettings settings) => settings.Enabled
        && settings.TemplateId == IncidentTemplateId && settings.TemplateFingerprint == IncidentTemplateFingerprint
        && settings.Parameters.SequenceEqual(ExpectedParameters);
    private static string CohortFingerprint(IEnumerable<Candidate> rows) => Hash(string.Join("\n", rows.Select(x =>
        $"{x.Delivery.Id:N}|{x.Delivery.AttemptId:N}|{x.Delivery.CreatedAt:O}|{x.Delivery.PayloadDigest}|{x.GradeVersion}")));
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool FixedEquals(string a, string b) => a.Length == b.Length
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
    private static void Add(Dictionary<string, int> values, string key) => values[key] = values.GetValueOrDefault(key) + 1;
    private static string ReadCohortFingerprint(string? json)
    {
        using var document = JsonDocument.Parse(json ?? "{}");
        return document.RootElement.TryGetProperty("cohortFingerprint", out var value)
            ? value.GetString() ?? string.Empty : string.Empty;
    }
    private sealed record Candidate(AssessmentParentDelivery Delivery, AssessmentParentResult Result,
        WhatsAppCloudService.TemplateMessageRequest Request, string GradeVersion);
}
