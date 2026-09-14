using System.ComponentModel.DataAnnotations;
using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class LearningFollowUp : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;
    public Guid PerformedByUserId { get; set; }
    public User PerformedByUser { get; set; } = null!;
    [MaxLength(20)] public string Status { get; set; } = "New";
    [MaxLength(2000)] public string Note { get; set; } = string.Empty;
    [MaxLength(2000)] public string Reason { get; set; } = string.Empty;
}
