using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Interfaces.Finance;

public sealed record CreatePlatformExpenseRequest(
    decimal Amount,
    DateTime OccurredAt,
    Guid CategoryId,
    Guid? CostCenterId,
    Guid? VendorId,
    string Description,
    string? DocumentNumber,
    Guid CreatedByUserId);

public sealed record PostPlatformExpenseRequest(
    Guid? TreasuryAccountId,
    Guid ActorUserId,
    string IdempotencyKey,
    string? Reason);

public sealed record PayPlatformExpenseRequest(
    Guid TreasuryAccountId,
    decimal Amount,
    string PaymentReference,
    Guid ActorUserId,
    string IdempotencyKey);

public sealed record CreatePlatformRefundRequest(
    Guid OriginalSourceId,
    string OriginalSourceType,
    Guid StudentId,
    Guid? TeacherId,
    decimal PlatformAmount,
    decimal TeacherAmount,
    int Method,
    Guid? TreasuryAccountId,
    string Reason,
    string? PaymentReference,
    Guid CreatedByUserId);

public interface IPlatformFinanceOperationsService
{
    Task<PlatformExpense> CreateExpenseAsync(CreatePlatformExpenseRequest request, CancellationToken ct);
    Task<PlatformExpense> PostExpenseAsync(Guid expenseId, PostPlatformExpenseRequest request, CancellationToken ct);
    Task<ExpensePayment> PayExpenseAsync(Guid expenseId, PayPlatformExpenseRequest request, CancellationToken ct);
    Task<PlatformRefund> CreateRefundAsync(CreatePlatformRefundRequest request, CancellationToken ct);
    Task<PlatformRefund> PostRefundAsync(Guid refundId, string idempotencyKey, Guid actorUserId, CancellationToken ct);
}
