namespace NaderGorge.Domain.Enums;

public enum LiveSupportAIPolicyStatus { Draft, Published, Superseded }
public enum LiveSupportAIMode { AiActive, HumanQueued, HumanAssigned, AiResolved, Failed, Closed }
public enum LiveSupportAITurnStatus { Queued, Processing, Completed, Failed, DiscardedAfterHandoff, DiscardedAfterDisable }
public enum LiveSupportAIDecisionType { Reply, ProposeAction, RequestVerification, ProposeAccountCreation, RequestResolution, Handoff }
public enum LiveSupportAIPendingActionStatus { PendingConfirmation, Confirmed, Cancelled, Expired, Invalidated, Executing, Succeeded, Failed }
public enum LiveSupportAIVerificationStatus { AwaitingLookup, Challenging, Verified, Failed, Exhausted, Ambiguous, Cancelled, HandedOff }
public enum LiveSupportAIComparisonMode { ExactNormalized, Date, PhoneDigits }
