namespace NaderGorge.Domain.Enums;

public enum LiveSupportEventType
{
    ConversationCreated, QueueEntered, QueuePositionChanged, Assigned, FirstStaffResponse,
    MessageSent, TransferRequested, Transferred, StaffDisconnected, StaffReconnected,
    AttendanceCheckedIn, AttendanceCheckedOut, StudentLinked, StudentUnlinked,
    StudentLinkReplaced, ActionRequested, ActionSucceeded, ActionFailed,
    ParticipantDisconnected, ParticipantReconnected, Closed, Abandoned, RatingSubmitted,
    FollowUpCreated, AdminIntervened
}
