using System;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Entities.Gamification;

public class StudentBadge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    
    public string BadgeName { get; set; } = string.Empty;
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

    public User Student { get; set; } = null!;
}
