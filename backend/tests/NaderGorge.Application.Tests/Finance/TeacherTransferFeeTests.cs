using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Application.Features.Admin.PlatformFinance.Teachers;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Application.Tests.Finance;

public sealed class TeacherTransferFeeTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private TeacherSettlementAuthorityService settlements = null!;
    private FinancialPostingService posting = null!;
    private readonly TeacherProfile teacher = new() { User = new User {
        FullName = "Transfer teacher", PhoneNumber = "01011112222", PasswordHash = "test" } };

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await PlatformFinanceSeeder.SeedAsync(db);
        db.Add(teacher); await db.SaveChangesAsync();
        posting = new(db); settlements = new(db, posting);
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    [Theory]
    [InlineData("فودافون كاش", "80", "20", "0.30")]
    [InlineData("Vodafone Cash", "80", "20", "0.30")]
    [InlineData("bank", "80", "20", "0")]
    [InlineData("فودافون كاش", "100", "0", "0")]
    [InlineData("VF-Cash", "1", "1", "0.02")]
    public async Task Payment_credits_platform_fee_and_records_actual_teacher_cash_without_changing_sales(
        string method, string teacherShareText, string platformShareText, string expectedFeeText)
    {
        var teacherShare = Money(teacherShareText); var platformShare = Money(platformShareText); var fee = Money(expectedFeeText);
        var settlement = await ApprovedAsync(teacherShare, platformShare);
        var quote = (await settlements.PaymentPreviewAsync(settlement.Id, method, default))!;
        Assert.Equal(fee, quote.TransferFee);
        Assert.Equal(teacherShare - fee, quote.NetTransferAmount);
        Assert.Equal(teacherShare, settlement.NetPayableAmount);
        Assert.Empty(await TeacherTransferFee.PaidLines(db).ToListAsync());

        var paid = await settlements.PayAsync(teacher.UserId, settlement.Id,
            new(method, "TRANSFER-1", null, quote.NetTransferAmount), default);
        Assert.Equal(TeacherFinanceCommandStatus.Success, paid.Status);
        db.ChangeTracker.Clear();
        var payment = await db.TeacherSettlementPayments.SingleAsync();
        Assert.Equal(teacherShare - fee, payment.Amount);
        Assert.Equal(payment.Amount, (await db.FinancialInvoices.SingleAsync()).Amount);
        Assert.Equal(fee, -(await TeacherTransferFee.PaidLines(db).Select(x => x.Amount).ToListAsync()).Sum());
        var account = (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!;
        Assert.Equal(0m, account.Available); Assert.Equal(0m, account.Reserved);
        Assert.Equal(payment.Amount, account.Paid); Assert.Equal(fee, account.TransferFees);
        Assert.Equal(0m, account.BalanceDifference); Assert.Equal(0m, account.SourceDifference);
        var journalLines = await db.JournalLines.Include(x => x.FinancialAccount).ToListAsync();
        Assert.Equal(platformShare + fee, journalLines.Where(x => x.FinancialAccount.Code == "4000").Sum(x => x.Credit - x.Debit));
        Assert.Equal(0m, journalLines.Where(x => x.FinancialAccount.Code == "2000").Sum(x => x.Credit - x.Debit));
        Assert.Equal(teacherShare + platformShare - payment.Amount,
            journalLines.Where(x => x.FinancialAccount.Code == "1000").Sum(x => x.Debit - x.Credit));
        foreach (var journal in await db.JournalEntries.Include(x => x.Lines).ToListAsync())
            Assert.Equal(journal.Lines.Sum(x => x.Debit), journal.Lines.Sum(x => x.Credit));
        var report = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id,
            new(null, DateOnly.FromDateTime(CairoTime.ToLocal(DateTime.UtcNow))), default))!;
        Assert.Equal(0m, report.Summary.Closing); Assert.Equal(fee, report.Summary.TransferFees);
        Assert.Equal(payment.Amount, report.Summary.Paid); Assert.Equal(platformShare + fee, report.Summary.Platform);
        Assert.Equal(platformShare, Assert.Single(report.Purchases).Platform);
        if (method == "فودافون كاش" && teacherShare == 80m && platformShare == 20m)
            await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(), "massar-teacher-transfer-fee-preview.pdf"),
                TeacherDetailedReportPdf.Generate(report));
        var platformReport = (await new GetTeacherFinancialSummaryQuery(db).GetAsync(teacher.Id, null, null, default))!;
        Assert.Equal(teacherShare + platformShare, platformReport.GrossSales);
        Assert.Equal(platformShare + fee, platformReport.PlatformShare);
        Assert.Equal(payment.Amount, platformReport.Paid); Assert.Equal(0m, platformReport.Outstanding);
        var statement = (await new TeacherStatementService(db).GetAsync(teacher.Id, null, null, 1, 50, default))!;
        Assert.Equal(fee, statement.Totals.TransferFees);
        Assert.Equal(platformShare + fee, statement.Totals.PlatformEarned);
        Assert.Equal(payment.Amount, statement.Totals.TeacherPayments);
        if (fee > 0m) Assert.Contains(TeacherTransferFee.Description, statement.Items.Single(x => x.Kind == "SettlementPayment").Detail);
        Assert.Equal(TeacherFinanceCommandStatus.Conflict, (await settlements.PayAsync(teacher.UserId, settlement.Id,
            new(method, "TRANSFER-1", null, payment.Amount), default)).Status);
        Assert.Single(await db.TeacherSettlementPayments.ToListAsync());
        Assert.Equal(fee == 0m ? 0 : 1, await TeacherTransferFee.PaidLines(db).CountAsync());
    }

    [Fact]
    public async Task Unadjusted_or_missing_cash_amount_cannot_silently_record_an_overpayment()
    {
        var settlement = await ApprovedAsync(80m, 20m);
        foreach (var amount in new decimal?[] { 80m, null })
            Assert.Equal(TeacherFinanceCommandStatus.Invalid, (await settlements.PayAsync(teacher.UserId, settlement.Id,
                new("فودافون كاش", "WRONG-CASH", null, amount), default)).Status);
        Assert.Equal(TeacherSettlementStatus.Approved, (await db.TeacherSettlements.SingleAsync()).Status);
        Assert.Equal(80m, (await db.TeacherAccounts.SingleAsync()).CurrentBalance);
        Assert.Equal(80m, (await db.TeacherAccounts.SingleAsync()).ReservedBalance);
        Assert.Empty(await db.TeacherSettlementPayments.ToListAsync());
        Assert.Empty(await TeacherTransferFee.PaidLines(db).ToListAsync());
        Assert.Single(await db.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task Refund_reduces_fee_basis_and_only_selected_allocations_are_charged()
    {
        var original = await IncomeAsync(80m, 20m, "original");
        original.ReversedAmount = 20m;
        var other = await IncomeAsync(400m, 100m, "other");
        var account = await db.TeacherAccounts.SingleAsync();
        account.CurrentBalance -= 20m; account.TotalEarnings -= 20m;
        await db.SaveChangesAsync();
        var settlement = await ApproveAsync([original.Id]);
        var quote = (await settlements.PaymentPreviewAsync(settlement.Id, "فودافون كاش", default))!;
        Assert.Equal(60m, quote.TeacherAmount); Assert.Equal(15m, quote.PlatformShareBasis);
        Assert.Equal(0.23m, quote.TransferFee); Assert.Equal(59.77m, quote.NetTransferAmount);
        Assert.Equal(TeacherFinanceCommandStatus.Success, (await settlements.PayAsync(teacher.UserId, settlement.Id,
            new("فودافون كاش", "PARTIAL", null, quote.NetTransferAmount), default)).Status);
        Assert.Equal(TeacherFinancialPayoutStatus.Unpaid, other.PayoutStatus);
        Assert.Equal(400m, (await db.TeacherAccounts.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task Debt_offset_without_cash_does_not_charge_transfer_fee()
    {
        var allocation = await IncomeAsync(80m, 20m, "offset");
        db.Add(new TeacherPayoutAdjustment { Teacher = teacher, Amount = -80m, Reason = "previous debt" });
        await db.SaveChangesAsync();
        var settlement = await ApproveAsync([allocation.Id]);
        var quote = (await settlements.PaymentPreviewAsync(settlement.Id, "فودافون كاش", default))!;
        Assert.Equal(0m, quote.NetTransferAmount); Assert.Equal(0m, quote.TransferFee);
        Assert.Equal(TeacherFinanceCommandStatus.Success, (await settlements.PayAsync(teacher.UserId, settlement.Id,
            new("فودافون كاش", "OFFSET", null, 0m), default)).Status);
        Assert.Empty(await TeacherTransferFee.PaidLines(db).ToListAsync());
    }

    [Fact]
    public async Task Cancelling_a_settlement_does_not_charge_transfer_fee_or_allow_payment()
    {
        var settlement = await ApprovedAsync(80m, 20m);
        Assert.Equal(TeacherFinanceCommandStatus.Success, (await settlements.CancelAsync(settlement.Id, default)).Status);
        Assert.Null(await settlements.PaymentPreviewAsync(settlement.Id, "فودافون كاش", default));
        Assert.Equal(TeacherFinanceCommandStatus.Conflict, (await settlements.PayAsync(teacher.UserId, settlement.Id,
            new("فودافون كاش", "CANCELLED", null, 79.7m), default)).Status);
        Assert.Empty(await TeacherTransferFee.PaidLines(db).ToListAsync());
        Assert.Empty(await db.TeacherSettlementPayments.ToListAsync());
        Assert.Equal(80m, (await db.TeacherAccounts.SingleAsync()).CurrentBalance);
        Assert.Equal(0m, (await db.TeacherAccounts.SingleAsync()).ReservedBalance);
    }

    [Fact]
    public async Task Fee_exceeding_teacher_cash_is_rejected_without_creating_debt()
    {
        var settlement = await ApprovedAsync(1m, 100m);
        var quote = (await settlements.PaymentPreviewAsync(settlement.Id, "فودافون كاش", default))!;
        Assert.Equal(-0.5m, quote.NetTransferAmount);
        Assert.Equal(TeacherFinanceCommandStatus.Invalid, (await settlements.PayAsync(teacher.UserId, settlement.Id,
            new("فودافون كاش", "TOO-LARGE", null, quote.NetTransferAmount), default)).Status);
        Assert.Empty(await db.TeacherSettlementPayments.ToListAsync());
        Assert.Empty(await TeacherTransferFee.PaidLines(db).ToListAsync());
        Assert.Equal(1m, (await db.TeacherAccounts.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task Debt_named_like_a_transfer_fee_is_not_reported_as_platform_fee_income()
    {
        await IncomeAsync(80m, 20m, "sale");
        db.Add(new TeacherPayoutAdjustment { Teacher = teacher, Amount = -0.3m,
            Reason = TeacherTransferFee.Description, Status = TeacherPayoutAdjustmentStatus.Open });
        await db.SaveChangesAsync();
        var report = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id,
            new(null, DateOnly.FromDateTime(CairoTime.ToLocal(DateTime.UtcNow))), default))!;
        Assert.Equal(0m, report.Summary.TransferFees); Assert.Equal(20m, report.Summary.Platform);
        Assert.Equal(-0.3m, report.Summary.Adjustments); Assert.Equal(79.7m, report.Summary.Closing);
    }

    [Fact]
    public async Task Failed_posting_rolls_back_cash_fee_invoice_and_account_together()
    {
        var settlement = await ApprovedAsync(80m, 20m);
        db.Add(new AccountingPeriod { StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date,
            Status = AccountingPeriodStatus.Closed });
        await db.SaveChangesAsync();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => settlements.PayAsync(teacher.UserId,
            settlement.Id, new("فودافون كاش", "CLOSED-PERIOD", null, 79.7m), default));
        Assert.Equal("FINANCE_PERIOD_CLOSED", failure.Message);
        db.ChangeTracker.Clear();
        Assert.Equal(TeacherSettlementStatus.Approved, (await db.TeacherSettlements.SingleAsync()).Status);
        Assert.Equal(80m, (await db.TeacherAccounts.SingleAsync()).CurrentBalance);
        Assert.Equal(80m, (await db.TeacherAccounts.SingleAsync()).ReservedBalance);
        Assert.Equal(FinancialInvoiceStatus.Approved, (await db.FinancialInvoices.SingleAsync()).Status);
        Assert.Empty(await db.TeacherSettlementPayments.ToListAsync());
        Assert.Empty(await TeacherTransferFee.PaidLines(db).ToListAsync());
        Assert.Single(await db.JournalEntries.ToListAsync());
    }

    private async Task<TeacherSettlement> ApprovedAsync(decimal teacherShare, decimal platformShare) =>
        await ApproveAsync([(await IncomeAsync(teacherShare, platformShare, "sale")).Id]);

    private async Task<TeacherFinancialAllocation> IncomeAsync(decimal teacherShare, decimal platformShare, string key)
    {
        var allocation = new TeacherFinancialAllocation { Teacher = teacher, TeacherShareAmount = teacherShare,
            PlatformShareAmount = platformShare, GrossBasisAmount = teacherShare + platformShare,
            ReviewStatus = TeacherFinancialReviewStatus.Approved, PayoutStatus = TeacherFinancialPayoutStatus.Unpaid,
            TeacherFinancialEvent = new TeacherFinancialEvent { SourceType = TeacherFinancialSourceType.DirectPurchase,
                IdempotencyKey = key, GrossAmount = teacherShare + platformShare, PaidAmount = teacherShare + platformShare,
                OccurredAt = DateTime.UtcNow } };
        var account = await db.TeacherAccounts.SingleOrDefaultAsync();
        if (account is null) { account = new TeacherAccount { Teacher = teacher }; db.Add(account); }
        account.CurrentBalance += teacherShare; account.TotalEarnings += teacherShare;
        db.Add(allocation); await db.SaveChangesAsync();
        List<FinancialPostingLine> lines = [new("1000", teacherShare + platformShare, 0m), new("2000", 0m, teacherShare, TeacherId: teacher.Id)];
        if (platformShare > 0m) lines.Add(new("4000", 0m, platformShare, TeacherId: teacher.Id));
        await posting.PostAsync(new("DirectSale", allocation.TeacherFinancialEventId, "DirectSale", key,
            "Paid sale", DateTime.UtcNow, teacher.UserId, lines));
        return allocation;
    }

    private async Task<TeacherSettlement> ApproveAsync(IReadOnlyList<Guid> allocationIds)
    {
        var created = await settlements.CreateAsync(teacher.UserId,
            new(teacher.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), null, allocationIds), default);
        Assert.Equal(TeacherFinanceCommandStatus.Success, created.Status);
        var settlement = await db.TeacherSettlements.SingleAsync(x => x.Id == created.Id);
        Assert.Equal(TeacherFinanceCommandStatus.Success, (await settlements.TransitionAsync(teacher.UserId, settlement.Id,
            TeacherSettlementStatus.Draft, TeacherSettlementStatus.Reviewed, default)).Status);
        Assert.Equal(TeacherFinanceCommandStatus.Success, (await settlements.TransitionAsync(teacher.UserId, settlement.Id,
            TeacherSettlementStatus.Reviewed, TeacherSettlementStatus.Approved, default)).Status);
        return settlement;
    }

    private static decimal Money(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);
}
