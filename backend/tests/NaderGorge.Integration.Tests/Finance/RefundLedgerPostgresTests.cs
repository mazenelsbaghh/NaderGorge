using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services.Finance;
using NaderGorge.Integration.Tests.AdminAI;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class RefundLedgerPostgresTests
{
    [Fact]
    public async Task ExternalPackageRefund_RevokesAccessAndPostsCashWithActorAndReason()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();

        var student = new User { FullName = "Refund student", PhoneNumber = "01000000031", PasswordHash = "test" };
        var teacherUser = new User { FullName = "Refund teacher", PhoneNumber = "01000000032", PasswordHash = "test" };
        var actor = new User { FullName = "Refund operator", PhoneNumber = "01000000033", PasswordHash = "test" };
        var teacher = new TeacherProfile { User = teacherUser };
        var subject = new Subject { Name = "Refund subject", NormalizedName = "refund subject" };
        var package = new Package
        {
            Name = "Refund package", Description = "PostgreSQL refund workflow", Price = 100m,
            Subject = subject, Teacher = teacher, TargetGrade = "SecondSecondary"
        };
        var grant = new StudentAccessGrant { User = student, GrantType = CodeType.Package, PackageId = package.Id, IsActive = true };
        var treasuryAccount = new FinancialAccount
        {
            Code = "1000", Name = "Cash", Type = FinancialAccountType.Asset,
            NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Treasury
        };
        var refundAccount = new FinancialAccount
        {
            Code = "4100", Name = "Refunds", Type = FinancialAccountType.ContraRevenue,
            NormalSide = FinancialNormalSide.Debit, Role = FinancialAccountRole.Refunds
        };
        var cashbox = new TreasuryAccount { Name = "Refund cashbox", Type = TreasuryAccountType.Cashbox, FinancialAccountId = treasuryAccount.Id };
        db.AddRange(student, teacherUser, actor, teacher, subject, package, grant, treasuryAccount, refundAccount, cashbox);
        await db.SaveChangesAsync();

        using var services = new ServiceCollection()
            .AddSingleton<IAppDbContext>(db)
            .AddSingleton<TeacherAccountingService>()
            .AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ApiResponse).Assembly))
            .BuildServiceProvider();
        var mediator = services.GetRequiredService<IMediator>();
        var operations = new PlatformFinanceOperationsService(
            db, new FinancialPostingService(db), new BalanceService(db, NullLogger<BalanceService>.Instance));
        var controller = new AdminPlatformFinanceController(
            null!, null!, operations, null!, null!, null!, db,
            null!, null!, null!, null!, mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString())], "test"))
                }
            }
        };

        var request = new ExternalPackageRefundBody(grant.Id, null, student.Id, teacher.Id,
            75m, 0m, cashbox.Id, "طلب الطالب", "CASH-REFUND-1");
        var result = await controller.CreateExternalPackageRefund(request, default);

        Assert.IsType<OkObjectResult>(result.Result);
        await using var verifyDb = fixture.CreateDbContext();
        Assert.False((await verifyDb.StudentAccessGrants.SingleAsync(item => item.Id == grant.Id)).IsActive);
        var refund = await verifyDb.PlatformRefunds.SingleAsync();
        Assert.Equal(75m, refund.TotalAmount);
        Assert.Equal("طلب الطالب", refund.Reason);
        Assert.Equal(PlatformRefundStatus.Posted, refund.Status);
        Assert.Equal(grant.Id, refund.AccessGrantId);
        Assert.Equal(actor.Id, refund.CreatedByUserId);
        var journal = await verifyDb.JournalEntries.Include(item => item.Lines).SingleAsync();
        Assert.Equal(actor.Id, journal.ActorUserId);
        Assert.Equal(75m, journal.Lines.Sum(line => line.Debit));
        Assert.Equal(75m, journal.Lines.Sum(line => line.Credit));
        Assert.Empty(await verifyDb.BalanceTransactions.ToListAsync());

        await using var replayDb = fixture.CreateDbContext();
        using var replayServices = new ServiceCollection()
            .AddSingleton<IAppDbContext>(replayDb)
            .AddSingleton<TeacherAccountingService>()
            .AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ApiResponse).Assembly))
            .BuildServiceProvider();
        var replayController = new AdminPlatformFinanceController(
            null!, null!, new PlatformFinanceOperationsService(replayDb,
                new FinancialPostingService(replayDb),
                new BalanceService(replayDb, NullLogger<BalanceService>.Instance)),
            null!, null!, null!, replayDb, null!, null!, null!, null!,
            replayServices.GetRequiredService<IMediator>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString())], "test"))
                }
            }
        };
        Assert.IsType<OkObjectResult>(
            (await replayController.CreateExternalPackageRefund(request, default)).Result);
        Assert.IsType<ConflictObjectResult>(
            (await replayController.CreateExternalPackageRefund(request with { PlatformAmount = 74m }, default)).Result);
        Assert.Equal(1, await replayDb.PlatformRefunds.CountAsync());
        Assert.Equal(1, await replayDb.JournalEntries.CountAsync());
    }

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
