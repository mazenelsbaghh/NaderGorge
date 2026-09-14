using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class PlatformFinanceExportTests
{
    [Fact]
    public async Task Xlsx_and_pdf_exports_are_non_empty_for_the_same_period()
    {
        var (db, _, _) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var service = new PlatformFinanceExportService(db);
            var xlsx = await service.ExportLedgerAsync("xlsx", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), Guid.NewGuid(), CancellationToken.None);
            var pdf = await service.ExportLedgerAsync("pdf", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), Guid.NewGuid(), CancellationToken.None);
            Assert.NotEmpty(xlsx.Content);
            Assert.NotEmpty(pdf.Content);
            Assert.EndsWith(".xlsx", xlsx.FileName);
            Assert.EndsWith(".pdf", pdf.FileName);
        }
    }
}
