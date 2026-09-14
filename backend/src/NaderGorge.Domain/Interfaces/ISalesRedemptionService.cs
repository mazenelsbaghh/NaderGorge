namespace NaderGorge.Domain.Interfaces;

public sealed record SalesRedemptionResult(
    bool Success,
    string Message,
    Guid? GrantId,
    string? RedirectUrl
);

public interface ISalesRedemptionService
{
    Task<SalesRedemptionResult> RedeemPrintableCodeAsync(
        Guid studentId,
        Guid requestId,
        string code,
        CancellationToken cancellationToken = default);
}
