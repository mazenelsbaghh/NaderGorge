using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Domain.Entities;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class RefundLedgerPostgresTests
{
    [Fact]
    public async Task PostedRefundLedger_UsesThePostingActorName()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var student = new User { FullName = "Refund student", PhoneNumber = "01000000021", PasswordHash = "test" };
        var creator = new User { FullName = "Draft creator", PhoneNumber = "01000000022", PasswordHash = "test" };
        var poster = new User { FullName = "Cash operator", PhoneNumber = "01000000023", PasswordHash = "test" };
        var journal = new JournalEntry { ActorUserId = poster.Id, SourceType = "PlatformRefund", PostingKind = "RefundPost" };
        var refund = new PlatformRefund
        {
            OriginalSourceId = Guid.NewGuid(), OriginalSourceType = "PurchaseOperation",
            StudentId = student.Id, CreatedByUserId = creator.Id, JournalEntryId = journal.Id,
            PlatformAmount = 70m, TeacherAmount = 30m, Method = PlatformRefundMethod.Cash,
            Status = PlatformRefundStatus.Posted, Reason = "استرداد بطلب الطالب"
        };
        db.AddRange(student, creator, poster, journal, refund);
        await db.SaveChangesAsync();

        var controller = new AdminPlatformFinanceController(
            null!, null!, null!, null!, null!, null!, db,
            null!, null!, null!, null!, null!);
        var response = await controller.Refunds(null, null, default);
        var rows = Assert.IsAssignableFrom<IEnumerable<PlatformRefundListItem>>(
            Assert.IsType<OkObjectResult>(response.Result).Value);
        var row = Assert.Single(rows, item => item.Id == refund.Id);
        Assert.Equal(100m, row.TotalAmount);
        Assert.Equal("استرداد بطلب الطالب", row.Reason);
        Assert.Equal(poster.Id, row.ProcessedByUserId);
        Assert.Equal(poster.FullName, row.ProcessedByName);
    }
}
