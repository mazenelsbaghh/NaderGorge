using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class EmploymentContract : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public EmployeeProfile? Employee { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public EmploymentContractType Type { get; set; }
    public EmploymentContractStatus Status { get; set; } = EmploymentContractStatus.Draft;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? ProbationEndDate { get; set; }
    public decimal BaseSalary { get; set; }
    public string Currency { get; set; } = "EGP";
    public int TermsVersion { get; set; } = 1;
    public string? TermsJson { get; set; }
}
