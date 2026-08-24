using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportWhatsAppBinding : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid GuestSessionId { get; set; }
    public string WhatsAppUserId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime LastInboundAt { get; set; }
    public DateTime CustomerServiceWindowExpiresAt { get; set; }
    public long Version { get; set; }
}

public sealed class LiveSupportWhatsAppMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid? LiveSupportMessageId { get; set; }
    public string? MetaMessageId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string? TemplateLanguage { get; set; }
    public string? TemplateParametersJson { get; set; }
    public string? FailureCode { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? ProviderTimestamp { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public long Version { get; set; }
}

public sealed class LiveSupportWhatsAppPendingReceipt : BaseEntity
{
    public string MetaMessageId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ProviderTimestamp { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? FailureCode { get; set; }
    public long Version { get; set; }
}

public sealed class LiveSupportWhatsAppTemplate : BaseEntity
{
    public string MetaTemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ComponentsJson { get; set; } = "[]";
    public DateTime LastSyncedAt { get; set; }
    public long Version { get; set; }
}
