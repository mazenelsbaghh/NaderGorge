using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NaderGorge.Application.Features.Admin.Finance.Commands;
using NaderGorge.Application.Features.Teacher.Finance.Commands;
using NaderGorge.Application.Features.Teacher.Finance.Queries;
using NaderGorge.Application.Features.Codes.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Application.Tests.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NaderGorge.Application.Tests.Finance;

public class TestLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {}
}

public class CommissionTests
{
    [Fact]
    [Trait("Category", "Finance")]
    public async Task RequestPayout_ValidAmount_CreatesPendingPayout()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Test Teacher 2", "01077777777");
        var teacherProfile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            CommissionRate = 0.15m
        };
        db.TeacherProfiles.Add(teacherProfile);

        var account = new TeacherAccount
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherProfile.Id,
            TotalEarnings = 500.00m,
            CurrentBalance = 500.00m,
            CommissionRate = 0.15m
        };
        db.TeacherAccounts.Add(account);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new RequestPayoutCommandHandler(db, audit);

        var result = await handler.Handle(
            new RequestPayoutCommand(teacherUser.Id, 200.00m),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200.00m, result.Data!.Amount);
        Assert.Equal("Pending", result.Data.Status);

        var payout = await db.TeacherPayouts.FirstOrDefaultAsync(p => p.TeacherId == teacherProfile.Id);
        Assert.NotNull(payout);
        Assert.Equal(200.00m, payout!.Amount);
        Assert.Equal(PayoutStatus.Pending, payout.Status);

        // Verify balance not deducted yet
        var updatedAccount = await db.TeacherAccounts.FindAsync(account.Id);
        Assert.Equal(500.00m, updatedAccount!.CurrentBalance);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task RequestPayout_AmountGreaterThanBalance_ReturnsFailure()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Test Teacher 3", "01088888888");
        var teacherProfile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            CommissionRate = 0.15m
        };
        db.TeacherProfiles.Add(teacherProfile);

        var account = new TeacherAccount
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherProfile.Id,
            TotalEarnings = 100.00m,
            CurrentBalance = 100.00m,
            CommissionRate = 0.15m
        };
        db.TeacherAccounts.Add(account);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new RequestPayoutCommandHandler(db, audit);

        var result = await handler.Handle(
            new RequestPayoutCommand(teacherUser.Id, 150.00m),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("رصيدك الحالي لا يكفي", result.Message);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task ResolvePayout_Approve_DeductsBalance()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Test Teacher 4", "01099999999");
        var teacherProfile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            CommissionRate = 0.15m
        };
        db.TeacherProfiles.Add(teacherProfile);

        var account = new TeacherAccount
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherProfile.Id,
            TotalEarnings = 500.00m,
            CurrentBalance = 500.00m,
            CommissionRate = 0.15m
        };
        db.TeacherAccounts.Add(account);

        var payout = new TeacherPayout
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherProfile.Id,
            Amount = 200.00m,
            Status = PayoutStatus.Pending
        };
        db.TeacherPayouts.Add(payout);
        await db.SaveChangesAsync();

        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01010101010");
        var audit = new TestAuditRepository();
        var handler = new ResolvePayoutCommandHandler(db, audit);

        var result = await handler.Handle(
            new ResolvePayoutCommand(payout.Id, PayoutStatus.Paid, null, adminUser.Id),
            CancellationToken.None);

        Assert.True(result.Success);

        var updatedPayout = await db.TeacherPayouts.FindAsync(payout.Id);
        Assert.Equal(PayoutStatus.Paid, updatedPayout!.Status);
        Assert.Equal(adminUser.Id, updatedPayout.HandledByUserId);

        var updatedAccount = await db.TeacherAccounts.FindAsync(account.Id);
        Assert.Equal(300.00m, updatedAccount!.CurrentBalance);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task ResolvePayout_Reject_PreservesBalance()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Test Teacher 5", "01012121212");
        var teacherProfile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            CommissionRate = 0.15m
        };
        db.TeacherProfiles.Add(teacherProfile);

        var account = new TeacherAccount
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherProfile.Id,
            TotalEarnings = 500.00m,
            CurrentBalance = 500.00m,
            CommissionRate = 0.15m
        };
        db.TeacherAccounts.Add(account);

        var payout = new TeacherPayout
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherProfile.Id,
            Amount = 200.00m,
            Status = PayoutStatus.Pending
        };
        db.TeacherPayouts.Add(payout);
        await db.SaveChangesAsync();

        var adminUser = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01010101011");
        var audit = new TestAuditRepository();
        var handler = new ResolvePayoutCommandHandler(db, audit);

        var result = await handler.Handle(
            new ResolvePayoutCommand(payout.Id, PayoutStatus.Rejected, "Invalid paperwork", adminUser.Id),
            CancellationToken.None);

        Assert.True(result.Success);

        var updatedPayout = await db.TeacherPayouts.FindAsync(payout.Id);
        Assert.Equal(PayoutStatus.Rejected, updatedPayout!.Status);
        Assert.Equal("Invalid paperwork", updatedPayout.RejectionReason);

        var updatedAccount = await db.TeacherAccounts.FindAsync(account.Id);
        Assert.Equal(500.00m, updatedAccount!.CurrentBalance);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task GetTeacherAccount_ReturnsCorrectDetails()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        var teacherUser = await TestAppDbContextFactory.SeedUserAsync(db, "Test Teacher 6", "01013131313");
        var teacherProfile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = teacherUser.Id,
            CommissionRate = 0.10m
        };
        db.TeacherProfiles.Add(teacherProfile);
        await db.SaveChangesAsync();

        var handler = new GetTeacherAccountQueryHandler(db);

        var result = await handler.Handle(
            new GetTeacherAccountQuery(teacherUser.Id),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(teacherProfile.Id, result.Data!.TeacherId);
        Assert.Equal("Test Teacher 6", result.Data.TeacherName);
        Assert.Equal(0m, result.Data.CurrentBalance);
        Assert.Equal(0.10m, result.Data.CommissionRate);
    }
}
