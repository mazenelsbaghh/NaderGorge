namespace NaderGorge.Application.Interfaces.Finance;

public sealed record FinanceExportResult(byte[] Content, string ContentType, string FileName);

public interface IPlatformFinanceExportService
{
    Task<FinanceExportResult> ExportLedgerAsync(string format, DateTime from, DateTime to, Guid actorUserId, CancellationToken ct);
}
