namespace NaderGorge.Domain.Enums;

public enum LiveSupportEventType
{
    ConversationCreated, QueueEntered, QueuePositionChanged, Assigned, FirstStaffResponse,
    MessageSent, TransferRequested, Transferred, StaffDisconnected, StaffReconnected,
    AttendanceCheckedIn, AttendanceCheckedOut, StudentLinked, StudentUnlinked,
    StudentLinkReplaced, ActionRequested, ActionSucceeded, ActionFailed,
    ParticipantDisconnected, ParticipantReconnected, Closed, Abandoned, RatingSubmitted,
    FollowUpCreated, AdminIntervened,
    AIPolicyPublished, AIPolicyDisabled, AITurnQueued, AITurnStarted,
    AITurnCompleted, AITurnFailed, AIReplySent, AIActionProposed,
    AIActionConfirmed, AIActionCancelled, AIActionSucceeded, AIActionFailed,
    AIVerificationStarted, AIVerificationAttempted, AIVerificationSucceeded,
    AIVerificationFailed, AIHandoffRequested, AIHandoffCompleted,
    AIInactivityWarningSent, AIResolved, AIAutoClosed
}
