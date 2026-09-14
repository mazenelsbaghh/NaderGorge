namespace NaderGorge.Domain.Enums;

public enum EmployeeDocumentCategory { Identity, Contract, Qualification, Medical, Payroll, Other }
public enum HrAssetStatus { Available, Assigned, Maintenance, Retired, Lost }
public enum AssetCustodyState { Active, Returned, Lost, Waived }
public enum PerformanceCycleState { Draft, Active, Closed }
public enum PerformanceReviewState { Draft, SelfSubmitted, ManagerSubmitted, Published, Appealed, Resolved }
public enum EmployeeCaseState { Open, UnderInvestigation, AwaitingResponse, Decided, Closed }
public enum DisciplinaryActionType { Warning, Suspension, FinancialPenalty, Termination, NoAction }
public enum RequisitionState { Draft, Open, OnHold, Filled, Cancelled }
public enum CandidateStage { Applied, Screening, Interview, Offer, Hired, Rejected, Withdrawn }
public enum OfferState { Draft, Sent, Accepted, Rejected, Expired, Converted }
public enum LifecycleTaskState { Pending, InProgress, Completed, Waived }
public enum OffboardingState { Draft, Blocked, InProgress, Completed, Cancelled }
