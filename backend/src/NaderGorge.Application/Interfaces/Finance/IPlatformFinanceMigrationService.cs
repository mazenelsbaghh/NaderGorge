namespace NaderGorge.Application.Interfaces.Finance;

public sealed record FinanceHistoricalMigrationPreview(
    DateTime From,
    DateTime To,
    int RechargeCandidates,
    decimal RechargeAmount,
    int SaleCandidates,
    decimal SaleAmount,
    int AmbiguousCandidates,
    IReadOnlyList<string> Ambiguities);

public sealed record FinanceHistoricalMigrationResult(
    Guid BatchId,
    int Posted,
    int AlreadyPosted,
    int Failed,
    IReadOnlyList<string> Errors);

public interface IPlatformFinanceMigrationService
{
    Task<FinanceHistoricalMigrationPreview> PreviewAsync(DateTime from, DateTime to, CancellationToken ct);
    Task<FinanceHistoricalMigrationResult> PostAsync(DateTime from, DateTime to, Guid actorUserId, CancellationToken ct);
}
