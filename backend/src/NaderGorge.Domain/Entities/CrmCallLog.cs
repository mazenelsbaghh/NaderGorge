using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class CrmCallLog : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public DateTime CallDate { get; set; } = DateTime.UtcNow;
    public CallOutcome Outcome { get; set; }
    public string? Notes { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
}
