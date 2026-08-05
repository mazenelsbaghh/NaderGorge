using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Admin.PlatformFinance.Expenses;

public sealed record PlatformExpenseDraftCommand(
    decimal Amount,
    DateTime OccurredAt,
    Guid CategoryId,
    Guid? CostCenterId,
    Guid? VendorId,
    string Description,
    string? DocumentNumber);

public sealed record PlatformExpensePostingCommand(
    Guid ExpenseId,
    Guid? TreasuryAccountId,
    string IdempotencyKey,
    string? Reason);

public sealed record PlatformExpensePaymentCommand(
    Guid ExpenseId,
    Guid TreasuryAccountId,
    decimal Amount,
    string PaymentReference,
    string IdempotencyKey);

public static class PlatformExpenseCommandValidator
{
    public static void ValidateDraft(PlatformExpenseDraftCommand command)
    {
        if (command.Amount <= 0m) throw new ArgumentOutOfRangeException(nameof(command.Amount));
        if (string.IsNullOrWhiteSpace(command.Description)) throw new InvalidOperationException("FINANCE_EXPENSE_DESCRIPTION_REQUIRED");
        if (command.Description.Length > 1000) throw new InvalidOperationException("FINANCE_EXPENSE_DESCRIPTION_TOO_LONG");
        if (command.DocumentNumber?.Length > 80) throw new InvalidOperationException("FINANCE_EXPENSE_DOCUMENT_TOO_LONG");
    }

    public static void ValidatePosting(PlatformExpensePostingCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) throw new InvalidOperationException("FINANCE_IDEMPOTENCY_REQUIRED");
    }

    public static void ValidatePayment(PlatformExpensePaymentCommand command)
    {
        if (command.Amount <= 0m) throw new ArgumentOutOfRangeException(nameof(command.Amount));
        if (string.IsNullOrWhiteSpace(command.PaymentReference)) throw new InvalidOperationException("FINANCE_PAYMENT_REFERENCE_REQUIRED");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) throw new InvalidOperationException("FINANCE_IDEMPOTENCY_REQUIRED");
    }
}
