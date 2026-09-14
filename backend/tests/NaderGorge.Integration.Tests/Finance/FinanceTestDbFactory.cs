using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.Finance;

internal static class FinanceTestDbFactory
{
    public static AppDbContext Create() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"finance-{Guid.NewGuid():N}").Options);

    public static async Task<(AppDbContext Db, Guid CashTreasuryId, Guid StudentId)> CreateLedgerAsync()
    {
        var db = Create();
        var cash = Account("1000", FinancialAccountType.Asset, FinancialAccountRole.Treasury, FinancialNormalSide.Debit);
        var general = Account("1100", FinancialAccountType.Liability, FinancialAccountRole.GeneralStudentLiability, FinancialNormalSide.Credit);
        var teacherLiability = Account("1110", FinancialAccountType.Liability, FinancialAccountRole.TeacherStudentLiability, FinancialNormalSide.Credit);
        var teacher = Account("2000", FinancialAccountType.Liability, FinancialAccountRole.TeacherPayable, FinancialNormalSide.Credit);
        var revenue = Account("4000", FinancialAccountType.Revenue, FinancialAccountRole.PlatformRevenue, FinancialNormalSide.Credit);
        var refunds = Account("4100", FinancialAccountType.ContraRevenue, FinancialAccountRole.Refunds, FinancialNormalSide.Debit);
        var expense = Account("5000", FinancialAccountType.Expense, FinancialAccountRole.OperatingExpense, FinancialNormalSide.Debit);
        var payroll = Account("5100", FinancialAccountType.Expense, FinancialAccountRole.PayrollExpense, FinancialNormalSide.Debit);
        db.FinancialAccounts.AddRange(cash, general, teacherLiability, teacher, revenue, refunds, expense, payroll);
        var treasury = new TreasuryAccount { Name = "Test cashbox", Type = TreasuryAccountType.Cashbox, FinancialAccountId = cash.Id };
        var student = new User { FullName = "Finance student", PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "test" };
        db.TreasuryAccounts.Add(treasury);
        db.Users.Add(student);
        await db.SaveChangesAsync();
        return (db, treasury.Id, student.Id);
    }

    private static FinancialAccount Account(string code, FinancialAccountType type, FinancialAccountRole role, FinancialNormalSide side) => new() { Code = code, Name = code, Type = type, Role = role, NormalSide = side };
}
