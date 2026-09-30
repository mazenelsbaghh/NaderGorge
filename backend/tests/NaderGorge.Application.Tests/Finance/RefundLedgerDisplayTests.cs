using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Finance;

public sealed class RefundLedgerDisplayTests
{
    [Fact]
    public async Task RefundLedger_ShowsAmountReasonAndActualOperatorForPostedAndLegacyRefunds()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"refund-ledger-{Guid.NewGuid()}").Options);
        var student = new User { FullName = "Student", PhoneNumber = "01000000001" };
        var creator = new User { FullName = "Draft author", PhoneNumber = "01000000002" };
        var poster = new User { FullName = "Refund operator", PhoneNumber = "01000000003" };
        var legacyOperator = new User { FullName = "Legacy operator", PhoneNumber = "01000000004" };
        var journal = new JournalEntry { ActorUserId = poster.Id };
        var refund = new PlatformRefund
        {
            StudentId = student.Id, CreatedByUserId = creator.Id, JournalEntryId = journal.Id,
            OriginalSourceId = Guid.NewGuid(), OriginalSourceType = "PurchaseOperation",
            PlatformAmount = 80m, TeacherAmount = 20m, Reason = "رد مصروف الحصة",
            Method = PlatformRefundMethod.Cash, Status = PlatformRefundStatus.Posted
        };
        var balance = new StudentBalance { User = student };
        var legacy = new BalanceTransaction
        {
            StudentBalance = balance, Amount = 25m, TransactionType = "Refund",
            Description = "سبب قديم", PerformedByUserId = legacyOperator.Id
        };
        db.AddRange(student, creator, poster, legacyOperator, journal, refund, balance, legacy);
        await db.SaveChangesAsync();

        var controller = new AdminPlatformFinanceController(
            null!, null!, null!, null!, null!, null!, db,
            null!, null!, null!, null!, null!);
        var response = await controller.Refunds(null, null, default);
        var rows = Assert.IsAssignableFrom<IEnumerable<PlatformRefundListItem>>(
            Assert.IsType<OkObjectResult>(response.Result).Value);

        var posted = Assert.Single(rows, item => item.Id == refund.Id);
        Assert.Equal(100m, posted.TotalAmount);
        Assert.Equal("رد مصروف الحصة", posted.Reason);
        Assert.Equal(poster.Id, posted.ProcessedByUserId);
        Assert.Equal(poster.FullName, posted.ProcessedByName);
        var old = Assert.Single(rows, item => item.Id == legacy.Id);
        Assert.Equal(25m, old.TotalAmount);
        Assert.Equal("سبب قديم", old.Reason);
        Assert.Equal(legacyOperator.FullName, old.ProcessedByName);
    }
}
