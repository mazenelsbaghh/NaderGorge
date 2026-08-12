namespace NaderGorge.Domain.Enums;

public enum AdminAICapabilityBaselineStatus { Draft, Active, Superseded, Rejected }
public enum AdminAISensitiveDataPolicyStatus { Draft, Active, Superseded }
public enum AdminAIConversationStatus { Active, Archived }
public enum AdminAIMessageRole { Admin, Assistant, Status }
public enum AdminAITurnStatus { Queued, Planning, Retrieving, Answering, WaitingClarification, ProposalReady, Completed, CancelRequested, Cancelled, Failed, AccessRevoked }
public enum AdminAITurnStepStatus { Queued, Claimed, ProviderRunning, ReadsRequested, ReadsCompleted, Completed, Cancelled, Failed, Superseded }
public enum AdminAIModelDecisionType { Answer, Clarify, RequestReads, ProposeActions, Refuse }
public enum AdminAIReadInvocationStatus { Pending, Running, Succeeded, Empty, Truncated, Rejected, Cancelled, Failed }
public enum AdminAICapabilityKind { Read, Preview, Export, Mutation, ExternalSideEffect, SecureContinuation, Excluded }
public enum AdminAIRiskCategory { Ordinary, Destructive, Financial, Permission, Security, AccountDisable, Credential, Bulk, ExternalSideEffect }
public enum AdminAIConfirmationType { Explicit, TypedStrong }
public enum AdminAIProposalStatus { PendingSecureInput, PendingConfirmation, Confirming, Executing, Succeeded, PartiallySucceeded, Cancelled, Expired, Invalidated, Rejected, Failed, RecoveryRequired }
public enum AdminAIChallengeStatus { Pending, Accepted, Rejected, Locked, Expired, Cancelled }
public enum AdminAISecureInputGrantStatus { Issued, Submitted, Consumed, Cancelled, Expired, Purged }
public enum AdminAIExecutionStatus { Claimed, Executing, Succeeded, PartiallySucceeded, Rejected, Failed, RecoveryRequired }
public enum AdminAIExecutionItemStatus { Succeeded, Skipped, ValidationFailed, AuthorizationFailed, Stale, DependencyFailed, SystemFailed }
public enum AdminAIAuditEventType { ConversationCreated, ConversationRenamed, ConversationArchived, ConversationRestored, TurnQueued, TurnClaimed, TurnCancelled, TurnFailed, ReadStarted, ReadCompleted, ReadRejected, AnswerCompleted, ClarificationRequested, RequestRefused, ProposalCreated, ProposalCancelled, ProposalExpired, ProposalInvalidated, SecureInputIssued, SecureInputConsumed, ConfirmationAccepted, ConfirmationRejected, ExecutionStarted, ExecutionSucceeded, ExecutionPartiallySucceeded, ExecutionRejected, ExecutionFailed, ExecutionRecoveryRequired, AccessRevoked, BaselineActivated, SensitivePolicyActivated }

public static class AdminAITurnStatusExtensions
{
    public static bool IsTerminal(this AdminAITurnStatus status) => status is AdminAITurnStatus.Completed or AdminAITurnStatus.Cancelled or AdminAITurnStatus.Failed or AdminAITurnStatus.AccessRevoked;
    public static bool IsActive(this AdminAITurnStatus status) => !status.IsTerminal();
}
