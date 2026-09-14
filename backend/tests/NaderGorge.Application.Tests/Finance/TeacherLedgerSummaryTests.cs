using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.PlatformFinance.Teachers;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.Finance;
using NaderGorge.Infrastructure.Services.Finance.Adapters;

namespace NaderGorge.Application.Tests.Finance;

public sealed class TeacherLedgerSummaryTests
{
    [Fact]
    public async Task Report_separates_sales_refunds_and_payments_and_includes_opening_balance()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var teacher = new TeacherProfile { User = new User { FullName = "Teacher", PhoneNumber = "01012345678", PasswordHash = "test" } };
        db.Add(teacher);
        var roles = new[] { ("1000", FinancialAccountRole.Treasury), ("1100", FinancialAccountRole.GeneralStudentLiability),
            ("2000", FinancialAccountRole.TeacherPayable), ("4000", FinancialAccountRole.PlatformRevenue), ("4100", FinancialAccountRole.Refunds) };
        db.AddRange(roles.Select(x => new FinancialAccount { Code = x.Item1, Name = x.Item1, Role = x.Item2 }));
        await db.SaveChangesAsync();
        var posting = new FinancialPostingService(db);
        var now = DateTime.UtcNow;
        await Post("Opening", now.AddDays(-10), [new("1000", 10m, 0m), new("2000", 0m, 10m, TeacherId: teacher.Id)]);
        await Post("Purchase", now, [new("1100", 100m, 0m), new("2000", 0m, 60m, TeacherId: teacher.Id), new("4000", 0m, 40m)]);
        await Post("TeacherPayout", now, [new("2000", 20m, 0m, TeacherId: teacher.Id), new("1000", 0m, 20m, TeacherId: teacher.Id)]);
        await Post("PlatformRefund", now, [new("4100", 5m, 0m), new("2000", 10m, 0m, TeacherId: teacher.Id), new("1000", 0m, 15m)]);
        var report = await new GetTeacherFinancialSummaryQuery(db).GetAsync(teacher.Id, now.AddDays(-1), now.AddDays(1), CancellationToken.None);
        Assert.NotNull(report);
        Assert.Equal(100m, report.GrossSales);
        Assert.Equal(50m, report.TeacherShare);
        Assert.Equal(35m, report.PlatformShare);
        Assert.Equal(15m, report.Refunds);
        Assert.Equal(20m, report.Paid);
        Assert.Equal(40m, report.Outstanding);
        Assert.Equal(report, Assert.Single(await new GetTeacherFinancialSummaryQuery(db).GetAllAsync(now.AddDays(-1), now.AddDays(1), CancellationToken.None)));

        // An agreed subsidy must produce a balanced loss entry, not drop the platform debit.
        var subsidized = await new SalesFinancialAdapter(posting).PostAsync(new("Purchase", Guid.NewGuid(), teacher.UserId,
            teacher.Id, 50m, -10m, 60m, now, null, "subsidized-sale"), CancellationToken.None);
        Assert.Equal(subsidized.Lines.Sum(x => x.Debit), subsidized.Lines.Sum(x => x.Credit));
        var revenueAccount = await db.FinancialAccounts.SingleAsync(x => x.Code == "4000");
        var loss = subsidized.Lines.Single(x => x.FinancialAccountId == revenueAccount.Id);
        Assert.Equal(10m, loss.Debit);
        Assert.Equal(teacher.Id, loss.TeacherId);

        Task<JournalEntry> Post(string source, DateTime date, IReadOnlyList<FinancialPostingLine> lines) =>
            posting.PostAsync(new(source, Guid.NewGuid(), source, Guid.NewGuid().ToString(), source, date, null, lines));
    }
}
