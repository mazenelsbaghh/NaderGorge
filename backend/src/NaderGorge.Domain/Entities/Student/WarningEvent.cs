using System;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Entities.Student;

public enum WarningSeverity
{
    Low,
    Medium,
    Critical
}

public class WarningEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    
    public WarningSeverity Severity { get; set; }
    public string TriggerReason { get; set; } = string.Empty;
    public bool IsResolved { get; set; } = false;
    
    public Guid? ResolvedByAssistantId { get; set; }
    public string? ResolutionNotes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Student { get; set; } = null!;
    public User? ResolvedByAssistant { get; set; }
}
