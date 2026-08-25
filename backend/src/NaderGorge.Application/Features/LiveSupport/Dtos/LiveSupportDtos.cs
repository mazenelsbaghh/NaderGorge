using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.LiveSupport.Dtos;

public sealed record LiveSupportParticipantIdentity(
    LiveSupportParticipantType Type,
    Guid? StudentUserId,
    Guid? GuestSessionId);

public sealed record LiveSupportAvailabilityDto(
    bool IsAvailable,
    int AvailableStaffCount,
    DateTime? NextAvailableAt,
    string Code,
    string Message,
    IReadOnlyList<LiveSupportScheduleWindowDto>? BusinessHours = null,
    bool IsOutsideBusinessHours = false);

public sealed record LiveSupportMessageDto(
    Guid Id,
    Guid ConversationId,
    LiveSupportSenderType SenderType,
    string ClientMessageId,
    LiveSupportMessageType Type,
    string Content,
    DateTime SentAt,
    Guid? AttachmentId,
    DateTime? DeliveredAt,
    DateTime? ReadAt,
    DateTime? EditedAt,
    DateTime? DeletedAt,
    string? SenderDisplayName = null,
    LiveSupportReplyDto? ReplyTo = null,
    string? ExternalDeliveryStatus = null);

public sealed record LiveSupportReplyDto(Guid Id, LiveSupportSenderType SenderType, LiveSupportMessageType Type, string Content, bool IsDeleted);

public sealed record UpdateLiveSupportMessageDto(string Content);

public sealed record LiveSupportAISummaryDto(
    string? HandoffSafeSummary,
    string? HandoffReasonCode,
    long? PolicyVersion,
    string? VerificationStatus,
    IReadOnlyList<string> AttemptedActionKeys,
    IReadOnlyList<string> FailedTurnErrors);

public sealed record LiveSupportConversationDto(
    Guid Id,
    LiveSupportParticipantType ParticipantType,
    LiveSupportConversationStatus Status,
    Guid? CurrentOwnerUserId,
    Guid? LinkedStudentUserId,
    string? ParticipantName,
    string? Subject,
    DateTime CreatedAt,
    DateTime? QueuedAt,
    DateTime? AssignedAt,
    DateTime? ClosedAt,
    int? QueuePosition,
    long Version,
    bool CanSend,
    bool CanRate,
    bool IsAiActive,
    bool IsAiTyping,
    LiveSupportAISummaryDto? AiSummary,
    int UnreadParticipantMessageCount = 0,
    string Channel = "Web",
    string? ExternalPhoneNumber = null,
    DateTime? CustomerServiceWindowExpiresAt = null);

public sealed record LiveSupportWhatsAppTemplateDto(
    Guid Id,
    string Name,
    string Language,
    string Category,
    string Status,
    System.Text.Json.JsonElement Components,
    DateTime LastSyncedAt,
    long Version,
    string Fingerprint);

public sealed record SendLiveSupportWhatsAppTemplateRequest(
    string ClientMessageId,
    Guid TemplateId,
    IReadOnlyList<string> Parameters,
    string PreviewText);

public sealed record SendLiveSupportWhatsAppTemplateCommand(
    Guid StaffUserId,
    bool IsAdmin,
    Guid ConversationId,
    SendLiveSupportWhatsAppTemplateRequest Request);

public sealed record LiveSupportGuestSessionDto(Guid Id, string DisplayName, DateTime ExpiresAt, string CookieToken);

public sealed record LiveSupportStaffBootstrapDto(
    bool IsEnabled,
    bool IsCheckedIn,
    int ActiveLoad,
    int Capacity,
    int WaitingCount,
    IReadOnlyList<LiveSupportConversationDto> Conversations,
    IReadOnlyList<LiveSupportCannedReplyDto> CannedReplies);

public sealed record LiveSupportSendResultDto(LiveSupportMessageDto Message, bool Replayed);
public sealed record LiveSupportExternalMessage(
    LiveSupportParticipantIdentity Participant,
    Guid ConversationId,
    string ClientMessageId,
    string Content,
    LiveSupportMessageType Type,
    Guid? AttachmentId = null);
public sealed record LiveSupportMessagePageDto(IReadOnlyList<LiveSupportMessageDto> Items, string? NextCursor, long LastEventSequence, IReadOnlyList<LiveSupportTimelineItemDto> MissedEvents);
public sealed record LiveSupportAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string DownloadUrl);
public sealed record LiveSupportAttachmentDownloadDto(Stream Content, string FileName, string ContentType, long SizeBytes);

public sealed record LiveSupportScheduleWindowDto(int DayOfWeek, TimeOnly StartLocalTime, TimeOnly EndLocalTime);

public sealed record LiveSupportStaffConfigDto(
    Guid UserId,
    string StaffName,
    bool IsEnabled,
    int MaxActiveConversations,
    int ActiveLoad,
    bool IsCheckedIn,
    long Version,
    IReadOnlyList<LiveSupportScheduleWindowDto> Schedule);

public sealed record LiveSupportCannedReplyDto(string Id, string Title, string Content, bool SendImmediately);
public sealed record LiveSupportAdminConfigDto(bool FeatureEnabled, IReadOnlyList<LiveSupportStaffConfigDto> Staff, IReadOnlyList<LiveSupportCannedReplyDto> CannedReplies);

public sealed record LiveSupportStudentSearchDto(Guid UserId, string FullName, string MaskedPhone, string? StudentCode);
public sealed record LiveSupportDeviceDto(Guid Id, string? Name, string? Type, string? Os, string? Browser, DateTime LastUsedAt, bool IsActive);
public sealed record LiveSupportGrantDto(Guid Id, string GrantType, Guid? PackageId, DateTime GrantedAt, DateTime? ExpiresAt, bool IsActive);
public sealed record LiveSupportNoteDto(Guid Id, string Content, bool IsPinned, DateTime CreatedAt);
public sealed record LiveSupportStudentContextDto(
    Guid UserId, string FullName, string PhoneNumber, bool IsActive, string? StudentCode,
    string? Governorate, string? SchoolName, string? EducationStage, string? GradeLevel,
    decimal Balance, int Points, string? Level, string? CrmStatus, string? CrmPriority,
    IReadOnlyList<LiveSupportDeviceDto> Devices, IReadOnlyList<LiveSupportGrantDto> Grants,
    IReadOnlyList<LiveSupportNoteDto> Notes, int WatchEvents, int ExamAttempts, int HomeworkSubmissions);
public sealed record LiveSupportStudentContextSectionDto(string Section, System.Text.Json.JsonElement Data);
public sealed record LiveSupportStudentSupportHistoryDto(
    Guid ConversationId,
    LiveSupportConversationStatus Status,
    string? Subject,
    DateTime StartedAt,
    DateTime? EndedAt,
    DateTime LastActivityAt,
    int MessageCount,
    string? LastMessagePreview,
    string? LastEventType,
    IReadOnlyList<LiveSupportStudentSupportActivityDto> Activities);
public sealed record LiveSupportStudentSupportActivityDto(DateTime At, string Type);

public sealed record LiveSupportAdminConversationDto(Guid Id, string ParticipantName, LiveSupportParticipantType ParticipantType, LiveSupportConversationStatus Status, string? OwnerName, DateTime CreatedAt, DateTime? AssignedAt, DateTime? FirstResponseAt, DateTime? ClosedAt, double? WaitSeconds, double? HandleSeconds, string? Subject, string? AiTurnStatus, string? AiTurnFailureCode, string Channel = "Web", string? ExternalPhoneNumber = null, DateTime? CustomerServiceWindowExpiresAt = null, string? LastExternalDeliveryStatus = null);
public sealed record LiveSupportStaffPerformanceDto(Guid StaffUserId, string StaffName, int ParticipatedConversations, int ClosedConversations, int RatingCount, double? AverageRating);
public sealed record LiveSupportWhatsAppOperationsSummaryDto(int Open, int Waiting, int Active, int ClosedToday, int FailedOutbound, int ApprovedTemplates, DateTime? LastInboundAt, DateTime? LastOutboundAt, DateTime? LastTemplateSyncAt);
public sealed record LiveSupportAdminDashboardDto(int WaitingCount, int ActiveCount, int ClosedToday, IReadOnlyList<LiveSupportAdminConversationDto> Conversations, IReadOnlyList<LiveSupportStaffPerformanceDto> StaffPerformance, LiveSupportWhatsAppOperationsSummaryDto WhatsApp);
public sealed record LiveSupportRatingDto(Guid Id, Guid ConversationId, int Stars, string? Comment, DateTime SubmittedAt, string SubmittedByName, bool IsStudent);
public sealed record LiveSupportTimelineItemDto(DateTime At, string Type, string? ActorName, string Summary, string? SafeDetails);
public sealed record LiveSupportConversationTimelineDto(LiveSupportAdminConversationDto Conversation, IReadOnlyList<LiveSupportTimelineItemDto> Items, int? RatingStars, string? RatingComment);

public static class LiveSupportErrorCodes
{
    public const string SupportUnavailable = "LIVE_SUPPORT_UNAVAILABLE";
    public const string OpenConversationExists = "LIVE_SUPPORT_OPEN_CONVERSATION_EXISTS";
    public const string ConversationTerminal = "LIVE_SUPPORT_CONVERSATION_TERMINAL";
    public const string Forbidden = "LIVE_SUPPORT_FORBIDDEN";
    public const string MessageConflict = "LIVE_SUPPORT_MESSAGE_CONFLICT";
    public const string RatingConflict = "LIVE_SUPPORT_RATING_CONFLICT";
    public const string AudioStaffOnly = "LIVE_SUPPORT_AUDIO_STAFF_ONLY";
    public const string WhatsAppMessageImmutable = "LIVE_SUPPORT_WHATSAPP_MESSAGE_IMMUTABLE";
}

public sealed record LiveSupportAITurnContextDto(
    Guid TurnId,
    Guid ConversationId,
    Guid PolicyVersionId,
    long ExpectedConversationVersion,
    string SystemInstructions,
    List<string> KnowledgeDocuments,
    List<LiveSupportMessageDto> Messages,
    string ParticipantType
);

public sealed record LiveSupportAITurnCompleteRequest(
    long ExpectedConversationVersion,
    LiveSupportAIDecision Decision,
    string Provider,
    string Model,
    string? ProviderResponseId,
    int? InputTokenCount,
    int? OutputTokenCount,
    int? LatencyMs,
    string CallbackIdempotencyKey
);

public sealed record LiveSupportAIDecision(
    string Type,
    string? MessageAr,
    LiveSupportAIDecisionAction? Action,
    LiveSupportAIDecisionVerification? Verification,
    LiveSupportAIDecisionAccountCreation? AccountCreation,
    LiveSupportAIDecisionHandoff? Handoff
);

public sealed record LiveSupportAIDecisionAction(
    string Key,
    System.Text.Json.Nodes.JsonObject? Arguments,
    string SafeEffectSummaryAr
);

public sealed record LiveSupportAIDecisionVerification(
    string Intent
);

public sealed record LiveSupportAIDecisionAccountCreation(
    IReadOnlyList<string> RequestedFields
);

public sealed record LiveSupportAIDecisionHandoff(
    string ReasonCode,
    string SafeSummaryAr
);

public sealed record LiveSupportAITurnFailRequest(
    string FailureCode,
    string? SafeFailureDetail,
    string Provider,
    string Model,
    int? LatencyMs,
    string CallbackIdempotencyKey
);

public sealed record LiveSupportLookupRequestDto(
    string LookupKey,
    string Value
);

public sealed record LiveSupportAnswerChallengeDto(
    string Answer
);

public sealed record LiveSupportAIVerificationSessionDto(
    Guid SessionId,
    string Status,
    string? NextQuestionKey,
    string? PromptText,
    int AttemptCount,
    int MaxAttempts
);

public sealed record LiveSupportAIPendingActionDto(
    Guid Id,
    string ActionKey,
    string SafeProposalJson,
    string Status,
    DateTime ExpiresAt
);

public sealed record LiveSupportRegisterGuestDto(
    string FullName,
    string PhoneNumber,
    string Password,
    string Governorate,
    string EducationStage,
    string GradeLevel,
    string SchoolName,
    string ParentPhoneNumber
);
