using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public interface ILiveSupportService
{
    Task<LiveSupportAvailabilityDto> GetAvailabilityAsync(CancellationToken ct);
    Task<IReadOnlyList<LiveSupportConversationDto>> ListParticipantConversationsAsync(LiveSupportParticipantIdentity participant, CancellationToken ct);
    Task<LiveSupportConversationDto> CreateConversationAsync(LiveSupportParticipantIdentity participant, string? subject, Guid? previousConversationId, CancellationToken ct);
    Task<LiveSupportConversationDto?> GetParticipantConversationAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct);
    Task<IReadOnlyList<LiveSupportMessageDto>> GetParticipantMessagesAsync(LiveSupportParticipantIdentity participant, Guid conversationId, int pageSize, CancellationToken ct);
    Task<LiveSupportSendResultDto> SendParticipantMessageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, string clientMessageId, string content, LiveSupportMessageType type, CancellationToken ct);
    Task<LiveSupportSendResultDto> SendParticipantAttachmentMessageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, string clientMessageId, Guid attachmentId, string? caption, LiveSupportMessageType type, CancellationToken ct);
    Task<LiveSupportConversationDto> AbandonAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct);
    Task SubmitRatingAsync(LiveSupportParticipantIdentity participant, Guid conversationId, int stars, string? comment, CancellationToken ct);
    Task<LiveSupportStaffBootstrapDto> GetStaffBootstrapAsync(Guid staffUserId, bool isAdmin, CancellationToken ct);
    Task<IReadOnlyList<LiveSupportMessageDto>> GetStaffMessagesAsync(Guid staffUserId, bool isAdmin, Guid conversationId, int pageSize, CancellationToken ct);
    Task<long> GetStaffLastEventSequenceAsync(Guid staffUserId, bool isAdmin, Guid conversationId, CancellationToken ct);
    Task<LiveSupportSendResultDto> SendStaffMessageAsync(Guid staffUserId, bool isAdmin, Guid conversationId, string clientMessageId, string content, CancellationToken ct);
    Task<LiveSupportConversationDto> CloseAsync(Guid staffUserId, bool isAdmin, Guid conversationId, string reason, CancellationToken ct);
    Task<LiveSupportConversationDto> TransferAsync(Guid staffUserId, bool isAdmin, Guid conversationId, Guid? targetStaffUserId, string reason, CancellationToken ct);
    Task<LiveSupportAdminConfigDto> GetAdminConfigAsync(CancellationToken ct);
    Task SetFeatureEnabledAsync(bool enabled, CancellationToken ct);
    Task<LiveSupportStaffConfigDto> UpdateStaffConfigAsync(Guid actorUserId, Guid staffUserId, bool enabled, int capacity, long? expectedVersion, IReadOnlyList<LiveSupportScheduleWindowDto> schedule, CancellationToken ct);
    Task ReleaseStaffAssignmentsAsync(Guid staffUserId, LiveSupportAssignmentEndReason reason, CancellationToken ct);
    Task<IReadOnlyList<LiveSupportStudentSearchDto>> SearchStudentsAsync(Guid staffUserId, bool isAdmin, Guid conversationId, string query, CancellationToken ct);
    Task<LiveSupportConversationDto> ChangeStudentLinkAsync(Guid staffUserId, bool isAdmin, Guid conversationId, Guid? studentUserId, string reason, long expectedVersion, CancellationToken ct);
    Task<LiveSupportStudentContextDto> GetStudentContextAsync(Guid staffUserId, bool isAdmin, Guid conversationId, CancellationToken ct);
    Task<LiveSupportAdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct);
    Task<LiveSupportConversationTimelineDto> GetAdminTimelineAsync(Guid conversationId, CancellationToken ct);
    Task<LiveSupportMessagePageDto> GetParticipantMessagePageAsync(LiveSupportParticipantIdentity participant, Guid conversationId, int pageSize, string? cursor, long? afterSequence, CancellationToken ct);
    Task<LiveSupportAttachmentDto> SaveParticipantAttachmentAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct);
    Task<LiveSupportAttachmentDownloadDto> OpenParticipantAttachmentAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Guid attachmentId, CancellationToken ct);
    Task<LiveSupportConversationDto> AdminInterveneAsync(Guid adminUserId, Guid conversationId, string operation, Guid? targetStaffUserId, string reason, CancellationToken ct);
    Task<LiveSupportAITurnContextDto?> ClaimAITurnAsync(Guid turnId, CancellationToken ct);
    Task CompleteAITurnAsync(Guid turnId, LiveSupportAITurnCompleteRequest request, CancellationToken ct);
    Task FailAITurnAsync(Guid turnId, LiveSupportAITurnFailRequest request, CancellationToken ct);
    Task ConfirmPendingActionAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Guid proposalId, CancellationToken ct);
    Task CancelPendingActionAsync(LiveSupportParticipantIdentity participant, Guid conversationId, Guid proposalId, CancellationToken ct);
    Task ConfirmHandoffAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct);
    Task CancelHandoffAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct);
    Task<LiveSupportAIVerificationSessionDto> StartVerificationLookupAsync(LiveSupportParticipantIdentity participant, Guid conversationId, LiveSupportLookupRequestDto request, CancellationToken ct);
    Task<LiveSupportAIVerificationSessionDto> SubmitVerificationChallengeAsync(LiveSupportParticipantIdentity participant, Guid conversationId, LiveSupportAnswerChallengeDto request, CancellationToken ct);
    Task ConfirmRegistrationProposalAsync(LiveSupportParticipantIdentity participant, Guid conversationId, LiveSupportRegisterGuestDto request, CancellationToken ct);
    Task<LiveSupportAIPendingActionDto?> GetActivePendingActionAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct);
    Task<LiveSupportAIVerificationSessionDto?> GetActiveVerificationSessionAsync(LiveSupportParticipantIdentity participant, Guid conversationId, CancellationToken ct);
}

public interface ILiveSupportPresenceStore
{
    Task ConnectedAsync(Guid staffUserId, string connectionId);
    Task DisconnectedAsync(Guid staffUserId, string connectionId);
    Task HeartbeatAsync(Guid staffUserId);
    Task<bool> IsConnectedAsync(Guid staffUserId);
    Task<IReadOnlyList<Guid>> ClaimExpiredDisconnectsAsync(DateTime utcNow);
}

public sealed class LiveSupportException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
