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
    public string? TransferReference { get; set; }
    public string? AdminNote { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? PaidByUserId { get; set; }
    public User? PaidByUser { get; set; }
    public DateTime? PaidAt { get; set; }

    public Guid? HandledByUserId { get; set; }
    public User? HandledByUser { get; set; }

    public DateTime? HandledAt { get; set; }
    public ICollection<TeacherFinancialAllocation> Allocations { get; set; } = new List<TeacherFinancialAllocation>();
    public ICollection<TeacherPayoutAdjustment> Adjustments { get; set; } = new List<TeacherPayoutAdjustment>();
}
