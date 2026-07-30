namespace NaderGorge.Domain.Enums;

public enum PayComponentClass { Earning, Deduction, EmployerContribution, Informational }
public enum HrPayrollRunStatus { Draft, Prepared, FinanceReview, FinanceApproved, GMApproved, Paid, Closed, Returned }
public enum EmployeePayrollStatus { Calculated, Reviewed, Approved, Paid, Settled }
public enum HrFinancialRequestType { Advance, Loan, Expense, Commission }
public enum HrFinancialRequestState { PendingApproval, Approved, Rejected, Cancelled, Settled }
public enum HrInstallmentState { Scheduled, Applied, Paid, Cancelled }
