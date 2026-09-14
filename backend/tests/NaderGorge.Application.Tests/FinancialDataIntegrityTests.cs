using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using Xunit;

namespace NaderGorge.Application.Tests;

public class FinancialDataIntegrityTests
{
    private sealed class FakePostgresSerializationException : Exception
    {
        public string SqlState => "40001";
    }

    [Fact]
    public async Task AddCredit_WhenReferenceAlreadyCredited_ReturnsExistingTransactionWithoutSecondCredit()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Recharge Student", "01021212121");
        var rechargeRequestId = Guid.NewGuid();
        var balanceService = new BalanceService(db, NullLogger<BalanceService>.Instance);

        var first = await balanceService.AddCredit(
            user.Id,
            100m,
            "First credit",
            rechargeRequestId,
            "DigitalRecharge");

        var second = await balanceService.AddCredit(
            user.Id,
            100m,
            "Duplicate credit",
            rechargeRequestId,
            "DigitalRecharge");

        var balance = await db.StudentBalances.SingleAsync(x => x.UserId == user.Id);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(100m, balance.CurrentBalance);
        Assert.Single(await db.BalanceTransactions.ToListAsync());
    }

    [Fact]
    public async Task SerializationRetryHelper_When40001Occurs_RetriesBeforeReturning()
    {
        var attempts = 0;

        var result = await SerializationRetryHelper.ExecuteAsync(
            _ =>
            {
                attempts++;
                if (attempts == 1)
                    throw new FakePostgresSerializationException();

                return Task.FromResult("ok");
            },
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task DeductBalance_WhenDebitedTwice_DoesNotCreateNegativeBalanceAndReconcilesLedger()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Debit Student", "01023232323");
        var balanceService = new BalanceService(db, NullLogger<BalanceService>.Instance);

        await balanceService.AddCredit(user.Id, 100m, "Initial credit");
        await balanceService.DeductBalance(user.Id, 75m, "First debit");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            balanceService.DeductBalance(user.Id, 75m, "Second debit"));

        var balance = await db.StudentBalances.SingleAsync(x => x.UserId == user.Id);
        var ledgerTotal = await db.BalanceTransactions
            .Where(tx => tx.StudentBalanceId == balance.Id)
            .SumAsync(tx => tx.Amount);

        Assert.Contains("Insufficient balance", failure.Message);
        Assert.Equal(25m, balance.CurrentBalance);
        Assert.Equal(balance.CurrentBalance, ledgerTotal);
        Assert.True(balance.Version >= 2);
    }

    [Fact]
    public async Task SaveChanges_WhenDeletingUserWithFinancialHistory_SoftDeletesPrincipal()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Soft Delete Student", "01024242424");
        db.StudentBalances.Add(new StudentBalance { UserId = user.Id, CurrentBalance = 10m });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var userToDelete = await db.Users.SingleAsync(row => row.Id == user.Id);
        db.Users.Remove(userToDelete);
        await db.SaveChangesAsync();

        var storedUser = await db.Users.IgnoreQueryFilters().SingleAsync(row => row.Id == user.Id);
        Assert.False(storedUser.IsActive);
        Assert.True(storedUser.IsDeleted);
        Assert.NotNull(storedUser.DeletedAt);
        Assert.Equal("Soft-deleted because financial history exists.", storedUser.SuspensionReason);
    }

    [Fact]
    public void EfModel_ContainsPhase2FinanceConstraintsAndIndexes()
    {
        using var db = TestAppDbContextFactory.Create();
        var model = db.GetService<IDesignTimeModel>().Model;

        var teacherAccount = model.FindEntityType(typeof(TeacherAccount));
        Assert.NotNull(teacherAccount);
        Assert.Contains(
            teacherAccount!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_teacher_accounts_reserved_available");

        var balanceTransaction = model.FindEntityType(typeof(BalanceTransaction));
        Assert.NotNull(balanceTransaction);
        Assert.Contains(
            balanceTransaction!.GetIndexes(),
            index => index.IsUnique &&
                index.GetFilter() == "\"ReferenceId\" IS NOT NULL AND \"TransactionType\" IN ('DigitalRecharge', 'CodeRedemption')");

        var incomingSms = model.FindEntityType(typeof(IncomingSmsLog));
        Assert.NotNull(incomingSms);
        Assert.Contains(
            incomingSms!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_incoming_sms_logs_match_consistency");
        Assert.Contains(
            incomingSms.GetIndexes(),
            index => index.IsUnique && index.GetFilter() == "\"MatchedRechargeRequestId\" IS NOT NULL");
        Assert.Single(incomingSms.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Any(property => property.Name == nameof(IncomingSmsLog.MatchedRechargeRequestId)));

        var grant = model.FindEntityType(typeof(StudentAccessGrant));
        Assert.NotNull(grant);
        var grantTargetCheck = Assert.Single(
            grant!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_student_access_grants_target_shape");
        Assert.Contains("\"PackageId\" IS NOT NULL", grantTargetCheck.Sql);
        Assert.Contains("\"LessonVideoId\" IS NOT NULL", grantTargetCheck.Sql);

        Assert.Contains(
            grant.GetIndexes(),
            index => index.IsUnique && index.GetFilter() == "\"IsActive\" = TRUE AND \"GrantType\" = 3 AND \"LessonId\" IS NOT NULL");

        var studentBalance = model.FindEntityType(typeof(StudentBalance));
        Assert.NotNull(studentBalance);
        Assert.True(studentBalance!.FindProperty(nameof(StudentBalance.Version))!.IsConcurrencyToken);
        Assert.True(teacherAccount.FindProperty(nameof(TeacherAccount.Version))!.IsConcurrencyToken);
    }

    [Fact]
    public void EfModel_RestrictsFinancialHistoryDeletes()
    {
        using var db = TestAppDbContextFactory.Create();
        var model = db.GetService<IDesignTimeModel>().Model;

        AssertDeleteBehavior<StudentBalance>("UserId", DeleteBehavior.NoAction);
        AssertDeleteBehavior<BalanceTransaction>("StudentBalanceId", DeleteBehavior.Restrict);
        AssertDeleteBehavior<TeacherAccount>("TeacherId", DeleteBehavior.Restrict);
        AssertDeleteBehavior<TeacherPayout>("TeacherId", DeleteBehavior.Restrict);
        AssertDeleteBehavior<RechargeRequest>("WalletId", DeleteBehavior.Restrict);
        AssertDeleteBehavior<IncomingSmsLog>("WalletId", DeleteBehavior.Restrict);

        void AssertDeleteBehavior<TEntity>(string foreignKeyPropertyName, DeleteBehavior expected)
        {
            var entity = model.FindEntityType(typeof(TEntity));
            Assert.NotNull(entity);
            var foreignKey = entity!.GetForeignKeys()
                .Single(key => key.Properties.Any(property => property.Name == foreignKeyPropertyName));
            Assert.Equal(expected, foreignKey.DeleteBehavior);
        }
    }
}
