using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Interfaces;

public sealed record DiscountInput(
    IReadOnlyList<string> CouponCodes,
    IReadOnlyList<string> PrintableCodes
);

public sealed record DiscountLine(
    string SourceType,
    Guid SourceId,
    string Code,
    decimal Amount,
    string Label
);

public sealed record DiscountCalculationResult(
    bool Success,
    string? Error,
    Guid OperationId,
    SalesTargetType TargetType,
    Guid TargetId,
    decimal GrossAmount,
    decimal CouponDiscountAmount,
    decimal PrintableCodeDiscountAmount,
    decimal TotalDiscountAmount,
    IReadOnlyList<DiscountLine> Lines
);

public interface IDiscountEngine
{
    Task<DiscountCalculationResult> PreviewAsync(
        Guid studentId,
        SalesTargetContext target,
        DiscountInput input,
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<DiscountCalculationResult> CommitAsync(
        Guid studentId,
        SalesTargetContext target,
        DiscountInput input,
        Guid operationId,
        CancellationToken cancellationToken = default);
}
