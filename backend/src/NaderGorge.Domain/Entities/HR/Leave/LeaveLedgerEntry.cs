using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;
namespace NaderGorge.Domain.Entities;
public sealed class LeaveLedgerEntry : BaseEntity
{
    public Guid LeaveBalanceId { get; set; } public LeaveBalance? LeaveBalance { get; set; }
    public LeaveLedgerEntryType EntryType { get; set; } public decimal Amount { get; set; }
    public string SourceType { get; set; } = string.Empty; public Guid SourceId { get; set; }
    public string Reason { get; set; } = string.Empty; public Guid? ActorUserId { get; set; }
}
