using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class AdminFinanceCenterTests
{
    [Fact]
    public async Task Settlement_reserves_each_allocation_once_then_allows_only_valid_state_transitions()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Finance Admin", "01000000031");
        var teacher = await SeedTeacherAsync(db, "Settlement Teacher", "01000000032");
        var allocation = await RecordAllocationAsync(db, teacher, 60m, "settlement-line");
        var controller = CreateController(db, admin.Id);
        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow.AddDays(1);

        var created = await controller.CreateSettlement(new CreateSettlementDto(teacher.Id, from, to, null, [allocation.Id]), CancellationToken.None);
        Assert.IsType<CreatedAtActionResult>(created);

        var settlement = Assert.Single(db.TeacherSettlements);
        Assert.Equal(TeacherSettlementStatus.Draft, settlement.Status);
        Assert.Equal(60m, Assert.Single(db.TeacherAccounts).ReservedBalance);
        Assert.Equal(TeacherFinancialPayoutStatus.Reserved, (await db.TeacherFinancialAllocations.SingleAsync()).PayoutStatus);
        Assert.Single(db.FinancialInvoices);

        var duplicate = await controller.CreateSettlement(new CreateSettlementDto(teacher.Id, from, to, null, [allocation.Id]), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(duplicate);

        Assert.IsType<OkObjectResult>(await controller.ReviewSettlement(settlement.Id, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.ApproveSettlement(settlement.Id, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.PaySettlement(settlement.Id,
            new PaySettlementDto("bank", "TX-1", "https://files.test/proof.pdf", 60m), CancellationToken.None));

        Assert.Equal(TeacherSettlementStatus.Paid, settlement.Status);
        Assert.Equal(TeacherFinancialPayoutStatus.Paid, (await db.TeacherFinancialAllocations.SingleAsync()).PayoutStatus);
        Assert.Equal(0m, (await db.TeacherAccounts.SingleAsync()).ReservedBalance);
        Assert.Equal(FinancialInvoiceStatus.Paid, (await db.FinancialInvoices.SingleAsync()).Status);
        Assert.Single(db.TeacherSettlementPayments);
    }

    [Fact]
    public async Task Partial_paid_reversal_creates_one_open_debt_and_reusing_its_key_is_safe()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Refund Admin", "01000000041");
        var teacher = await SeedTeacherAsync(db, "Refund Teacher", "01000000042");
        var allocation = await RecordAllocationAsync(db, teacher, 60m, "paid-refund-line");
        var payout = new TeacherPayout { Id = Guid.NewGuid(), TeacherId = teacher.Id, Amount = 60m, Status = PayoutStatus.Paid, PaidAt = DateTime.UtcNow };
        db.TeacherPayouts.Add(payout);
        allocation.PayoutId = payout.Id;
        allocation.PayoutStatus = TeacherFinancialPayoutStatus.Paid;
        await db.SaveChangesAsync();
        var controller = CreateController(db, admin.Id);
        var request = new CreateReversalDto([new ReversalLineDto(allocation.Id, 25m)], "partial refund",
            TeacherReversalDisposition.NextSettlementDeduction, "refund:partial:line");

        Assert.IsType<OkObjectResult>(await controller.ReverseSelectedLines(request, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.ReverseSelectedLines(request, CancellationToken.None));

        var original = await db.TeacherFinancialAllocations.SingleAsync(x => x.Id == allocation.Id);
        Assert.Equal(25m, original.ReversedAmount);
        Assert.Equal(TeacherFinancialPayoutStatus.Paid, original.PayoutStatus);
        var debt = Assert.Single(db.TeacherPayoutAdjustments);
        Assert.Equal(-25m, debt.Amount);
        Assert.Equal(TeacherPayoutAdjustmentStatus.Open, debt.Status);
        Assert.Single(db.TeacherFinancialEvents.Where(x => x.IdempotencyKey == request.IdempotencyKey));
        var reversal = Assert.Single(db.TeacherFinancialAllocations.Where(x => x.TeacherShareAmount < 0m));
        Assert.Equal(-25m, reversal.TeacherShareAmount);
    }

    private static AdminTeacherFinanceCenterController CreateController(NaderGorge.Infrastructure.Data.AppDbContext db, Guid actorId) => new(db, new NoopMediator())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId.ToString())], "test"))
            }
        }
    };

    private static async Task<TeacherProfile> SeedTeacherAsync(NaderGorge.Infrastructure.Data.AppDbContext db, string name, string phone)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, name, phone);
        var teacher = new TeacherProfile { Id = Guid.NewGuid(), UserId = user.Id, CommissionRate = 0.6m };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();
        return teacher;
    }

    private static async Task<TeacherFinancialAllocation> RecordAllocationAsync(NaderGorge.Infrastructure.Data.AppDbContext db,
        TeacherProfile teacher, decimal amount, string key)
    {
        var service = new TeacherAccountingService(db);
        await service.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.DirectPurchase, Guid.NewGuid(), null, SalesTargetType.Lesson, Guid.NewGuid(),
            100m, 0m, 100m, 0m, 100m - amount, key, "{}", DateTime.UtcNow,
            TeacherFinancialReviewStatus.AutoApproved,
            [new TeacherFinancialAllocationInput(teacher.Id, TeacherAllocationMode.FixedAmount, amount, 100m, amount,
                100m - amount, null, null, "Lesson")]), CancellationToken.None);
        return await db.TeacherFinancialAllocations.SingleAsync();
    }

    private sealed class NoopMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
