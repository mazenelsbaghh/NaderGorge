using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Infrastructure.Data;
using Npgsql;

namespace NaderGorge.Infrastructure.Services;

public sealed class AssessmentParentNotificationDispatcher(
    AppDbContext db, WhatsAppCloudService cloud, IWhatsAppCampaignDataProtector protector)
{
    public async Task DispatchAsync(OutboxEvent notification, CancellationToken ct)
    {
        var result = await AssessmentParentResultReader.ReadAsync(db, notification, ct);
        if (result is null || !result.Settings.Enabled || result.EnabledAt is null
            || result.EnabledAt > notification.CreatedAt) return;
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
        var response = await cloud.SendTemplateAsync(request, ct);
        var ambiguous = WhatsAppCampaignDispatcher.IsAmbiguous(response);
        var retry = !response.Success && !ambiguous && response.IsRetryable && delivery.AttemptCount < 4;
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
