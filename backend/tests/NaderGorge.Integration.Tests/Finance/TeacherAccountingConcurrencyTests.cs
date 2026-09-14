using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Integration.Tests.LiveSupport;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class TeacherAccountingConcurrencyTests(PostgresLiveSupportFixture fixture)
    : IClassFixture<PostgresLiveSupportFixture>
{
    [Fact]
    public async Task ConcurrentPurchaseRegression_CreditsEverySaleWithoutVersionConflicts()
    {
        await fixture.ResetAsync();
        var (teacherId, studentId, accountId) = await SeedAccountAsync();
        const int saleCount = 12;

        var recordings = Enumerable.Range(0, saleCount)
            .Select(index => RecordSaleAsync(teacherId, studentId, index));
        await Task.WhenAll(recordings);

        await using var verificationDb = CreateDbContext();
        var account = await verificationDb.TeacherAccounts.SingleAsync(candidate => candidate.Id == accountId);
        Assert.Equal(saleCount, account.TotalEarnings);
        Assert.Equal(saleCount, account.CurrentBalance);
        Assert.Equal(saleCount, account.Version);
    }

    private async Task<(Guid TeacherId, Guid StudentId, Guid AccountId)> SeedAccountAsync()
    {
        await using var db = CreateDbContext();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var teacherUser = new User { FullName = "Concurrency Teacher", PhoneNumber = $"T{suffix}", PasswordHash = "hash" };
        var student = new User { FullName = "Concurrency Student", PhoneNumber = $"S{suffix}", PasswordHash = "hash" };
        var teacher = new TeacherProfile { User = teacherUser, Specialization = "Math", ContactInfo = "test" };
        var account = new TeacherAccount { Teacher = teacher };
        db.AddRange(teacherUser, student, teacher, account);
        await db.SaveChangesAsync();
        return (teacher.Id, student.Id, account.Id);
    }

    private async Task RecordSaleAsync(Guid teacherId, Guid studentId, int saleNumber)
    {
        await using var db = CreateDbContext();
        var service = new TeacherAccountingService(db);
        await service.RecordEventAsync(CreateSale(teacherId, studentId, saleNumber), CancellationToken.None);
    }

    private static TeacherFinancialEventInput CreateSale(Guid teacherId, Guid studentId, int saleNumber)
    {
        var sourceId = Guid.NewGuid();
        return new TeacherFinancialEventInput(
            TeacherFinancialSourceType.DirectPurchase, sourceId, studentId,
            SalesTargetType.Package, Guid.NewGuid(), 1m, 0m, 1m, 0m, 0m,
            $"test:concurrent-purchase:{sourceId}", "{}", DateTime.UtcNow,
            TeacherFinancialReviewStatus.AutoApproved,
            [new(teacherId, TeacherAllocationMode.CommissionRate, 1m, 1m, 1m, 0m,
                "Student", "test", $"Sale {saleNumber}")]);
    }

    private AppDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);
}
