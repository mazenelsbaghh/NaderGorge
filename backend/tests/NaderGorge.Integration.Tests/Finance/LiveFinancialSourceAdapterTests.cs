using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Infrastructure.Services.Finance;
using NaderGorge.Infrastructure.Services.Finance.Adapters;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class LiveFinancialSourceAdapterTests
{
    [Fact]
    public async Task Recharge_sale_teacher_and_payroll_adapters_create_balanced_entries()
    {
        var (db, _, studentId) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var posting = new FinancialPostingService(db);
            var teacherId = Guid.NewGuid();
            await new RechargeFinancialAdapter(posting).PostAsync(new("RechargeRequest", Guid.NewGuid(), studentId, teacherId, 10m, 0m, 0m, DateTime.UtcNow, null, "adapter-recharge"), CancellationToken.None);
            await new SalesFinancialAdapter(posting).PostAsync(new("Purchase", Guid.NewGuid(), studentId, teacherId, 10m, 7m, 3m, DateTime.UtcNow, null, "adapter-sale"), CancellationToken.None);
            await new TeacherFinancialAdapter(posting).PostAsync(new("TeacherSettlement", Guid.NewGuid(), studentId, teacherId, 3m, 0m, 0m, DateTime.UtcNow, null, "adapter-teacher"), CancellationToken.None);
            await new PayrollFinancialAdapter(posting).PostAsync(new("Payroll", Guid.NewGuid(), studentId, null, 2m, 0m, 0m, DateTime.UtcNow, null, "adapter-payroll"), CancellationToken.None);
            Assert.Equal(4, db.JournalEntries.Count());
            Assert.All(db.JournalEntries, entry => Assert.Equal(entry.Lines.Sum(line => line.Debit), entry.Lines.Sum(line => line.Credit)));
        }
    }
}
