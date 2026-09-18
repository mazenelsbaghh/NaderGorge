using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Interfaces;

public sealed record PromotionalFundingResult(
    Guid OperationId,
    decimal PromotionalAmount,
    decimal PaidAmount,
    IReadOnlyList<Guid> AllocationIds)
{
    public decimal PaidTeacherBalanceAmount { get; init; }
    public decimal TotalPaidAmount => PaidAmount + PaidTeacherBalanceAmount;
    public decimal GiftAmount => PromotionalAmount - PaidTeacherBalanceAmount;
}

public interface IPromotionalBalanceService
{
    Task ExpireAvailableAsync(Guid studentId, CancellationToken ct = default);
    Task<Guid?> ResolveTeacherIdAsync(CodeType contentType, Guid contentId, CancellationToken ct = default);
    Task<decimal> GetEligibleAmountAsync(Guid studentId, Guid? teacherId, CancellationToken ct = default);
    Task<PromotionalFundingResult> ConsumeAsync(
        Guid studentId,
        Guid? teacherId,
        CodeType contentType,
        Guid contentId,
        decimal price,
        CancellationToken ct = default);
}
