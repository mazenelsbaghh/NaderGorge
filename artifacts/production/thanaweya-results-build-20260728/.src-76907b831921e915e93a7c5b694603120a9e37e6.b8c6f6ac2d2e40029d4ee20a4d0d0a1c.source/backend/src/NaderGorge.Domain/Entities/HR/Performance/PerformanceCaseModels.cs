using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class PerformanceCycle : BaseEntity
{
    public string Name { get; set; } = string.Empty; public DateOnly StartsOn { get; set; } public DateOnly EndsOn { get; set; }
    public PerformanceCycleState State { get; set; } = PerformanceCycleState.Draft;
    public ICollection<PerformanceGoal> Goals { get; set; } = new List<PerformanceGoal>();
}
public sealed class PerformanceGoal : BaseEntity
{
    public Guid PerformanceCycleId { get; set; } public PerformanceCycle? PerformanceCycle { get; set; }
    public string Name { get; set; } = string.Empty; public decimal Weight { get; set; }
}
public sealed class PerformanceReview : BaseEntity
{
    public Guid PerformanceCycleId { get; set; } public PerformanceCycle? PerformanceCycle { get; set; }
    public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; } public Guid ManagerUserId { get; set; }
    public string ScoresJson { get; set; } = "{}"; public decimal WeightedScore { get; set; }
    public PerformanceReviewState State { get; set; } = PerformanceReviewState.Draft; public DateTime? PublishedAt { get; set; }
    public string? AppealReason { get; set; } public string? AppealResolution { get; set; } public int Version { get; set; } = 1;
}
public sealed class EmployeeCase : BaseEntity
{
    public string CaseNumber { get; set; } = string.Empty; public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; }
    public Guid OpenedByUserId { get; set; } public string Title { get; set; } = string.Empty; public string Description { get; set; } = string.Empty;
    public bool IsConfidential { get; set; } = true; public EmployeeCaseState State { get; set; } = EmployeeCaseState.Open; public int Version { get; set; } = 1;
    public ICollection<CaseEvidence> Evidence { get; set; } = new List<CaseEvidence>(); public ICollection<CaseResponse> Responses { get; set; } = new List<CaseResponse>();
    public ICollection<DisciplinaryAction> Actions { get; set; } = new List<DisciplinaryAction>();
}
public sealed class CaseEvidence : BaseEntity
{
    public Guid EmployeeCaseId { get; set; } public EmployeeCase? EmployeeCase { get; set; } public string AssetReference { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty; public Guid AddedByUserId { get; set; }
}
public sealed class CaseResponse : BaseEntity
{
    public Guid EmployeeCaseId { get; set; } public EmployeeCase? EmployeeCase { get; set; } public Guid SubmittedByUserId { get; set; }
    public string Response { get; set; } = string.Empty; public string? AttachmentReference { get; set; }
}
public sealed class DisciplinaryAction : BaseEntity
{
    public Guid EmployeeCaseId { get; set; } public EmployeeCase? EmployeeCase { get; set; } public DisciplinaryActionType Type { get; set; }
    public string Reason { get; set; } = string.Empty; public decimal? FinancialAmount { get; set; } public Guid ApprovedByUserId { get; set; }
    public Guid? PayrollLineItemId { get; set; } public PayrollLineItem? PayrollLineItem { get; set; }
}
