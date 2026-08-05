using NaderGorge.Application.Features.Admin.PlatformFinance.Teachers;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Infrastructure.Services.Finance;

namespace NaderGorge.Integration.Tests.Finance;

public sealed class TeacherPayableReconciliationTests
{
    [Fact]
    public async Task Teacher_summary_is_read_from_posted_teacher_dimensions()
    {
        var (db, _, studentId) = await FinanceTestDbFactory.CreateLedgerAsync();
        await using (db)
        {
            var teacherId = Guid.NewGuid();
            var profile = new NaderGorge.Domain.Entities.TeacherProfile { Id = teacherId, User = new() { FullName = "Teacher", PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "test" } };
            db.TeacherProfiles.Add(profile);
            await db.SaveChangesAsync();
            await new FinancialPostingService(db).PostAsync(new("Purchase", Guid.NewGuid(), "Sale", "teacher-summary-1", "sale", DateTime.UtcNow, null, [new("1000", 20m, 0m, StudentId: studentId), new("2000", 0m, 20m, StudentId: studentId, TeacherId: teacherId)]));
            var summary = await new GetTeacherFinancialSummaryQuery(db).GetAsync(teacherId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), CancellationToken.None);
            Assert.NotNull(summary);
            Assert.Equal(20m, summary!.TeacherShare);
        }
    }
}
