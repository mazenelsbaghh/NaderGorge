using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAIAuditWriter(
    IAppDbContext db,
    IAdminAISensitiveDataPolicy sensitiveDataPolicy,
    IAdminAIDataProtector dataProtector) : IAdminAIAuditWriter
{
    public Task WriteAsync(
        string eventType,
        Guid? actorId,
        Guid? conversationId,
        Guid? turnId,
        Guid? proposalId,
        object safeEvidence,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AdminAIAuditEventType>(eventType, ignoreCase: false, out var parsedEventType) || !Enum.IsDefined(parsedEventType))
            throw new InvalidOperationException($"Unknown Admin AI audit event type '{eventType}'.");
        ArgumentNullException.ThrowIfNull(safeEvidence);
        sensitiveDataPolicy.AssertSafeSchema(safeEvidence.GetType());

        var evidenceJson = sensitiveDataPolicy.RedactJson(JsonSerializer.Serialize(safeEvidence));
        if (Encoding.UTF8.GetByteCount(evidenceJson) > 16_384)
            throw new InvalidOperationException("Admin AI audit evidence exceeds the safe size limit.");

        using var evidenceDocument = JsonDocument.Parse(evidenceJson);
        var evidenceRoot = evidenceDocument.RootElement;
        if (ContainsForbiddenAuditField(evidenceRoot))
            throw new InvalidOperationException("Raw transcript or unrestricted audit values are forbidden in Admin AI evidence.");
        var executionId = ReadGuid(evidenceRoot, "executionId");
        var capabilityKey = ReadBoundedString(evidenceRoot, "capabilityKey", 160);
        var safeTargetReference = ReadBoundedString(evidenceRoot, "safeTargetReference", 200);
        if (IsTerminalExecutionEvent(parsedEventType) && (!proposalId.HasValue || !executionId.HasValue))
            throw new InvalidOperationException("Terminal execution audit events require proposalId and executionId correlation.");

        var activity = Activity.Current;
        var correlationId = activity?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var traceId = activity?.TraceId.ToString() ?? correlationId;
        var evidenceHash = dataProtector.Digest("admin-ai-audit-evidence", Encoding.UTF8.GetBytes(evidenceJson));

        db.AdminAIAuditEvents.Add(new AdminAIAuditEvent
        {
            EventType = parsedEventType,
            ActorAdminUserId = actorId,
            ConversationId = conversationId,
            TurnId = turnId,
            ProposalId = proposalId,
            ExecutionId = executionId,
            CapabilityKey = capabilityKey,
            SafeTargetReference = safeTargetReference,
            SafeEvidenceJson = evidenceJson,
            EvidenceHash = evidenceHash,
            CorrelationId = correlationId,
            TraceId = traceId
        });
        db.AuditLogs.Add(new AuditLog
        {
            Action = $"AdminAI.{parsedEventType}",
            EntityType = proposalId.HasValue ? "AdminAIActionProposal" : turnId.HasValue ? "AdminAITurn" : "AdminAIConversation",
            EntityId = proposalId ?? turnId ?? conversationId,
            PerformedByUserId = actorId,
            ActorType = actorId.HasValue ? "User" : "System",
            CorrelationId = correlationId,
            Reason = $"Admin AI append-only evidence summary {evidenceHash}"
        });
        return Task.CompletedTask;
    }

    private static bool IsTerminalExecutionEvent(AdminAIAuditEventType eventType) => eventType is
        AdminAIAuditEventType.ExecutionSucceeded or
        AdminAIAuditEventType.ExecutionPartiallySucceeded or
        AdminAIAuditEventType.ExecutionRejected or
        AdminAIAuditEventType.ExecutionFailed or
        AdminAIAuditEventType.ExecutionRecoveryRequired;

    private static Guid? ReadGuid(JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        return Guid.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    private static string? ReadBoundedString(JsonElement root, string name, int maxLength)
    {
        if (!TryGetProperty(root, name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) || text.Length > maxLength ? null : text;
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object)
            foreach (var property in root.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
        value = default;
        return false;
    }

    private static bool ContainsForbiddenAuditField(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
            {
                var normalized = new string(property.Name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                if (normalized is "rawtranscript" or "oldvalues" or "newvalues") return true;
                if (ContainsForbiddenAuditField(property.Value)) return true;
            }
        if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                if (ContainsForbiddenAuditField(item)) return true;
        return false;
    }
}
