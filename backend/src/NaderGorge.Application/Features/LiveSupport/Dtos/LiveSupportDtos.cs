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
    string Message);

public sealed record LiveSupportMessageDto(
    Guid Id,
    Guid ConversationId,
    LiveSupportSenderType SenderType,
    string ClientMessageId,
    LiveSupportMessageType Type,
    string Content,
    DateTime SentAt);

public sealed record LiveSupportConversationDto(
    Guid Id,
    LiveSupportParticipantType ParticipantType,
    LiveSupportConversationStatus Status,
    Guid? CurrentOwnerUserId,
    Guid? LinkedStudentUserId,
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
    bool IsAiTyping);

public sealed record LiveSupportGuestSessionDto(Guid Id, string DisplayName, DateTime ExpiresAt, string CookieToken);

public sealed record LiveSupportStaffBootstrapDto(
    bool IsEnabled,
    bool IsCheckedIn,
    int ActiveLoad,
    int Capacity,
    int WaitingCount,
    IReadOnlyList<LiveSupportConversationDto> Conversations);

public sealed record LiveSupportSendResultDto(LiveSupportMessageDto Message, bool Replayed);
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

public sealed record LiveSupportAdminConfigDto(bool FeatureEnabled, IReadOnlyList<LiveSupportStaffConfigDto> Staff);

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

public sealed record LiveSupportAdminConversationDto(Guid Id, string ParticipantName, LiveSupportParticipantType ParticipantType, LiveSupportConversationStatus Status, string? OwnerName, DateTime CreatedAt, DateTime? AssignedAt, DateTime? FirstResponseAt, DateTime? ClosedAt, double? WaitSeconds, double? HandleSeconds, string? Subject);
public sealed record LiveSupportStaffPerformanceDto(Guid StaffUserId, string StaffName, int ParticipatedConversations, int ClosedConversations, int RatingCount, double? AverageRating);
public sealed record LiveSupportAdminDashboardDto(int WaitingCount, int ActiveCount, int ClosedToday, IReadOnlyList<LiveSupportAdminConversationDto> Conversations, IReadOnlyList<LiveSupportStaffPerformanceDto> StaffPerformance);
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
    LiveSupportAIDecisionHandoff? Handoff
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
