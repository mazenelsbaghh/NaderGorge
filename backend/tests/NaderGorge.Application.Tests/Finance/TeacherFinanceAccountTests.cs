using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Finance.Commands;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Application.Features.Teacher.Finance.Commands;
using NaderGorge.Application.Features.Teacher.Finance.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Application.Tests.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Application.Tests.Finance;

public sealed class TeacherFinanceAccountTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly TeacherProfile teacher = new() { User = new User { FullName = "Account teacher", PhoneNumber = "01098765432", PasswordHash = "test" } };

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Add(teacher); await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    [Fact]
    public async Task Account_and_teacher_screen_use_signed_recognized_sources_and_actual_cash_payments()
    {
        db.Add(new TeacherAccount { Teacher = teacher, TotalEarnings = 180m, CurrentBalance = 80m, ReservedBalance = 30m });
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 120m);
        AddIncome(TeacherFinancialSourceType.PublicExamPurchase, 60m);
        AddIncome(TeacherFinancialSourceType.Refund, -20m, TeacherFinancialReviewStatus.Reversed, TeacherFinancialPayoutStatus.Debt);
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 900m, TeacherFinancialReviewStatus.PendingReview);
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 700m, TeacherFinancialReviewStatus.Rejected);
        db.Add(new TeacherPayoutAdjustment { Teacher = teacher, Amount = -20m, Status = TeacherPayoutAdjustmentStatus.Open });
        db.Add(new TeacherPayout { Teacher = teacher, Amount = 60m, Status = PayoutStatus.Paid });
        db.Add(new TeacherPayout { Teacher = teacher, Amount = 30m, Status = PayoutStatus.Pending });
        db.Add(new TeacherSettlement { TeacherId = teacher.Id, CreatedByUserId = teacher.UserId, Status = TeacherSettlementStatus.Paid,
            Payments = [new TeacherSettlementPayment { Amount = 40m, PaidByUserId = teacher.UserId }] });
        await db.SaveChangesAsync();

        var account = (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!;
        Assert.Equal(160m, account.TotalEarned);
        Assert.Equal(100m, account.Paid);
        Assert.Equal(60m, account.NetBalance);
        Assert.Equal(30m, account.NetPayable);
        Assert.Equal(160m, account.SourceEarnings);
        Assert.Equal(0m, account.SourceDifference);
        Assert.Equal(0m, account.BalanceDifference);
        Assert.Equal(3, account.Sources.Count);
        var self = await new GetTeacherAccountQueryHandler(db).Handle(new(teacher.UserId), default);
        Assert.Equal(account.NetPayable, self.Data!.AvailableBalance);
        Assert.Equal(account.TotalEarned, self.Data.TotalEarnings);
        Assert.Equal(account.Paid, self.Data.Account!.Paid);
        var day = CairoTime.ToLocal(DateTime.UtcNow).Date;
        var calendar = await new GetTeacherFinanceCalendarQueryHandler(db).Handle(new(teacher.UserId, day, day), default);
        Assert.Equal(160m, Assert.Single(calendar.Data!).TeacherShareAmount);
        Assert.Equal(1, calendar.Data![0].PendingReviewCount);
    }

    [Fact]
    public async Task Statement_keeps_pending_and_cash_movements_separate_and_exports_summary()
    {
        db.Add(new TeacherAccount { Teacher = teacher, TotalEarnings = 100m, CurrentBalance = 50m });
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 100m);
        AddIncome(TeacherFinancialSourceType.Refund, -20m, TeacherFinancialReviewStatus.Reversed);
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 50m, TeacherFinancialReviewStatus.PendingReview);
        db.Add(new TeacherPayout { Teacher = teacher, Amount = 30m, Status = PayoutStatus.Paid });
        db.Add(new TeacherPayout { Teacher = teacher, Amount = 25m, Status = PayoutStatus.Pending });
        db.Add(new TeacherPayoutAdjustment { Teacher = teacher, Amount = -10m, Reason = "Refund debt" });
        db.Add(new TeacherSettlement { TeacherId = teacher.Id, CreatedByUserId = teacher.UserId,
            Status = TeacherSettlementStatus.Paid,
            Payments = [new TeacherSettlementPayment { Amount = 20m, PaidByUserId = teacher.UserId }] });
        await db.SaveChangesAsync();

        var service = new TeacherStatementService(db);
        var firstPage = (await service.GetAsync(teacher.Id, null, null, 1, 2, default))!;
        Assert.Equal(8, firstPage.Total);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(80m, firstPage.Totals.Earned);
        Assert.Equal(50m, firstPage.Totals.PendingEarnings);
        Assert.Equal(50m, firstPage.Totals.TeacherPayments);
        Assert.Equal(10m, firstPage.Totals.OpenDebtAdjustments);
        Assert.Equal(40m, firstPage.Account.NetPayable);

        var pdf = (await service.ExportPdfAsync(teacher.Id, null, null, default))!;
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 4));
        Assert.True(pdf.Content.Length > 1000);
    }

    [Fact]
    public async Task Statement_counts_students_vodafone_refunds_and_used_codes_from_their_source_records()
    {
        var student = new User { FullName = "Student A", PhoneNumber = "01011111111", PasswordHash = "test" };
        var wallet = new DigitalWallet { Label = "Teacher wallet", PhoneNumber = "01022222222" };
        var sms = new IncomingSmsLog { Wallet = wallet, Sender = "VodafoneCash", ReceivedAt = DateTime.UtcNow,
            DeduplicationHash = Guid.NewGuid().ToString("N"), TransferReference = "VF-1" };
        var group = new CodeGroup { Name = "Teacher codes", Teacher = teacher, CreatedByUserId = teacher.UserId };
        var code = new AccessCode { CodeGroup = group, CodeHash = Guid.NewGuid().ToString("N"), SerialNumber = 12345 };
        var journal = new JournalEntry { SourceType = "Refund", IdempotencyKey = Guid.NewGuid().ToString("N") };
        db.AddRange(student, wallet, sms, group, code, journal,
            new TeacherAccount { Teacher = teacher, TotalEarnings = 30m, CurrentBalance = 30m },
            new RechargeRequest { User = student, Teacher = teacher, Wallet = wallet, MatchedSmsLog = sms,
                Amount = 100m, Status = RechargeRequestStatus.Approved, ResolvedAt = DateTime.UtcNow },
            new RechargeRequest { User = student, Teacher = teacher, Wallet = wallet,
                Amount = 25m, Status = RechargeRequestStatus.Approved, ResolvedAt = DateTime.UtcNow },
            new AccessCodeActivationLog { AccessCode = code, Student = student, Teacher = teacher,
                Price = 75m, CommissionEarned = 30m, ActivatedAt = DateTime.UtcNow },
            new TeacherFinancialAllocation { Teacher = teacher, StudentNameSnapshot = student.FullName,
                TeacherShareAmount = 30m, TeacherFinancialEvent = new TeacherFinancialEvent {
                    Student = student, SourceType = TeacherFinancialSourceType.DirectPurchase,
                    SourceId = Guid.NewGuid(), PaidAmount = 120m, OccurredAt = DateTime.UtcNow } });
        await db.SaveChangesAsync();
        db.Add(new PlatformRefund { StudentId = student.Id, TeacherId = teacher.Id,
            PlatformAmount = 30m, TeacherAmount = 20m, Status = PlatformRefundStatus.Posted,
            Method = PlatformRefundMethod.Cash, JournalEntryId = journal.Id, Reason = "Refund" });
        await db.SaveChangesAsync();

        var statement = (await new TeacherStatementService(db).GetAsync(teacher.Id, null, null, 1, 25, default))!;
        Assert.Equal(1, statement.Activity.PurchasingStudents);
        Assert.Equal(120m, statement.Activity.PurchaseValue);
        Assert.Equal(1, statement.Activity.RechargeStudents);
        Assert.Equal(125m, statement.Activity.RechargeAmount);
        Assert.Equal(100m, statement.Activity.VodafoneCashAmount);
        Assert.Equal(25m, statement.Activity.OtherRechargeAmount);
        Assert.Equal(2, statement.Items.Count(x => x.Kind == "StudentCollection" && x.Detail.StartsWith("تحويل مقبول")));
        Assert.DoesNotContain(statement.Items, x => x.Detail.Contains("غير مؤكد"));
        Assert.Equal(1, statement.Activity.RefundedStudents);
        Assert.Equal(50m, statement.Activity.RefundAmount);
        Assert.Equal(1, statement.Activity.ActivatedCodes);
        Assert.Equal(75m, statement.Activity.ActivatedCodeValue);
        Assert.Contains(statement.Items, x => x.Kind == "StudentCollection" && x.Detail.Contains("تحويل مقبول · فودافون كاش"));
        Assert.Contains(statement.Items, x => x.Kind == "StudentRefund" && x.StudentRefundAmount == 50m);
        Assert.Contains(statement.Items, x => x.Kind == "CodeActivation" && x.Reference == "12345");
        var pdf = (await new TeacherStatementService(db).ExportPdfAsync(teacher.Id, null, null, default))!;
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 4));
    }

    [Fact]
    public async Task Simple_statement_groups_prices_and_historical_shares_without_counting_students_twice()
    {
        var student = new User { FullName = "طالب تجريبي", PhoneNumber = "01099900111", PasswordHash = "test" };
        db.Add(student);
        foreach (var share in new[] { 80m, 80m, 70m })
            db.Add(new TeacherFinancialAllocation { Teacher = teacher, TeacherShareAmount = share,
                PlatformShareAmount = 100m - share, TeacherFinancialEvent = new TeacherFinancialEvent {
                    Student = student, SourceType = TeacherFinancialSourceType.DirectPurchase,
                    SourceId = Guid.NewGuid(), IdempotencyKey = Guid.NewGuid().ToString("N"),
                    PaidAmount = 100m, OccurredAt = DateTime.UtcNow } });
        db.Add(new TeacherFinancialAllocation { Teacher = teacher, TeacherShareAmount = 999m,
            ReviewStatus = TeacherFinancialReviewStatus.PendingReview,
            TeacherFinancialEvent = new TeacherFinancialEvent { Student = student,
                SourceType = TeacherFinancialSourceType.DirectPurchase,
                IdempotencyKey = Guid.NewGuid().ToString("N"), PaidAmount = 999m } });
        await db.SaveChangesAsync();
        var service = new TeacherStatementService(db);
        var statement = (await service.GetAsync(teacher.Id, null, null, 1, 1, default))!;
        Assert.Equal(1, statement.Activity.PurchasingStudents);
        Assert.Equal(3, statement.Activity.PurchaseOperations);
        Assert.Equal(300m, statement.Activity.PurchaseValue);
        Assert.Equal(70m, statement.Totals.PlatformEarned);
        Assert.Equal(2, statement.Sales.Count);
        var twentyPercent = Assert.Single(statement.Sales, sale => sale.PlatformPercent == 20m);
        Assert.Equal(2, twentyPercent.Operations);
        Assert.Equal(1, twentyPercent.Students);
        Assert.Equal(200m, twentyPercent.Total);
        Assert.Equal(160m, twentyPercent.TeacherShare);
        Assert.Equal(40m, twentyPercent.PlatformShare);
        Assert.Single(statement.Items);
        var pdf = (await service.ExportPdfAsync(teacher.Id, null, null, default))!;
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 4));
        if (Environment.GetEnvironmentVariable("FINANCE_PDF_SAMPLE") is { Length: > 0 } samplePath)
            await File.WriteAllBytesAsync(samplePath, pdf.Content);
    }

    [Fact]
    public async Task Code_statement_keeps_delivery_value_and_only_receipts_up_to_the_report_end()
    {
        var day = DateTime.UtcNow.Date.AddDays(-5);
        var financial = new FinancialAccount { Code = "TEST-CASH", Name = "Cash" };
        var treasury = new TreasuryAccount { Name = "Cash", FinancialAccountId = financial.Id };
        var firstJournal = new JournalEntry { SequenceNumber = 1, IdempotencyKey = "receipt-one" };
        var secondJournal = new JournalEntry { SequenceNumber = 2, IdempotencyKey = "receipt-two" };
        var delivery = new CodeGroupDeliveryConfirmation {
            CodeGroup = new CodeGroup { Teacher = teacher, Name = "دفعة أكواد", TotalCodes = 10, CreatedByUserId = teacher.UserId },
            ConfirmedAt = day, ConfirmedByUserId = teacher.UserId, PlatformAmountDue = 200m, TeacherRetainedAmount = 800m,
            Payments = [
                new CodeGroupDeliveryPayment { Amount = 50m, ReceivedAt = day.AddDays(1), TreasuryAccountId = treasury.Id,
                    ReceivedByUserId = teacher.UserId, JournalEntryId = firstJournal.Id, IdempotencyKey = "code-one" },
                new CodeGroupDeliveryPayment { Amount = 150m, ReceivedAt = day.AddDays(3), TreasuryAccountId = treasury.Id,
                    ReceivedByUserId = teacher.UserId, JournalEntryId = secondJournal.Id, IdempotencyKey = "code-two" }
            ]
        };
        db.AddRange(financial, treasury, firstJournal, secondJournal, delivery);
        await db.SaveChangesAsync();
        var service = new TeacherStatementService(db);
        var historical = (await service.GetAsync(teacher.Id, day, day.AddDays(2), 1, 25, default))!;
        var batch = Assert.Single(historical.CodeBatches);
        Assert.Equal(10, batch.Codes);
        Assert.Equal(1000m, batch.Value);
        Assert.Equal(50m, batch.Collected);
        Assert.Equal(150m, batch.Remaining);
        Assert.Equal(50m, historical.Totals.PlatformCodePayments);
        var current = (await service.GetAsync(teacher.Id, day, null, 1, 25, default))!;
        Assert.Equal(0m, Assert.Single(current.CodeBatches).Remaining);
        Assert.Equal(200m, Assert.Single(current.CodeBatches).Collected);
    }

    [Fact]
    public async Task Reserved_debt_is_deducted_once_and_request_cannot_exceed_the_same_available_amount()
    {
        db.Add(new TeacherAccount { Teacher = teacher, TotalEarnings = 200m, CurrentBalance = 200m, ReservedBalance = 100m });
        var debt = new TeacherPayoutAdjustment { Teacher = teacher, Amount = -150m };
        db.Add(new TeacherSettlement { TeacherId = teacher.Id, CreatedByUserId = teacher.UserId, Status = TeacherSettlementStatus.Draft,
            GrossDueAmount = 100m, DebtDeductionAmount = 100m,
            Lines = [new TeacherSettlementLine { Adjustment = debt, Amount = -100m }] });
        await db.SaveChangesAsync();
        var before = (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!;
        Assert.Equal(50m, before.NetPayable);
        Assert.Equal(50m, before.UnreservedDebt);
        var handler = new RequestPayoutCommandHandler(db, new TestAuditRepository());
        Assert.False((await handler.Handle(new(teacher.UserId, 50.01m), default)).Success);
        Assert.False((await handler.Handle(new(teacher.UserId, 0.001m), default)).Success);
        Assert.Empty(await db.TeacherPayouts.ToListAsync());
        var requested = await handler.Handle(new(teacher.UserId, 50m), default);
        Assert.True(requested.Success, requested.Message);
        Assert.Equal(0m, requested.Data!.AvailableBalance);
        var after = (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!;
        Assert.Equal(0m, after.NetPayable);
        Assert.Equal(150m, after.Reserved);
    }

    [Fact]
    public async Task New_debt_after_reservation_blocks_payment_and_rejection_still_releases_money()
    {
        var account = new TeacherAccount { Teacher = teacher, CurrentBalance = 100m, TotalEarnings = 100m, ReservedBalance = 100m };
        var payout = new TeacherPayout { Teacher = teacher, Amount = 100m, Status = PayoutStatus.Approved };
        db.AddRange(account, payout, new TeacherPayoutAdjustment { Teacher = teacher, Amount = -20m });
        await db.SaveChangesAsync();
        var handler = new ResolvePayoutCommandHandler(db, new TestAuditRepository(), new FinancialPostingService(db));
        Assert.False((await handler.Handle(new(payout.Id, PayoutStatus.Paid, null, teacher.UserId), default)).Success);
        Assert.Equal(PayoutStatus.Approved, payout.Status);
        Assert.Equal(100m, account.CurrentBalance);
        Assert.Empty(await db.JournalEntries.ToListAsync());
        Assert.True((await handler.Handle(new(payout.Id, PayoutStatus.Rejected, "recalculate after refund", teacher.UserId), default)).Success);
        Assert.Equal(80m, (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!.NetPayable);
    }

    [Fact]
    public async Task Historical_balance_without_sources_is_flagged_without_recalculating_at_current_rate()
    {
        teacher.CommissionRate = 95m;
        db.Add(new TeacherAccount { Teacher = teacher, CurrentBalance = 80m, TotalEarnings = 100m });
        await db.SaveChangesAsync();
        var snapshot = (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!;
        Assert.Equal(100m, snapshot.TotalEarned);
        Assert.Equal(100m, snapshot.SourceDifference);
        Assert.Equal(-20m, snapshot.BalanceDifference);
        Assert.Empty(snapshot.Sources);
    }

    [Fact]
    public async Task Retrying_a_failed_reservation_creates_only_one_payout_and_one_reservation()
    {
        db.Add(new TeacherAccount { Teacher = teacher, CurrentBalance = 100m, TotalEarnings = 100m });
        await db.SaveChangesAsync();
        var interceptor = new FailFirstPayoutSave();
        await using var retryDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(interceptor).Options);
        var result = await new RequestPayoutCommandHandler(retryDb, new TestAuditRepository()).Handle(new(teacher.UserId, 75m), default);
        Assert.True(result.Success, result.Message);
        Assert.True(interceptor.Failed);
        Assert.Equal(75m, (await retryDb.TeacherPayouts.SingleAsync()).Amount);
        Assert.Equal(75m, (await retryDb.TeacherAccounts.SingleAsync()).ReservedBalance);
        Assert.Equal(25m, result.Data!.AvailableBalance);
    }

    [Fact]
    public async Task Second_settlement_cannot_skip_debt_partially_reserved_in_the_first()
    {
        db.Add(new TeacherAccount { Teacher = teacher, CurrentBalance = 200m, TotalEarnings = 200m });
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 100m);
        AddIncome(TeacherFinancialSourceType.DirectPurchase, 100m);
        db.Add(new TeacherPayoutAdjustment { Teacher = teacher, Amount = -150m });
        await db.SaveChangesAsync();
        var allocations = await db.TeacherFinancialAllocations.OrderBy(x => x.Id).ToListAsync();
        var service = new TeacherSettlementAuthorityService(db, new FinancialPostingService(db));
        var first = new SettlementCreationInput(teacher.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), null, [allocations[0].Id]);
        var second = first with { AllocationIds = [allocations[1].Id] };
        var created = await service.CreateAsync(teacher.UserId, first, default);
        Assert.Equal(TeacherFinanceCommandStatus.Success, created.Status);
        Assert.Equal(TeacherFinanceCommandStatus.Conflict, (await service.CreateAsync(teacher.UserId, second, default)).Status);
        var settlement = await db.TeacherSettlements.SingleAsync();
        Assert.Equal(0m, settlement.NetPayableAmount);
        await service.TransitionAsync(teacher.UserId, settlement.Id, TeacherSettlementStatus.Draft, TeacherSettlementStatus.Reviewed, default);
        await service.TransitionAsync(teacher.UserId, settlement.Id, TeacherSettlementStatus.Reviewed, TeacherSettlementStatus.Approved, default);
        Assert.Equal(TeacherFinanceCommandStatus.Success, (await service.PayAsync(teacher.UserId, settlement.Id, new("Debt offset", "first", null, 0m), default)).Status);
        var preview = await service.PreviewAsync(second, default);
        Assert.Equal(50m, preview.DebtDeductionAmount);
        Assert.Equal(50m, preview.NetPayableAmount);
        Assert.Equal(50m, (await new TeacherFinanceAccountService(db).GetAsync(teacher.Id, default))!.NetPayable);
        Assert.Equal(TeacherFinanceCommandStatus.Success, (await service.CreateAsync(teacher.UserId, second, default)).Status);
    }

    private void AddIncome(TeacherFinancialSourceType source, decimal share,
        TeacherFinancialReviewStatus review = TeacherFinancialReviewStatus.AutoApproved,
        TeacherFinancialPayoutStatus payout = TeacherFinancialPayoutStatus.Unpaid)
    {
        db.Add(new TeacherFinancialAllocation { Teacher = teacher, TeacherShareAmount = share,
            GrossBasisAmount = share, ReviewStatus = review, PayoutStatus = payout,
            TeacherFinancialEvent = new TeacherFinancialEvent { SourceType = source, IdempotencyKey = Guid.NewGuid().ToString(), OccurredAt = DateTime.UtcNow } });
    }

    private sealed class FailFirstPayoutSave : SaveChangesInterceptor
    {
        public bool Failed { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Failed && eventData.Context!.ChangeTracker.Entries<TeacherPayout>().Any(x => x.State == EntityState.Added))
            {
                Failed = true;
                throw new InvalidOperationException("could not serialize access due to concurrent update");
            }
            return ValueTask.FromResult(result);
        }
    }
}
