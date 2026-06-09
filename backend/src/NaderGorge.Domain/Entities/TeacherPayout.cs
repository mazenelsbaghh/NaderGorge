using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class TeacherPayout : BaseEntity
{
    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;

    public decimal Amount { get; set; }
    
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
    public string? RejectionReason { get; set; }

    public Guid? HandledByUserId { get; set; }
    public User? HandledByUser { get; set; }

    public DateTime? HandledAt { get; set; }
}
