using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportMessengerConfiguration : BaseEntity
{
    public const string DefaultConfigurationKey = "default";

    public string ConfigurationKey { get; set; } = DefaultConfigurationKey;
    public string AppId { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "v26.0";
    public byte[]? AppSecretCiphertext { get; set; }
    public byte[]? VerifyTokenCiphertext { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? VerifyTokenRotatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public long Version { get; set; }
}

public sealed class LiveSupportMessengerPage : BaseEntity
{
    public string PageId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public byte[] PageAccessTokenCiphertext { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
    public bool HumanAgentEnabled { get; set; }
    public string ConnectionStatus { get; set; } = "Pending";
    public bool? TokenValid { get; set; }
    public bool? IsSubscribed { get; set; }
    public DateTime? LastCredentialCheckAt { get; set; }
    public DateTime? LastSubscriptionCheckAt { get; set; }
    public string? LastErrorCode { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public long Version { get; set; }
}

public sealed class LiveSupportMessengerBinding : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid GuestSessionId { get; set; }
    public string PageId { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string SenderPsid { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public DateTime LastInboundAt { get; set; }
    public DateTime ReplyWindowExpiresAt { get; set; }
    public long Version { get; set; }
}

public sealed class LiveSupportMessengerMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid? LiveSupportMessageId { get; set; }
    public string PageId { get; set; } = string.Empty;
    public string SenderPsid { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FailureCode { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? ProviderTimestamp { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public long Version { get; set; }
}

public sealed class LiveSupportMessengerWebhookInbox : BaseEntity
{
    public string PageId { get; set; } = string.Empty;
    public string EventKind { get; set; } = string.Empty;
    public string DeduplicationKey { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? FailureCode { get; set; }
    public long Version { get; set; }
}
