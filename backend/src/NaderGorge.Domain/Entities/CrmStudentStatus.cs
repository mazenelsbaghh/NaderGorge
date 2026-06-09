using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class CrmStudentStatus
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public CrmStatus Status { get; set; } = CrmStatus.Unassigned;

    public Guid? AssignedAgentId { get; set; }
    public User? AssignedAgent { get; set; }

    public CrmPriority Priority { get; set; } = CrmPriority.Medium;

    public DateTime? NextFollowUpDate { get; set; }
    public DateTime? LastCalledAt { get; set; }
    public string? Notes { get; set; }
}
