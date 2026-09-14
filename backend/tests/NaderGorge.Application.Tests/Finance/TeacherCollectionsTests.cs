using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.TeacherFinanceCenter;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Finance;

public sealed class TeacherCollectionsTests
{
    [Fact]
    public async Task Statement_counts_only_confirmed_teacher_transfers_and_preserves_totals_across_pages()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var teacher = new TeacherProfile { User = User("teacher") };
        var otherTeacher = new TeacherProfile { User = User("other-teacher") };
        var emptyTeacher = new TeacherProfile { User = User("empty-teacher") };
        var wallet = new DigitalWallet { PhoneNumber = "01000000001", Label = "VodafoneCash", IsActive = false };
        var student = User("student");
        db.AddRange(teacher, otherTeacher, emptyTeacher, wallet, student);
        await db.SaveChangesAsync();
        var vf = Transfer(teacher.Id, 100.25m, RechargeRequestStatus.Matched, "VodafoneCash");
        var alias = Transfer(teacher.Id, 20.50m, RechargeRequestStatus.Approved, " VF-CASH ");
        var manual = Transfer(teacher.Id, 40m, RechargeRequestStatus.Approved);
        var orange = Transfer(teacher.Id, 30m, RechargeRequestStatus.Matched, "OrangeCash");
        db.AddRange(vf, alias, manual, orange,
            Transfer(otherTeacher.Id, 1000m, RechargeRequestStatus.Approved, "VodafoneCash"),
            Transfer(null, 2000m, RechargeRequestStatus.Matched, "VodafoneCash"));
        foreach (var status in new[] { RechargeRequestStatus.Pending, RechargeRequestStatus.Rejected,
            RechargeRequestStatus.Cancelled, RechargeRequestStatus.Expired })
            db.Add(Transfer(teacher.Id, 500m, status, "VodafoneCash"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var handler = new GetTeacherCollectionsQueryHandler(db);

        var first = await handler.Handle(new(teacher.Id, PageSize: 2), CancellationToken.None);
        var second = await handler.Handle(new(teacher.Id, Page: 2, PageSize: 2), CancellationToken.None);
        Assert.True(first.Success);
        Assert.Equal(190.75m, first.Data!.TotalAmount);
        Assert.Equal(120.75m, first.Data.VodafoneCashAmount);
        Assert.Equal(70m, first.Data.OtherOrUnverifiedAmount);
        Assert.Equal(4, first.Data.TotalCount);
        Assert.Equal(2, first.Data.VodafoneCashCount);
        Assert.Equal(first.Data.TotalAmount, second.Data!.TotalAmount);
        var rows = first.Data.Items.Concat(second.Data.Items).ToList();
        Assert.Equal(new[] { vf.Id, alias.Id, manual.Id, orange.Id }.Order(), rows.Select(x => x.Id).Order());
        Assert.False(rows.Single(x => x.Id == manual.Id).IsVodafoneCash);
        Assert.False(rows.Single(x => x.Id == orange.Id).IsVodafoneCash);

        var filtered = await handler.Handle(new(teacher.Id, VodafoneOnly: true), CancellationToken.None);
        Assert.Equal(190.75m, filtered.Data!.TotalAmount);
        Assert.Equal(2, filtered.Data.FilteredCount);
        Assert.All(filtered.Data.Items, row => Assert.True(row.IsVodafoneCash));
        var empty = await handler.Handle(new(emptyTeacher.Id), CancellationToken.None);
        Assert.True(empty.Success);
        Assert.Equal(0m, empty.Data!.TotalAmount);
        Assert.Empty(empty.Data.Items);
        Assert.False((await handler.Handle(new(Guid.NewGuid()), CancellationToken.None)).Success);
        Assert.False((await handler.Handle(new(teacher.Id, Page: int.MaxValue), CancellationToken.None)).Success);

        RechargeRequest Transfer(Guid? teacherId, decimal amount, RechargeRequestStatus status, string? sender = null) => new()
        {
            TeacherId = teacherId, UserId = student.Id, WalletId = wallet.Id, Amount = amount, Status = status,
            ResolvedAt = status is RechargeRequestStatus.Matched or RechargeRequestStatus.Approved ? DateTime.UtcNow : null,
            MatchedSmsLog = sender == null ? null : new IncomingSmsLog
            {
                WalletId = wallet.Id, Sender = sender, Body = "test receipt", DeduplicationHash = Guid.NewGuid().ToString(),
                ReceivedAt = DateTime.UtcNow, TransferReference = Guid.NewGuid().ToString()
            }
        };
    }

    private static User User(string name) => new() { FullName = name, PhoneNumber = name, PasswordHash = "test" };
}
