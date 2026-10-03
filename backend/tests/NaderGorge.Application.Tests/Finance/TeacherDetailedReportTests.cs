using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Application.Tests.Finance;

public sealed class TeacherDetailedReportTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    private readonly TeacherProfile teacher = new() { User = new() { FullName = "مدرس الاختبار", PhoneNumber = "01011111222", PasswordHash = "test" } };
    private readonly User student = new() { FullName = "طالب الاختبار", PhoneNumber = "01033333444", PasswordHash = "test" };
    private Package course = null!;
    private Lesson lesson = null!;
    private int purchaseCount;
    private static readonly DateOnly Cutoff = new(2026, 9, 30);
    private static DateTime At(int day) => CairoTime.GetDayRangeUtc(new DateTime(2026, 9, day)).StartUtc.AddHours(12);

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        course = new() { Name = "كورس عربي", Teacher = teacher, Subject = new() { Name = "العربي" } };
        lesson = new() { Title = "الحصة الأولى", ContentSection = new() { Title = "الشهر الأول", Term = new() { Title = "الترم الأول", Package = course } } };
        db.AddRange(lesson, student, new TeacherAccount { Teacher = teacher, CurrentBalance = 9999, TotalEarnings = 9999 });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    [Fact]
    public async Task Selected_period_includes_last_Cairo_day_and_uses_opening_without_current_balance_or_free_commission()
    {
        var start = CairoTime.GetDayRangeUtc(new DateTime(2026, 9, 1)).StartUtc;
        var end = CairoTime.GetDayRangeUtc(Cutoff.ToDateTime(TimeOnly.MinValue)).EndUtc;
        Purchase(start.AddTicks(-1), 100, 90, 10);
        Purchase(start, 100, 90, 10);
        Purchase(At(2), 0, 0, 0);
        Purchase(end.AddTicks(-1), 100, 90, 10);
        Purchase(end, 1000, 750, 250);
        db.Add(new TeacherPayout { Teacher = teacher, Amount = 20, Status = PayoutStatus.Paid, PaidAt = start.AddMinutes(-1) });
        db.Add(new TeacherPayout { Teacher = teacher, Amount = 50, Status = PayoutStatus.Paid, PaidAt = end.AddTicks(-1) });
        await db.SaveChangesAsync();

        var report = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(new(2026, 9, 1), Cutoff), default))!;
        Assert.Equal(3, report.Purchases.Count);
        Assert.Equal(1, report.Purchases.Count(x => x.Paid == 0));
        Assert.Single(report.Purchases.Where(x => x.Paid > 0).Select(x => x.StudentId).Distinct());
        Assert.Equal(new TeacherReportSummary(70, 180, 20, 0, 50, 0, 200), report.Summary);
        Assert.Equal(9999, await db.TeacherAccounts.Select(x => x.CurrentBalance).SingleAsync());
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Posted_refund_and_legacy_cancellation_audit_count_once_and_unrelated_teacher_is_excluded()
    {
        var sale = Purchase(At(10), 200, 150, 50);
        var grant = db.ChangeTracker.Entries<StudentAccessGrant>().Single().Entity;
        grant.CancelledAt = At(20); grant.CancellationReason = "طلب الطالب"; grant.IsActive = false;
        var original = sale.Allocations.Single(); original.ReviewStatus = TeacherFinancialReviewStatus.Reversed;
        db.Add(new TeacherFinancialAllocation { Teacher = teacher, TeacherShareAmount = -150, PlatformShareAmount = -50,
            ReviewStatus = TeacherFinancialReviewStatus.Reversed, TeacherFinancialEvent = new() { SourceType = TeacherFinancialSourceType.Refund,
                Student = student, TargetId = lesson.Id, TargetType = SalesTargetType.Lesson, OccurredAt = At(20), IdempotencyKey = Guid.NewGuid().ToString() } });
        var journal = new JournalEntry { OccurredAt = At(20), SourceType = "Refund", IdempotencyKey = Guid.NewGuid().ToString() };
        db.Add(journal);
        db.Add(new PlatformRefund { TeacherId = teacher.Id, StudentId = student.Id, OriginalSourceId = sale.SourceId,
            OriginalSourceType = "PurchaseOperation", AccessGrantId = grant.Id, TeacherAmount = 30, PlatformAmount = 10,
            Status = PlatformRefundStatus.Posted, Method = PlatformRefundMethod.StudentBalance, JournalEntryId = journal.Id });
        db.Add(new AuditLog { Action = "CANCEL_PACKAGE_GRANT", EntityType = "StudentAccessGrant", EntityId = grant.Id,
            CreatedAt = At(20), PerformedByUserId = teacher.UserId, NewValues = $"{{\"refundedAmount\":40,\"purchaseOperationId\":\"{sale.SourceId}\"}}" });
        var other = new TeacherProfile { User = new() { FullName = "مدرس آخر", PhoneNumber = "01055555666", PasswordHash = "test" } };
        db.Add(new TeacherFinancialAllocation { Teacher = other, TeacherShareAmount = 700,
            TeacherFinancialEvent = new() { SourceType = TeacherFinancialSourceType.DirectPurchase, Student = student,
                PaidAmount = 800, OccurredAt = At(10), IdempotencyKey = Guid.NewGuid().ToString() } });
        await db.SaveChangesAsync();

        var report = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(null, Cutoff), default))!;
        Assert.Equal(0, report.Summary.Earned);
        Assert.Equal(0, report.Summary.Platform);
        Assert.Equal(40, Assert.Single(report.Refunds).Amount);
        Assert.False(Assert.Single(report.Purchases).Counted);
        Assert.Single(report.Cancellations);
        Assert.Single(report.Purchases);
        var cancellationPeriod = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(new(2026, 9, 20), Cutoff), default))!;
        Assert.Empty(cancellationPeriod.Purchases);
        Assert.Single(cancellationPeriod.Cancellations);
        Assert.NotEmpty(TeacherDetailedReportPdf.Generate(cancellationPeriod));

        db.PlatformRefunds.Local.Single().Status = PlatformRefundStatus.Reversed;
        journal.Status = JournalEntryStatus.Reversed;
        db.Add(new JournalEntry { SequenceNumber = 1, OccurredAt = At(30).AddDays(2), SourceType = "Refund", PostingKind = "Reversal",
            ReversalOfId = journal.Id, IdempotencyKey = Guid.NewGuid().ToString() });
        await db.SaveChangesAsync();
        var historical = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(null, Cutoff), default))!;
        Assert.Equal(40, Assert.Single(historical.Refunds).Amount);
        var later = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(null, new(2026, 10, 2)), default))!;
        Assert.Equal(0, later.Refunds.Sum(x => x.Amount));
        Assert.Equal(2, later.Refunds.Count);
    }

    [Fact]
    public async Task Scoped_balance_is_reconstructed_at_end_without_later_usage_or_revocation_and_pdf_handles_long_details()
    {
        var issuance = new GiftIssuance { TargetType = GiftTargetType.TeacherBalance, Teacher = teacher, IssuedByUserId = teacher.UserId, Reason = "شحن مدفوع" };
        var recipient = new GiftRecipient { Student = student, GiftIssuance = issuance, OutcomeCode = "DIGITAL_RECHARGE", RevokedAt = At(30).AddDays(2) };
        var credit = new PromotionalBalanceAllocation { Student = student, Teacher = teacher, GiftRecipient = recipient,
            OriginalAmount = 100, AvailableAmount = 50, ConsumedAmount = 50, CreatedAt = At(1) };
        db.Add(credit);
        db.Add(new PromotionalBalanceUsage { Allocation = credit, GiftRecipient = recipient, Amount = 30, CreatedAt = At(5), PurchaseOperationId = Guid.NewGuid() });
        db.Add(new PromotionalBalanceUsage { Allocation = credit, GiftRecipient = recipient, Amount = 20, CreatedAt = At(30).AddDays(2), PurchaseOperationId = Guid.NewGuid() });
        Purchase(At(7), 100, 90, 10);
        await db.SaveChangesAsync();

        var service = new TeacherDetailedReportService(db);
        var report = (await service.ReadAsync(teacher.Id, new(null, Cutoff), default))!;
        Assert.Equal(70, Assert.Single(report.Funding).Remaining);
        Assert.Equal(30, report.Funding[0].Used);
        Assert.True(report.Funding[0].Paid);
        var extended = report with { Purchases = Enumerable.Range(0, 55).Select(index => report.Purchases[0] with {
            OperationId = Guid.NewGuid(), Content = string.Join(" ", Enumerable.Repeat("عنوان عربي طويل لتفاصيل الحصة", 12)) }).ToArray(),
            Summary = new(0, 4950, 550, 0, 0, 0, 4950) };
        var bytes = TeacherDetailedReportPdf.Generate(extended);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 10000);
        await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(), "massar-teacher-report-preview.pdf"), bytes);
    }

    [Fact]
    public async Task Teacher_retained_codes_and_backdated_fee_debt_reduce_payable_without_changing_sales_or_teacher_account()
    {
        var sale = Purchase(At(4), 100, 85, 15);
        sale.Allocations.Single().RetainedByTeacher = true;
        db.Add(new TeacherPayoutAdjustment { Teacher = teacher, RelatedFinancialEvent = sale, Amount = -15,
            Status = TeacherPayoutAdjustmentStatus.Open, CreatedAt = At(30).AddDays(2) });
        await db.SaveChangesAsync();
        var report = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(null, Cutoff), default))!;
        Assert.Equal(new TeacherReportSummary(0, 85, 15, 85, 0, -15, -15), report.Summary);
        Assert.Equal(85, Assert.Single(report.Purchases).Teacher);
        Assert.True(report.Purchases[0].Retained);
        Assert.Equal(9999, await db.TeacherAccounts.Select(x => x.CurrentBalance).SingleAsync());
    }

    [Fact]
    public async Task Refund_after_payout_reduces_payable_once_and_only_when_refund_occurs()
    {
        var sale = Purchase(At(4), 100, 90, 10);
        var payout = new TeacherPayout { Teacher = teacher, Amount = 90, Status = PayoutStatus.Paid, PaidAt = At(5) };
        db.Add(payout);
        var grant = db.ChangeTracker.Entries<StudentAccessGrant>().Single().Entity;
        grant.CancelledAt = At(20); grant.IsActive = false;
        sale.Allocations.Single().ReviewStatus = TeacherFinancialReviewStatus.Reversed;
        sale.Allocations.Single().PayoutStatus = TeacherFinancialPayoutStatus.Debt;
        db.Add(new TeacherFinancialAllocation { Teacher = teacher, TeacherShareAmount = -90, PlatformShareAmount = -10,
            ReviewStatus = TeacherFinancialReviewStatus.Reversed, PayoutStatus = TeacherFinancialPayoutStatus.Debt,
            TeacherFinancialEvent = new() { SourceType = TeacherFinancialSourceType.Cancellation, SourceId = grant.Id,
                Student = student, TargetType = SalesTargetType.Lesson, TargetId = lesson.Id,
                OccurredAt = At(20), IdempotencyKey = Guid.NewGuid().ToString() } });
        db.Add(new TeacherPayoutAdjustment { Teacher = teacher, RelatedFinancialEvent = sale, RelatedPayout = payout,
            Amount = -90, CreatedAt = At(20), Status = TeacherPayoutAdjustmentStatus.Open });
        await db.SaveChangesAsync();
        var service = new TeacherDetailedReportService(db);
        var beforeRefund = (await service.ReadAsync(teacher.Id, new(null, new(2026, 9, 10)), default))!;
        Assert.Equal(0, beforeRefund.Summary.Closing);
        Assert.Equal(0, beforeRefund.Summary.Adjustments);
        var afterRefund = (await service.ReadAsync(teacher.Id, new(null, Cutoff), default))!;
        Assert.Equal(new TeacherReportSummary(0, 0, 0, 0, 90, 0, -90), afterRefund.Summary);
        Assert.Single(afterRefund.Cancellations);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, null)]
    public async Task Legacy_balance_code_requires_one_matching_redemption_to_classify_paid(int matches, bool? expectedPaid)
    {
        var issuance = new GiftIssuance { Teacher = teacher, TargetType = GiftTargetType.TeacherBalance,
            IssuedByUserId = teacher.UserId, Reason = "Teacher scoped balance code: أكواد السنتر" };
        db.Add(new PromotionalBalanceAllocation { Teacher = teacher, Student = student, OriginalAmount = 100,
            AvailableAmount = 100, CreatedAt = At(1), GiftRecipient = new() { Student = student, GiftIssuance = issuance, OutcomeCode = "GRANTED" } });
        for (var index = 0; index < matches; index++)
            db.Add(new AccessCode { IsConsumed = true, ConsumedByUserId = student.Id, ConsumedAt = At(1),
                CodeHash = Guid.NewGuid().ToString(), CodeGroup = new() { Teacher = teacher, CreatedByUserId = teacher.UserId,
                    CodeType = CodeType.Balance, BalanceAmount = 100, Name = "أكواد السنتر" } });
        await db.SaveChangesAsync();
        var report = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(null, Cutoff), default))!;
        Assert.Equal(expectedPaid, Assert.Single(report.Funding).Paid);
        Assert.Equal(100, report.Funding[0].Remaining);
        Assert.Equal(0, report.Summary.Closing);
    }

    [Fact]
    public async Task Current_agreement_cards_ignore_inactive_and_expired_terms_but_preserve_history_and_recorded_allocations()
    {
        var end = CairoTime.GetDayRangeUtc(Cutoff.ToDateTime(TimeOnly.MinValue)).EndUtc;
        TeacherFinancialAgreement Agreement(TeacherAgreementScopeType scope, decimal fee, DateTime from) => new()
        {
            Teacher = teacher, ScopeType = scope, AllocationMode = TeacherAgreementAllocationMode.PlatformFixedPerUnit,
            AllocationValue = fee, EffectiveFrom = from, CreatedByUserId = teacher.UserId
        };
        var lessonRule = Agreement(TeacherAgreementScopeType.Lesson, 10, At(1));
        var monthRule = Agreement(TeacherAgreementScopeType.ContentSection, 40, At(1));
        var inactiveLesson = Agreement(TeacherAgreementScopeType.Lesson, 15, At(24));
        var inactiveMonth = Agreement(TeacherAgreementScopeType.ContentSection, 60, At(24));
        inactiveLesson.IsActive = false;
        inactiveMonth.IsActive = false;
        var expired = Agreement(TeacherAgreementScopeType.Lesson, 20, At(25));
        expired.EffectiveTo = At(30);
        var future = Agreement(TeacherAgreementScopeType.Lesson, 30, end);
        db.AddRange(lessonRule, monthRule, inactiveLesson, inactiveMonth, expired, future);
        Purchase(At(26), 100, 90, 10);
        await db.SaveChangesAsync();

        var report = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(null, Cutoff), default))!;
        Assert.Equal(2, report.CurrentAgreements.Count);
        Assert.Contains(report.CurrentAgreements, x => x.StartsWith("الحصة: 10 جنيه"));
        Assert.Contains(report.CurrentAgreements, x => x.StartsWith("الشهر: 40 جنيه"));
        Assert.Equal(5, report.Agreements.Count);
        Assert.Contains(report.Agreements, x => x.StartsWith("الحصة: 15 جنيه"));
        Assert.Contains(report.Agreements, x => x.StartsWith("الشهر: 60 جنيه"));
        Assert.Equal(new TeacherReportSummary(0, 90, 10, 0, 0, 0, 90), report.Summary);
        Assert.Equal(9999, await db.TeacherAccounts.Select(x => x.CurrentBalance).SingleAsync());
        Assert.False(db.ChangeTracker.HasChanges());
        await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(), "massar-teacher-report-active-agreements.pdf"), TeacherDetailedReportPdf.Generate(report));
    }

    [Theory]
    [InlineData(80, 20, 0, 0.30, 79.70)]
    [InlineData(80, 20, 10, 0.26, 69.74)]
    [InlineData(80, 0, 0, 0, 80)]
    [InlineData(80, 20, 83, 0, -3)]
    public async Task Report_transfer_fee_uses_remaining_entitlement_and_debt_without_recording_a_payment(
        decimal earned, decimal platform, decimal debt, decimal fee, decimal net)
    {
        Purchase(At(4), earned + platform, earned, platform);
        if (debt > 0)
            db.Add(new TeacherPayoutAdjustment { Teacher = teacher, Amount = -debt,
                Status = TeacherPayoutAdjustmentStatus.Open, CreatedAt = At(5) });
        await db.SaveChangesAsync();

        var report = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(null, Cutoff), default))!;

        Assert.Equal(earned - debt, report.Summary.Closing);
        Assert.Equal(fee, report.VodafoneCashTransfer!.TransferFee);
        Assert.Equal(net, report.VodafoneCashTransfer.NetTransferAmount);
        Assert.Empty(await db.TeacherSettlementPayments.ToListAsync());
        Assert.Empty(await db.TeacherPayouts.ToListAsync());
        Assert.Equal(9999, await db.TeacherAccounts.Select(x => x.CurrentBalance).SingleAsync());
        Assert.False(db.ChangeTracker.HasChanges());
        if (fee == 0.30m)
            await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(), "massar-teacher-report-integrated-fee.pdf"),
                TeacherDetailedReportPdf.Generate(report));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Settled_income_is_excluded_from_new_transfer_basis_but_remains_in_historical_report(bool modernSettlement)
    {
        var sale = Purchase(At(4), 100, 80, 20);
        if (modernSettlement)
        {
            var settlement = new TeacherSettlement { Teacher = teacher, PeriodFrom = At(4), PeriodTo = At(5),
                Status = TeacherSettlementStatus.Paid, PaidAt = At(5), GrossDueAmount = 80, NetPayableAmount = 80,
                CreatedByUserId = teacher.UserId };
            settlement.Lines.Add(new() { Allocation = sale.Allocations.Single(), Amount = 80 });
            settlement.Payments.Add(new() { Amount = 80, PaymentMethod = "bank", PaidAt = At(5), PaidByUserId = teacher.UserId });
            db.Add(settlement);
        }
        else
        {
            var payout = new TeacherPayout { Teacher = teacher, Amount = 80, Status = PayoutStatus.Paid, PaidAt = At(5) };
            sale.Allocations.Single().Payout = payout;
            db.Add(payout);
        }
        sale.Allocations.Single().PayoutStatus = TeacherFinancialPayoutStatus.Paid;
        Purchase(At(6), 120, 80, 40);
        await db.SaveChangesAsync();

        var service = new TeacherDetailedReportService(db);
        var historical = (await service.ReadAsync(teacher.Id, new(null, new(2026, 9, 4)), default))!;
        var current = (await service.ReadAsync(teacher.Id, new(null, Cutoff), default))!;

        Assert.Equal(0.30m, historical.VodafoneCashTransfer!.TransferFee);
        Assert.Equal(80, current.Summary.Closing);
        Assert.Equal(40, current.VodafoneCashTransfer!.PlatformShareBasis);
        Assert.Equal(0.60m, current.VodafoneCashTransfer.TransferFee);
        Assert.Equal(79.40m, current.VodafoneCashTransfer.NetTransferAmount);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Reversed_purchase_does_not_inflate_commission_basis_of_remaining_income()
    {
        var cancelled = Purchase(At(4), 1000, 750, 250);
        cancelled.Allocations.Single().ReviewStatus = TeacherFinancialReviewStatus.Reversed;
        db.Add(new TeacherFinancialAllocation { Teacher = teacher, TeacherShareAmount = -750, PlatformShareAmount = -250,
            ReviewStatus = TeacherFinancialReviewStatus.Reversed, TeacherFinancialEvent = new() {
                SourceType = TeacherFinancialSourceType.Refund, OccurredAt = At(5), IdempotencyKey = Guid.NewGuid().ToString() } });
        Purchase(At(6), 100, 80, 20);
        await db.SaveChangesAsync();

        var report = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(null, Cutoff), default))!;

        Assert.Equal(80, report.Summary.Closing);
        Assert.Equal(20, report.VodafoneCashTransfer!.PlatformShareBasis);
        Assert.Equal(0.30m, report.VodafoneCashTransfer.TransferFee);
        Assert.Equal(79.70m, report.VodafoneCashTransfer.NetTransferAmount);
    }

    [Theory]
    [InlineData(0, 40, 20, 0.30)]
    [InlineData(2, 80, 20, 0.30)]
    public async Task Transfer_quote_uses_posted_refunds_at_cutoff_instead_of_current_reversed_amount(
        int refundDaysAfterCutoff, decimal expectedTeacher, decimal expectedPlatform, decimal expectedFee)
    {
        var sale = Purchase(At(4), 100, 80, 20);
        var original = sale.Allocations.Single();
        original.ReversedAmount = 40;
        var at = refundDaysAfterCutoff == 0 ? At(20) : At(30).AddDays(refundDaysAfterCutoff);
        db.Add(new TeacherFinancialAllocation { Teacher = teacher, TeacherShareAmount = -40, PlatformShareAmount = 0,
            ReviewStatus = TeacherFinancialReviewStatus.Reversed, TeacherFinancialEvent = new() {
                SourceType = TeacherFinancialSourceType.Refund, OccurredAt = at, IdempotencyKey = Guid.NewGuid().ToString() } });
        await db.SaveChangesAsync();

        var report = (await new TeacherDetailedReportService(db).ReadAsync(teacher.Id, new(null, Cutoff), default))!;

        Assert.Equal(expectedTeacher, report.Summary.Closing);
        Assert.Equal(expectedPlatform, report.VodafoneCashTransfer!.PlatformShareBasis);
        Assert.Equal(expectedFee, report.VodafoneCashTransfer.TransferFee);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Fully_reversed_purchase_after_cutoff_remains_in_historical_quote_only()
    {
        var sale = Purchase(At(4), 100, 80, 20);
        var original = sale.Allocations.Single();
        original.ReversedAmount = 80;
        original.ReviewStatus = TeacherFinancialReviewStatus.Reversed;
        original.PayoutStatus = TeacherFinancialPayoutStatus.Reversed;
        db.Add(new TeacherFinancialAllocation { Teacher = teacher, TeacherShareAmount = -80, PlatformShareAmount = -20,
            ReviewStatus = TeacherFinancialReviewStatus.Reversed, PayoutStatus = TeacherFinancialPayoutStatus.Reversed,
            TeacherFinancialEvent = new() { SourceType = TeacherFinancialSourceType.Cancellation,
                OccurredAt = At(30).AddDays(2), IdempotencyKey = Guid.NewGuid().ToString() } });
        Purchase(At(6), 120, 80, 40);
        await db.SaveChangesAsync();
        var service = new TeacherDetailedReportService(db);
        var historical = (await service.ReadAsync(teacher.Id, new(null, Cutoff), default))!;
        var later = (await service.ReadAsync(teacher.Id, new(null, new(2026, 10, 3)), default))!;

        Assert.Equal(160, historical.Summary.Closing);
        Assert.Equal(60, historical.VodafoneCashTransfer!.PlatformShareBasis);
        Assert.Equal(0.90m, historical.VodafoneCashTransfer.TransferFee);
        Assert.Equal(80, later.Summary.Closing);
        Assert.Equal(40, later.VodafoneCashTransfer!.PlatformShareBasis);
        Assert.Equal(0.60m, later.VodafoneCashTransfer.TransferFee);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private TeacherFinancialEvent Purchase(DateTime at, decimal paid, decimal teacherShare, decimal fee)
    {
        var target = purchaseCount++ == 0 ? lesson : new Lesson { Title = "حصة أخرى", ContentSectionId = lesson.ContentSectionId };
        if (target != lesson) db.Add(target);
        var sale = new TeacherFinancialEvent { SourceType = TeacherFinancialSourceType.DirectPurchase, SourceId = Guid.NewGuid(),
            Student = student, TargetType = SalesTargetType.Lesson, TargetId = target.Id, PaidAmount = paid, GrossAmount = paid,
            OccurredAt = at, CreatedAt = at, IdempotencyKey = Guid.NewGuid().ToString() };
        sale.Allocations.Add(new() { Teacher = teacher, TeacherShareAmount = teacherShare, PlatformShareAmount = fee,
            StudentNameSnapshot = student.FullName, ContentNameSnapshot = target.Title });
        db.AddRange(sale, new StudentAccessGrant { User = student, LessonId = target.Id, GrantType = CodeType.Lesson,
            GrantedAt = at, CreatedAt = at });
        return sale;
    }
}
