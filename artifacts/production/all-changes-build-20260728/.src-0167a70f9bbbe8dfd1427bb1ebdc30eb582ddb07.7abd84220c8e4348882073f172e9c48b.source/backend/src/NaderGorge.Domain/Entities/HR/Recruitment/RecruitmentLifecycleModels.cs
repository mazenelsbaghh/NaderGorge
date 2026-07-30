using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class Requisition : BaseEntity
{
    public string RequisitionNumber { get; set; } = string.Empty; public string Title { get; set; } = string.Empty;
    public Guid? OrganizationUnitId { get; set; } public OrganizationUnit? OrganizationUnit { get; set; }
    public int Openings { get; set; } = 1; public RequisitionState State { get; set; } = RequisitionState.Draft;
    public Guid RequestedByUserId { get; set; } public string Requirements { get; set; } = string.Empty;
    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}
public sealed class Candidate : BaseEntity
{
    public Guid RequisitionId { get; set; } public Requisition? Requisition { get; set; }
    public string FullName { get; set; } = string.Empty; public string PhoneNumber { get; set; } = string.Empty; public string? Email { get; set; }
    public CandidateStage Stage { get; set; } = CandidateStage.Applied; public string? CvAssetReference { get; set; }
    public Guid? EmployeeProfileId { get; set; } public EmployeeProfile? EmployeeProfile { get; set; } public int Version { get; set; } = 1;
    public ICollection<CandidateInterview> Interviews { get; set; } = new List<CandidateInterview>(); public ICollection<CandidateOffer> Offers { get; set; } = new List<CandidateOffer>();
}
public sealed class CandidateInterview : BaseEntity
{
    public Guid CandidateId { get; set; } public Candidate? Candidate { get; set; } public DateTime ScheduledAt { get; set; }
    public Guid InterviewerUserId { get; set; } public decimal? Score { get; set; } public string? Feedback { get; set; }
}
public sealed class CandidateOffer : BaseEntity
{
    public Guid CandidateId { get; set; } public Candidate? Candidate { get; set; } public string OfferNumber { get; set; } = string.Empty;
    public decimal BaseSalary { get; set; } public string Currency { get; set; } = "EGP"; public DateOnly ProposedStartDate { get; set; }
    public OfferState State { get; set; } = OfferState.Draft; public DateTime? AcceptedAt { get; set; } public int Version { get; set; } = 1;
}
public sealed class EmployeeLifecycleTask : BaseEntity
{
    public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; } public string Phase { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty; public Guid? AssignedToUserId { get; set; } public DateTime DueAt { get; set; }
    public LifecycleTaskState State { get; set; } = LifecycleTaskState.Pending; public DateTime? CompletedAt { get; set; } public string? CompletionNote { get; set; }
}
public sealed class OffboardingProcess : BaseEntity
{
    public Guid EmployeeId { get; set; } public EmployeeProfile? Employee { get; set; } public DateOnly LastWorkingDate { get; set; }
    public string Reason { get; set; } = string.Empty; public OffboardingState State { get; set; } = OffboardingState.Draft;
    public string BlockersJson { get; set; } = "[]"; public Guid InitiatedByUserId { get; set; } public Guid? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; } public int Version { get; set; } = 1;
}
