using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Admin.PlatformFinance.Refunds;

public sealed record PlatformRefundDraftCommand(
    Guid OriginalSourceId,
    string OriginalSourceType,
    Guid StudentId,
    Guid? TeacherId,
    decimal PlatformAmount,
    decimal TeacherAmount,
    PlatformRefundMethod Method,
    Guid? TreasuryAccountId,
    string Reason,
    string? PaymentReference);

public sealed record PlatformRefundPostingCommand(Guid RefundId, string IdempotencyKey);

public static class PlatformRefundCommandValidator
{
    public static void ValidateDraft(PlatformRefundDraftCommand command)
    {
        if (command.OriginalSourceId == Guid.Empty || command.StudentId == Guid.Empty)
            throw new InvalidOperationException("FINANCE_REFUND_SOURCE_REQUIRED");
        if (command.PlatformAmount < 0m || command.TeacherAmount < 0m || command.PlatformAmount + command.TeacherAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(command.PlatformAmount));
        if (!Enum.IsDefined(command.Method)) throw new InvalidOperationException("FINANCE_INVALID_REFUND_METHOD");
        if (command.Method == PlatformRefundMethod.Cash && !command.TreasuryAccountId.HasValue)
            throw new InvalidOperationException("FINANCE_TREASURY_REQUIRED");
        if (string.IsNullOrWhiteSpace(command.Reason)) throw new InvalidOperationException("FINANCE_REASON_REQUIRED");
    }

    public static void ValidatePosting(PlatformRefundPostingCommand command)
    {
        if (command.RefundId == Guid.Empty) throw new InvalidOperationException("FINANCE_REFUND_REQUIRED");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) throw new InvalidOperationException("FINANCE_IDEMPOTENCY_REQUIRED");
    }
}
