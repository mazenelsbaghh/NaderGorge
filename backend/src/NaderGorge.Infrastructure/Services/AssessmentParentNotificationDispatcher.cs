using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Infrastructure.Data;
using Npgsql;
using NaderGorge.Application.Features.Assessments;

namespace NaderGorge.Infrastructure.Services;

public sealed class AssessmentParentNotificationDispatcher(
    AppDbContext db, WhatsAppCloudService cloud, IWhatsAppCampaignDataProtector protector)
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public async Task DispatchAsync(OutboxEvent notification, CancellationToken ct)
    {
        ExamParentMessageRetryEnvelope? retryEnvelope = null;
        if (notification.Type == ExamParentMessageRetryService.EventType)
        {
            retryEnvelope = JsonSerializer.Deserialize<ExamParentMessageRetryEnvelope>(notification.PayloadJson, WebJson);
            if (retryEnvelope is null || retryEnvelope.OperationId == Guid.Empty || retryEnvelope.AttemptId == Guid.Empty) return;
            var boundAudit = await db.AuditLogs.AsNoTracking().AnyAsync(item =>
                item.Action == ExamParentMessageRetryService.AuditAction && item.EntityType == "Exam"
                && item.EntityId == retryEnvelope.ExamId && item.CorrelationId == retryEnvelope.OperationId.ToString("N")
                && item.NewValues != null && item.NewValues.Contains(retryEnvelope.AttemptId.ToString()), ct);
            if (!boundAudit) return;
        }
        AssessmentParentRecoveryEnvelope? recoveryEnvelope = null;
        if (notification.Type == "AssessmentParentRecovery")
        {
            try { recoveryEnvelope = JsonSerializer.Deserialize<AssessmentParentRecoveryEnvelope>(notification.PayloadJson); }
            catch (JsonException) { }
            if (recoveryEnvelope is null) return;
        }
        var result = await AssessmentParentResultReader.ReadAsync(db, notification, ct);
        if (result is null || !result.Settings.Enabled || result.EnabledAt is null
            || result.EnabledAt > notification.CreatedAt)
        {
            if (recoveryEnvelope is not null)
            {
                var bound = await db.AssessmentParentDeliveries.AsNoTracking().SingleOrDefaultAsync(item =>
                    item.Id == recoveryEnvelope.DeliveryId && item.AttemptId == recoveryEnvelope.AttemptId
                    && item.AssessmentKind == "exam"
                    && item.AssessmentId == AssessmentParentNotificationRecoveryService.IncidentExamId
                    && item.StudentUserId.ToString() == notification.TargetUserId, ct);
                var auditBound = bound is null ? false : await db.AuditLogs.AsNoTracking().AnyAsync(item =>
                    item.Action == "AssessmentParentRecoveryRebuilt"
                    && item.EntityType == "AssessmentParentDelivery" && item.EntityId == bound.Id
                    && item.CorrelationId == recoveryEnvelope.OperationId.ToString("N")
                    && item.NewValues != null && item.NewValues.Contains(bound.PayloadDigest), ct);
                if (bound is not null && auditBound)
                    await FinishAsync(bound.Id, AssessmentParentDeliveryStatus.Skipped,
                        "RECOVERY_RESULT_NO_LONGER_FINAL", null, ct, AssessmentParentDeliveryStatus.Pending);
            }
            return;
        }
        if (retryEnvelope is not null && (result.AssessmentId != retryEnvelope.ExamId || result.AttemptId != retryEnvelope.AttemptId)) return;
        var delivery = await db.AssessmentParentDeliveries.AsNoTracking().SingleOrDefaultAsync(
            item => item.AssessmentKind == result.Kind && item.AttemptId == result.AttemptId, ct);
        if (retryEnvelope is not null && delivery?.MetaMessageId is { } previousMessageId)
        {
            await AssessmentParentDeliveryReceipts.ReconcileAsync(db, previousMessageId, ct);
            delivery = await db.AssessmentParentDeliveries.AsNoTracking().SingleAsync(item => item.Id == delivery.Id, ct);
        }
        if (retryEnvelope is not null && delivery?.Status == AssessmentParentDeliveryStatus.Failed)
        {
            if (!await RebuildFailedAsync(delivery, result, retryEnvelope.OperationId, ct)) return;
            delivery = await db.AssessmentParentDeliveries.AsNoTracking().SingleAsync(item => item.Id == delivery.Id, ct);
        }
        if (delivery is null)
        {
            delivery = await PrepareAsync(result, ct);
            if (delivery is null) return;
        }
        if (retryEnvelope is not null && delivery.Status == AssessmentParentDeliveryStatus.Pending
            && !await db.AuditLogs.AnyAsync(item => item.Action == "ExamParentRetryPrepared" && item.EntityId == delivery.Id
                && item.CorrelationId == retryEnvelope.OperationId.ToString("N"), ct))
        {
            db.AuditLogs.Add(new AuditLog
            {
                Action = "ExamParentRetryPrepared", EntityType = "AssessmentParentDelivery", EntityId = delivery.Id,
                CorrelationId = retryEnvelope.OperationId.ToString("N"), ActorType = "System",
                NewValues = JsonSerializer.Serialize(new { payloadDigest = delivery.PayloadDigest })
            });
            await db.SaveChangesAsync(ct);
        }
        if (delivery.Status == AssessmentParentDeliveryStatus.Sending)
        {
            // A provider request may have succeeded before the process stopped. Never resend blindly.
            await db.AssessmentParentDeliveries.Where(item => item.Id == delivery.Id
                    && item.Status == AssessmentParentDeliveryStatus.Sending
                    && item.ClaimedAt < DateTime.UtcNow.AddMinutes(-5))
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, AssessmentParentDeliveryStatus.Uncertain)
                    .SetProperty(item => item.FailureCode, "INTERRUPTED_DELIVERY"), ct);
            return;
        }
        if (delivery.Status != AssessmentParentDeliveryStatus.Pending) return;
        var recoveryAudit = await db.AuditLogs.AsNoTracking()
            .Where(item => item.Action == "AssessmentParentRecoveryRebuilt"
                && item.EntityType == "AssessmentParentDelivery" && item.EntityId == delivery.Id)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => item.NewValues).FirstOrDefaultAsync(ct);
        var isRecovery = retryEnvelope is null && recoveryAudit is not null;
        string? boundRecoveryGradeVersion = null;
        var boundRetryGradeVersion = retryEnvelope is null ? null
            : await AssessmentParentNotificationRecoveryService.ComputeGradeVersionAsync(db, delivery.AttemptId, result, ct);
        if (isRecovery && recoveryEnvelope is null) return;
        if (recoveryEnvelope is not null && (!isRecovery || recoveryEnvelope.DeliveryId != delivery.Id
            || recoveryEnvelope.AttemptId != delivery.AttemptId))
        {
            await FinishAsync(delivery.Id, AssessmentParentDeliveryStatus.Skipped,
                "RECOVERY_AUDIT_MISSING_OR_MISMATCHED", null, ct, AssessmentParentDeliveryStatus.Pending);
            return;
        }
        if (isRecovery)
        {
            string? expectedGradeVersion = null;
            string? expectedPayloadDigest = null;
            string? expectedTemplateFingerprint = null;
            int recoveryVersion = 0;
            try
            {
                using var audit = JsonDocument.Parse(recoveryAudit!);
                expectedGradeVersion = audit.RootElement.GetProperty("gradeVersion").GetString();
                expectedPayloadDigest = audit.RootElement.GetProperty("payloadDigest").GetString();
                expectedTemplateFingerprint = audit.RootElement.GetProperty("templateFingerprint").GetString();
                recoveryVersion = audit.RootElement.GetProperty("recoveryVersion").GetInt32();
                boundRecoveryGradeVersion = expectedGradeVersion;
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException) { }
            var currentGradeVersion = await AssessmentParentNotificationRecoveryService
                .ComputeGradeVersionAsync(db, delivery.AttemptId, result, ct);
            if (string.IsNullOrWhiteSpace(expectedGradeVersion)
                || recoveryVersion != 1 || expectedPayloadDigest != delivery.PayloadDigest
                || expectedTemplateFingerprint != delivery.TemplateFingerprint
                || (recoveryEnvelope is not null && (recoveryEnvelope.GradeVersion != expectedGradeVersion
                    || recoveryEnvelope.OperationId.ToString("N") != (await db.AuditLogs.AsNoTracking()
                        .Where(item => item.Action == "AssessmentParentRecoveryRebuilt" && item.EntityId == delivery.Id)
                        .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
                        .Select(item => item.CorrelationId).FirstAsync(ct))))
                || !string.Equals(expectedGradeVersion, currentGradeVersion, StringComparison.Ordinal)
                || !AssessmentParentNotificationRecoveryService.ExactSettings(result.Settings))
            {
                await FinishAsync(delivery.Id, AssessmentParentDeliveryStatus.Skipped,
                    "RECOVERY_GRADE_OR_TEMPLATE_CHANGED", null, ct, AssessmentParentDeliveryStatus.Pending);
                return;
            }
        }
        var template = await db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == delivery.TemplateId, ct);
        var currentPhone = ParentWhatsAppRecipients.Resolve(result.Student.StudentProfile,
            await ParentWhatsAppRecipients.ReadPriorityAsync(db, ct));
        var preferences = await db.WhatsAppContactPreferences.AsNoTracking()
            .Where(item => item.DestinationHash == delivery.DestinationHash && item.EffectiveAt <= DateTime.UtcNow)
            .ToListAsync(ct);
        if (template is null || template.Status != "APPROVED" || template.Category != "UTILITY"
            || template.Fingerprint != delivery.TemplateFingerprint
            || result.Settings.TemplateId != delivery.TemplateId
            || result.Settings.TemplateFingerprint != delivery.TemplateFingerprint
            || currentPhone is null || protector.DestinationHash(currentPhone) != delivery.DestinationHash
            || !WhatsAppCampaignService.DestinationAllowsCampaign(preferences, "UTILITY"))
        {
            await FinishAsync(delivery.Id, AssessmentParentDeliveryStatus.Skipped, "RECIPIENT_OR_TEMPLATE_CHANGED", null, ct, AssessmentParentDeliveryStatus.Pending);
            return;
        }
        var request = JsonSerializer.Deserialize<WhatsAppCloudService.TemplateMessageRequest>(
            protector.Unprotect(delivery.Id, delivery.ProtectedPayload, delivery.PayloadDigest))
            ?? throw new InvalidOperationException("Invalid protected assessment notification.");
        var claimed = await db.AssessmentParentDeliveries.Where(item => item.Id == delivery.Id
                && item.Status == AssessmentParentDeliveryStatus.Pending)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, AssessmentParentDeliveryStatus.Sending)
                .SetProperty(item => item.ClaimedAt, DateTime.UtcNow)
                .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1), ct);
        if (claimed != 1) return;
        if (isRecovery || retryEnvelope is not null)
        {
            var claimedResult = await AssessmentParentResultReader.ReadAsync(db, notification, ct);
            var claimedPhone = claimedResult is null ? null : ParentWhatsAppRecipients.Resolve(
                claimedResult.Student.StudentProfile, await ParentWhatsAppRecipients.ReadPriorityAsync(db, ct));
            var claimedVersion = claimedResult is null ? null : await AssessmentParentNotificationRecoveryService
                .ComputeGradeVersionAsync(db, delivery.AttemptId, claimedResult, ct);
            var templateCurrent = await db.LiveSupportWhatsAppTemplates.AsNoTracking().AnyAsync(item =>
                item.Id == delivery.TemplateId && item.Status == "APPROVED" && item.Category == "UTILITY"
                && item.Fingerprint == delivery.TemplateFingerprint, ct);
            var claimedPreferences = await db.WhatsAppContactPreferences.AsNoTracking()
                .Where(item => item.DestinationHash == delivery.DestinationHash && item.EffectiveAt <= DateTime.UtcNow)
                .ToListAsync(ct);
            if (claimedResult is null || claimedResult.EnabledAt is null
                || claimedResult.EnabledAt > notification.CreatedAt
                || claimedVersion != (boundRecoveryGradeVersion ?? boundRetryGradeVersion)
                || (isRecovery ? !AssessmentParentNotificationRecoveryService.ExactSettings(claimedResult.Settings)
                    : claimedResult.Settings.ToJson() != result.Settings.ToJson())
                || claimedPhone is null || protector.DestinationHash(claimedPhone) != delivery.DestinationHash
                || !templateCurrent || !WhatsAppCampaignService.DestinationAllowsCampaign(claimedPreferences, "UTILITY"))
            {
                await FinishAsync(delivery.Id, AssessmentParentDeliveryStatus.Skipped,
                    "RECOVERY_PRE_SEND_AUTHORITY_CHANGED", null, ct);
                return;
            }
        }
        var response = await cloud.SendTemplateAsync(request, ct);
        var ambiguous = WhatsAppCampaignDispatcher.IsAmbiguous(response);
        var retry = !isRecovery && !response.Success && !ambiguous && response.IsRetryable && delivery.AttemptCount < 4;
        var status = response.Success ? AssessmentParentDeliveryStatus.Sent
            : ambiguous ? AssessmentParentDeliveryStatus.Uncertain
            : retry ? AssessmentParentDeliveryStatus.Pending : AssessmentParentDeliveryStatus.Failed;
        await FinishAsync(delivery.Id, status, response.ErrorCode, response.MetaMessageId, ct);
        if (response.Success && response.MetaMessageId is not null)
            await AssessmentParentDeliveryReceipts.ReconcileAsync(db, response.MetaMessageId, ct);
        if (retry) throw new InvalidOperationException("Assessment parent notification provider requested retry.");
    }

    private async Task<bool> RebuildFailedAsync(AssessmentParentDelivery delivery, AssessmentParentResult result,
        Guid operationId, CancellationToken ct)
    {
        var phone = ParentWhatsAppRecipients.Resolve(result.Student.StudentProfile,
            await ParentWhatsAppRecipients.ReadPriorityAsync(db, ct));
        var template = await db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == result.Settings.TemplateId, ct);
        if (phone is null || template?.Category != "UTILITY" || template.Fingerprint != result.Settings.TemplateFingerprint) return false;
        var parameters = await AssessmentParentResultReader.ParametersAsync(db, result, ct, template);
        var validated = WhatsAppDirectTemplatePolicy.Validate(template, parameters);
        if (validated is null) return false;
        var request = new WhatsAppCloudService.TemplateMessageRequest(phone, template.Name, template.Language, validated.ProviderComponents);
        var payload = protector.Protect(delivery.Id, JsonSerializer.SerializeToUtf8Bytes(request));
        var digest = protector.Digest(delivery.Id, payload);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.AuditLogs.AnyAsync(item => item.Action == "ExamParentRetryPrepared" && item.EntityId == delivery.Id
                && item.CorrelationId == operationId.ToString("N"), ct)) return false;
        var rebuilt = await db.AssessmentParentDeliveries.Where(item => item.Id == delivery.Id
                && item.Status == AssessmentParentDeliveryStatus.Failed
                && !db.AuditLogs.Any(audit => audit.Action == "ExamParentRetryPrepared" && audit.EntityId == item.Id
                    && audit.CorrelationId == operationId.ToString("N"))
                && !db.LiveSupportWhatsAppPendingReceipts.Any(receipt => receipt.MetaMessageId == item.MetaMessageId
                    && (receipt.Status == "Read" || receipt.Status == "Delivered")))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, AssessmentParentDeliveryStatus.Pending)
                .SetProperty(item => item.TemplateId, template.Id).SetProperty(item => item.TemplateFingerprint, template.Fingerprint)
                .SetProperty(item => item.DestinationHash, protector.DestinationHash(phone))
                .SetProperty(item => item.ProtectedPayload, payload).SetProperty(item => item.PayloadDigest, digest)
                .SetProperty(item => item.AttemptCount, 0).SetProperty(item => item.ClaimedAt, (DateTime?)null)
                .SetProperty(item => item.SentAt, (DateTime?)null).SetProperty(item => item.MetaMessageId, (string?)null)
                .SetProperty(item => item.FailureCode, (string?)null), ct);
        if (rebuilt == 1)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Action = "ExamParentRetryPrepared", EntityType = "AssessmentParentDelivery", EntityId = delivery.Id,
                CorrelationId = operationId.ToString("N"), ActorType = "System",
                OldValues = JsonSerializer.Serialize(new { delivery.Status, delivery.MetaMessageId, delivery.FailureCode, delivery.AttemptCount }),
                NewValues = JsonSerializer.Serialize(new { payloadDigest = digest })
            });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return rebuilt == 1;
    }

    private async Task<AssessmentParentDelivery?> PrepareAsync(AssessmentParentResult result, CancellationToken ct)
    {
        var phone = ParentWhatsAppRecipients.Resolve(result.Student.StudentProfile,
            await ParentWhatsAppRecipients.ReadPriorityAsync(db, ct));
        if (phone is null) return await PrepareFailedAsync(result, "PARENT_PHONE_NOT_FOUND", ct);
        var template = await db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == result.Settings.TemplateId, ct);
        if (template?.Category != "UTILITY" || template.Fingerprint != result.Settings.TemplateFingerprint)
            return await PrepareFailedAsync(result, "RESULT_TEMPLATE_CHANGED", ct);
        var parameters = await AssessmentParentResultReader.ParametersAsync(db, result, ct, template);
        var validated = WhatsAppDirectTemplatePolicy.Validate(template, parameters);
        if (validated is null) return await PrepareFailedAsync(result, "RESULT_TEMPLATE_PARAMETERS_INVALID", ct);
        var delivery = new AssessmentParentDelivery
        {
            AssessmentKind = result.Kind, AssessmentId = result.AssessmentId, AttemptId = result.AttemptId,
            StudentUserId = result.Student.Id, TemplateId = template.Id, TemplateFingerprint = template.Fingerprint,
            DestinationHash = protector.DestinationHash(phone), Status = AssessmentParentDeliveryStatus.Pending
        };
        var request = new WhatsAppCloudService.TemplateMessageRequest(phone, template.Name, template.Language,
            validated.ProviderComponents);
        delivery.ProtectedPayload = protector.Protect(delivery.Id, JsonSerializer.SerializeToUtf8Bytes(request));
        delivery.PayloadDigest = protector.Digest(delivery.Id, delivery.ProtectedPayload);
        return await StoreDeliveryAsync(delivery, ct);
    }

    private Task<AssessmentParentDelivery> PrepareFailedAsync(AssessmentParentResult result, string code, CancellationToken ct) =>
        StoreDeliveryAsync(new AssessmentParentDelivery
        {
            AssessmentKind = result.Kind, AssessmentId = result.AssessmentId, AttemptId = result.AttemptId,
            StudentUserId = result.Student.Id, TemplateId = result.Settings.TemplateId ?? Guid.Empty,
            TemplateFingerprint = result.Settings.TemplateFingerprint ?? string.Empty,
            Status = AssessmentParentDeliveryStatus.Failed, FailureCode = code
        }, ct);

    private async Task<AssessmentParentDelivery> StoreDeliveryAsync(AssessmentParentDelivery delivery, CancellationToken ct)
    {
        db.AssessmentParentDeliveries.Add(delivery);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_assessment_parent_deliveries_AssessmentKind_AttemptId" })
        {
            db.Entry(delivery).State = EntityState.Detached;
            return await db.AssessmentParentDeliveries.AsNoTracking().SingleAsync(
                item => item.AssessmentKind == delivery.AssessmentKind && item.AttemptId == delivery.AttemptId, ct);
        }
        db.Entry(delivery).State = EntityState.Detached;
        return delivery;
    }

    private Task<int> FinishAsync(Guid id, AssessmentParentDeliveryStatus status, string? failure,
        string? messageId, CancellationToken ct, AssessmentParentDeliveryStatus expected = AssessmentParentDeliveryStatus.Sending) => db.AssessmentParentDeliveries
        .Where(item => item.Id == id && item.Status == expected)
        .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, status)
            .SetProperty(item => item.FailureCode, failure)
            .SetProperty(item => item.MetaMessageId, messageId)
            .SetProperty(item => item.SentAt, status == AssessmentParentDeliveryStatus.Sent ? DateTime.UtcNow : (DateTime?)null), ct);
}
