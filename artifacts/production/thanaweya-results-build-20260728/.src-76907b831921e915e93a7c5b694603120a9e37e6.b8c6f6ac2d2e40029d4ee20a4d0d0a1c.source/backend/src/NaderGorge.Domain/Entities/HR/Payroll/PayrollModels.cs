using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class PayComponent : BaseEntity
{
    public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty;
    public PayComponentClass Classification { get; set; } public bool IsTaxable { get; set; } public bool IsInsurable { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PayrollRule : BaseEntity
{
    public Guid PayComponentId { get; set; } public PayComponent? PayComponent { get; set; }
    public string Name { get; set; } = string.Empty; public string Expression { get; set; } = string.Empty;
    public decimal Rate { get; set; } public DateOnly EffectiveFrom { get; set; } public DateOnly? EffectiveTo { get; set; }
    public int Priority { get; set; } public int Version { get; set; } = 1; public bool IsActive { get; set; } = true;
}

public sealed class EmployeeCompensation : BaseEntity
{
    public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; }
    public decimal BaseSalary { get; set; } public string Currency { get; set; } = "EGP";
    public DateOnly EffectiveFrom { get; set; } public DateOnly? EffectiveTo { get; set; }
    public int Version { get; set; } = 1; public string Reason { get; set; } = string.Empty;
}

public sealed class HrPayrollRun : BaseEntity
{
    public string RunNumber { get; set; } = string.Empty; public DateOnly PeriodStart { get; set; } public DateOnly PeriodEnd { get; set; }
    public DateTime CutoffAt { get; set; } public HrPayrollRunStatus Status { get; set; } = HrPayrollRunStatus.Draft;
    public decimal TotalGross { get; set; } public decimal TotalDeductions { get; set; } public decimal TotalNet { get; set; }
    public Guid? PreparedByUserId { get; set; } public DateTime? PreparedAt { get; set; }
    public Guid? FinanceReviewedByUserId { get; set; } public DateTime? FinanceReviewedAt { get; set; }
    public Guid? GmApprovedByUserId { get; set; } public DateTime? GmApprovedAt { get; set; }
    public Guid? PaidByUserId { get; set; } public DateTime? PaidAt { get; set; } public DateTime? ClosedAt { get; set; }
    public string SourceDataVersion { get; set; } = string.Empty; public string ReconciliationHash { get; set; } = string.Empty;
    public int Version { get; set; } = 1; public ICollection<EmployeePayroll> Employees { get; set; } = new List<EmployeePayroll>();
}

public sealed class EmployeePayroll : BaseEntity
{
    public Guid PayrollRunId { get; set; } public HrPayrollRun? PayrollRun { get; set; }
    public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; }
    public string EmployeeNumberSnapshot { get; set; } = string.Empty; public string EmployeeNameSnapshot { get; set; } = string.Empty;
    public decimal BaseSalarySnapshot { get; set; } public string Currency { get; set; } = "EGP";
    public decimal Gross { get; set; } public decimal Deductions { get; set; } public decimal Net { get; set; }
    public EmployeePayrollStatus Status { get; set; } = EmployeePayrollStatus.Calculated;
    public ICollection<PayrollLineItem> Lines { get; set; } = new List<PayrollLineItem>();
}

public sealed class PayrollLineItem : BaseEntity
{
    public Guid EmployeePayrollId { get; set; } public EmployeePayroll? EmployeePayroll { get; set; }
    public Guid PayComponentId { get; set; } public PayComponent? PayComponent { get; set; }
    public decimal Amount { get; set; } public string InputsJson { get; set; } = "{}"; public string Explanation { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty; public Guid SourceId { get; set; }
    public Guid? RuleVersionId { get; set; } public PayrollRule? RuleVersion { get; set; } public bool IsAdjustment { get; set; }
}

public sealed class Payslip : BaseEntity
{
    public Guid EmployeePayrollId { get; set; } public EmployeePayroll? EmployeePayroll { get; set; }
    public int Version { get; set; } = 1; public string AssetReference { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty; public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PayrollSettlementAdjustment : BaseEntity
{
    public Guid OriginalPayrollLineItemId { get; set; } public PayrollLineItem? OriginalPayrollLineItem { get; set; }
    public Guid SettlementPayrollRunId { get; set; } public HrPayrollRun? SettlementPayrollRun { get; set; }
    public decimal Amount { get; set; } public string Reason { get; set; } = string.Empty; public Guid CreatedByUserId { get; set; }
}
