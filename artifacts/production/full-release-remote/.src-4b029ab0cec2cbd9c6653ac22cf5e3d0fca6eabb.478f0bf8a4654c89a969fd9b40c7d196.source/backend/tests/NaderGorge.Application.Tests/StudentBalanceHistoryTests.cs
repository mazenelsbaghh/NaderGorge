using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public sealed class StudentBalanceHistoryTests
{
    [Fact]
    public async Task Handle_IncludesConsumedContentCodeWithoutCreatingFinancialTransaction()
    {
        await using var db = TestAppDbContextFactory.Create();
        var studentId = Guid.NewGuid();
        var group = new CodeGroup { Name = "ترم الفيزياء الأول", CodeType = CodeType.Term };
        var code = new AccessCode
        {
            CodeGroup = group,
            IsConsumed = true,
            ConsumedByUserId = studentId,
            ConsumedAt = DateTime.UtcNow,
        };
        db.AccessCodes.Add(code);
        await db.SaveChangesAsync();

        var result = await new GetStudentBalanceQueryHandler(db).Handle(new GetStudentBalanceQuery(studentId), default);

        var transaction = Assert.Single(result.Data!.RecentTransactions);
        Assert.Equal(code.Id, transaction.Id);
        Assert.Equal("ContentCodeRedemption", transaction.TransactionType);
        Assert.Contains("ترم الفيزياء الأول", transaction.Description);
        Assert.False(transaction.AffectsBalance);
        Assert.Equal(0, transaction.Amount);
    }

    [Fact]
    public async Task Handle_ExcludesBalanceCodesAndMergesFinancialTransactionsByDate()
    {
        await using var db = TestAppDbContextFactory.Create();
        var studentId = Guid.NewGuid();
        var balance = new StudentBalance { UserId = studentId, CurrentBalance = 500 };
        var balanceTransaction = new BalanceTransaction
        {
            StudentBalance = balance,
            Amount = 500,
            BalanceAfter = 500,
            TransactionType = "CodeRedemption",
            Description = "شحن رصيد من كود",
            CreatedAt = DateTime.UtcNow,
        };
        var balanceGroup = new CodeGroup { Name = "كود رصيد", CodeType = CodeType.Balance };
        db.BalanceTransactions.Add(balanceTransaction);
        db.AccessCodes.Add(new AccessCode
        {
            CodeGroup = balanceGroup,
            IsConsumed = true,
            ConsumedByUserId = studentId,
            ConsumedAt = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var result = await new GetStudentBalanceQueryHandler(db).Handle(new GetStudentBalanceQuery(studentId), default);

        var transaction = Assert.Single(result.Data!.RecentTransactions);
        Assert.Equal(balanceTransaction.Id, transaction.Id);
        Assert.True(transaction.AffectsBalance);
    }
}
