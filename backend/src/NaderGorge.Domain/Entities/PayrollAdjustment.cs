using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class PayrollAdjustment : BaseEntity
{
    public Guid PayrollRecordId { get; set; }
    public PayrollRecord PayrollRecord { get; set; } = null!;

    public PayrollAdjustmentType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}
