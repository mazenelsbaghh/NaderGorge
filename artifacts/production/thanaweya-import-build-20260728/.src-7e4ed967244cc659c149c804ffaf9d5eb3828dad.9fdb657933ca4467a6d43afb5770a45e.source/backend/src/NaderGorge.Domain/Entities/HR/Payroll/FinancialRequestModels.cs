using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class HrFinancialRequest : BaseEntity
{
    public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; }
    public HrFinancialRequestType Type { get; set; } public HrFinancialRequestState State { get; set; } = HrFinancialRequestState.PendingApproval;
    public decimal Amount { get; set; } public decimal OutstandingBalance { get; set; }
    public int RequestedInstallments { get; set; } = 1; public string Reason { get; set; } = string.Empty;
    public string? AttachmentReference { get; set; } public Guid? ApprovalInstanceId { get; set; }
    public int Version { get; set; } = 1; public ICollection<HrFinancialInstallment> Installments { get; set; } = new List<HrFinancialInstallment>();
}

public sealed class HrFinancialInstallment : BaseEntity
{
    public Guid FinancialRequestId { get; set; } public HrFinancialRequest? FinancialRequest { get; set; }
    public int Sequence { get; set; } public DateOnly DueDate { get; set; } public decimal Amount { get; set; }
    public HrInstallmentState State { get; set; } = HrInstallmentState.Scheduled;
    public Guid? PayrollLineItemId { get; set; } public PayrollLineItem? PayrollLineItem { get; set; }
    public DateTime? AppliedAt { get; set; }
}

public sealed class HrPayrollInputSource : BaseEntity
{
    public string SourceType { get; set; } = string.Empty; public Guid SourceId { get; set; }
    public Guid EmployeePayrollId { get; set; } public EmployeePayroll? EmployeePayroll { get; set; }
    public Guid PayrollLineItemId { get; set; } public PayrollLineItem? PayrollLineItem { get; set; }
}
