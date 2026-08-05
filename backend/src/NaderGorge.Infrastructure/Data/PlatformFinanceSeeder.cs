using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Data;

public static class PlatformFinanceSeeder
{
    private static readonly (string Code, string Name, FinancialAccountType Type, FinancialNormalSide Side, FinancialAccountRole Role)[] Accounts =
    [
        ("1000", "الخزينة والمحافظ", FinancialAccountType.Asset, FinancialNormalSide.Debit, FinancialAccountRole.Treasury),
        ("1100", "رصيد الطالب العام", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.GeneralStudentLiability),
        ("1110", "رصيد الطالب المقيد بمدرس", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.TeacherStudentLiability),
        ("2000", "مستحقات المدرسين", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.TeacherPayable),
        ("2100", "مستحقات الموردين", FinancialAccountType.Liability, FinancialNormalSide.Credit, FinancialAccountRole.SupplierPayable),
        ("4000", "إيرادات المنصة", FinancialAccountType.Revenue, FinancialNormalSide.Credit, FinancialAccountRole.PlatformRevenue),
        ("4100", "استردادات ومردودات", FinancialAccountType.ContraRevenue, FinancialNormalSide.Debit, FinancialAccountRole.Refunds),
        ("5000", "مصروفات تشغيل المنصة", FinancialAccountType.Expense, FinancialNormalSide.Debit, FinancialAccountRole.OperatingExpense),
        ("5100", "مصروفات الرواتب", FinancialAccountType.Expense, FinancialNormalSide.Debit, FinancialAccountRole.PayrollExpense),
        ("9990", "حساب التسويات الافتتاحية", FinancialAccountType.Equity, FinancialNormalSide.Credit, FinancialAccountRole.OpeningSuspense)
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.FinancialAccounts.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var changed = false;
        foreach (var item in Accounts)
        {
            if (existing.ContainsKey(item.Code)) continue;
            var account = new FinancialAccount
            {
                Code = item.Code,
                Name = item.Name,
                Type = item.Type,
                NormalSide = item.Side,
                Role = item.Role
            };
            db.FinancialAccounts.Add(account);
            existing[item.Code] = account;
            changed = true;
        }

        if (changed) await db.SaveChangesAsync(cancellationToken);

        var treasuryAccount = existing["1000"];
        if (!await db.TreasuryAccounts.AnyAsync(x => x.FinancialAccountId == treasuryAccount.Id, cancellationToken))
        {
            db.TreasuryAccounts.Add(new TreasuryAccount
            {
                Name = "الخزينة الرئيسية",
                Type = TreasuryAccountType.Cashbox,
                FinancialAccountId = treasuryAccount.Id,
                IsActive = true
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        var wallets = await db.DigitalWallets.Where(x => x.IsActive).ToListAsync(cancellationToken);
        foreach (var wallet in wallets)
        {
            var code = $"W{wallet.Id:N}";
            var walletAccount = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
            if (walletAccount is null)
            {
                walletAccount = new FinancialAccount
                {
                    Code = code,
                    Name = $"محفظة {wallet.Label}",
                    Type = FinancialAccountType.Asset,
                    NormalSide = FinancialNormalSide.Debit,
                    Role = FinancialAccountRole.Treasury
                };
                db.FinancialAccounts.Add(walletAccount);
                await db.SaveChangesAsync(cancellationToken);
            }

            if (!await db.TreasuryAccounts.AnyAsync(x => x.DigitalWalletId == wallet.Id, cancellationToken))
            {
                db.TreasuryAccounts.Add(new TreasuryAccount
                {
                    Name = wallet.Label,
                    Type = TreasuryAccountType.DigitalWallet,
                    FinancialAccountId = walletAccount.Id,
                    DigitalWalletId = wallet.Id,
                    MaskedIdentifier = wallet.PhoneNumber.Length > 4 ? $"***{wallet.PhoneNumber[^4..]}" : wallet.PhoneNumber
                });
            }
        }

        if (!await db.ExpenseCategories.AnyAsync(x => x.IsActive, cancellationToken))
        {
            db.ExpenseCategories.Add(new ExpenseCategory { Name = "مصروفات عامة", AccountCode = "5000" });
        }
        await db.SaveChangesAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;
        if (!await db.AccountingPeriods.AnyAsync(x => x.StartDate <= today && x.EndDate >= today, cancellationToken))
        {
            db.AccountingPeriods.Add(new AccountingPeriod
            {
                StartDate = new DateTime(today.Year, 1, 1),
                EndDate = new DateTime(today.Year, 12, 31),
                Status = AccountingPeriodStatus.Open
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
