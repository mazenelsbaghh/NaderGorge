namespace NaderGorge.Domain.Enums;

public enum LiveSupportAIPolicyStatus { Draft = 0, Published = 1, Superseded = 2 }
public enum LiveSupportAIMode { AiActive = 0, HumanQueued = 1, HumanAssigned = 2, AiResolved = 3, Failed = 4, Closed = 5 }
public enum LiveSupportAITurnStatus
{
    Queued = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    DiscardedAfterHandoff = 4,
    DiscardedAfterDisable = 5,
    ProviderCompleted = 6,
    Cancelled = 7
}
public enum LiveSupportAIDecisionType { Reply = 0, ProposeAction = 1, RequestVerification = 2, ProposeAccountCreation = 3, RequestResolution = 4, Handoff = 5 }
public enum LiveSupportAIPendingActionStatus { PendingConfirmation = 0, Confirmed = 1, Cancelled = 2, Expired = 3, Invalidated = 4, Executing = 5, Succeeded = 6, Failed = 7 }
public enum LiveSupportAIPendingDecisionKind { Action = 0, Handoff = 1, AccountCreation = 2, Resolution = 3 }
public enum LiveSupportAICallbackStatus { NotReady = 0, Pending = 1, Delivered = 2, Failed = 3, Discarded = 4 }
public enum LiveSupportAIVerificationStatus { AwaitingLookup = 0, Challenging = 1, Verified = 2, Failed = 3, Exhausted = 4, Ambiguous = 5, Cancelled = 6, HandedOff = 7 }
public enum LiveSupportAIComparisonMode { ExactNormalized = 0, Date = 1, PhoneDigits = 2 }
