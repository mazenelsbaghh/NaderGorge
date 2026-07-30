namespace NaderGorge.Domain.Enums;

public enum LeaveRequestState { Draft, PendingApproval, Approved, Rejected, Withdrawn, Cancelled }
public enum LeaveLedgerEntryType { Grant, Carryover, Reserve, Release, Debit, Credit, Expire, Adjustment }
public enum ApprovalInstanceState { Pending, Approved, Rejected, Cancelled }
public enum ApprovalStepState { Pending, Approved, Rejected, Escalated, Skipped }
public enum ApprovalApproverKind { DirectManager, Permission, SpecificUser }
