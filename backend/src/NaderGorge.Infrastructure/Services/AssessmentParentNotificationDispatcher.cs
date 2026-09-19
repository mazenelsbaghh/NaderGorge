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
    public async Task DispatchAsync(OutboxEvent notification, CancellationToken ct)
    {
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
        var delivery = await db.AssessmentParentDeliveries.AsNoTracking().SingleOrDefaultAsync(
            item => item.AssessmentKind == result.Kind && item.AttemptId == result.AttemptId, ct);
        if (delivery is null)
        {
            delivery = await PrepareAsync(result, ct);
            if (delivery is null) return;
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
        var isRecovery = recoveryAudit is not null;
        string? boundRecoveryGradeVersion = null;
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
        if (isRecovery)
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
                || claimedVersion != boundRecoveryGradeVersion
                || !AssessmentParentNotificationRecoveryService.ExactSettings(claimedResult.Settings)
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
        if (retry) throw new InvalidOperationException("Assessment parent notification provider requested retry.");
    }

    private async Task<AssessmentParentDelivery?> PrepareAsync(AssessmentParentResult result, CancellationToken ct)
    {
        var phone = ParentWhatsAppRecipients.Resolve(result.Student.StudentProfile,
            await ParentWhatsAppRecipients.ReadPriorityAsync(db, ct));
        if (phone is null) return null;
        var template = await db.LiveSupportWhatsAppTemplates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == result.Settings.TemplateId, ct);
        if (template?.Category != "UTILITY" || template.Fingerprint != result.Settings.TemplateFingerprint) return null;
        var parameters = await AssessmentParentResultReader.ParametersAsync(db, result, ct);
        var validated = WhatsAppDirectTemplatePolicy.Validate(template, parameters);
        if (validated is null) return null;
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
        db.AssessmentParentDeliveries.Add(delivery);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_assessment_parent_deliveries_AssessmentKind_AttemptId" })
        {
            db.Entry(delivery).State = EntityState.Detached;
            return await db.AssessmentParentDeliveries.AsNoTracking().SingleAsync(
                item => item.AssessmentKind == result.Kind && item.AttemptId == result.AttemptId, ct);
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
