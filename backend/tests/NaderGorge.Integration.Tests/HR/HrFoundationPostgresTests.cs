using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Integration.Tests.HR;

public sealed class HrFoundationPostgresTests
{
    [Fact]
    public async Task IdempotencyAndRolloutKeys_AreEnforcedByPostgres()
    {
        await using var fixture = new PostgresHrFixture();
        await fixture.ResetAsync();
        var actorId = Guid.NewGuid();
        fixture.Db.HrIdempotencyRecords.Add(new HrIdempotencyRecord
        {
            Scope = "employee.provision", ActorUserId = actorId, Key = "same", RequestHash = "A", ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        fixture.Db.HrModuleRollouts.Add(new HrModuleRollout { Module = "people" });
        await fixture.Db.SaveChangesAsync();

        await using var duplicateDb = fixture.CreateContext();
        duplicateDb.HrIdempotencyRecords.Add(new HrIdempotencyRecord
        {
            Scope = "employee.provision", ActorUserId = actorId, Key = "same", RequestHash = "A", ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());

        await using var rolloutDb = fixture.CreateContext();
        rolloutDb.HrModuleRollouts.Add(new HrModuleRollout { Module = "people" });
        await Assert.ThrowsAsync<DbUpdateException>(() => rolloutDb.SaveChangesAsync());
    }

    [Fact]
    public async Task EmployeeHistory_PreventsProfileDeletion()
    {
        await using var fixture = new PostgresHrFixture();
        await fixture.ResetAsync();
        var user = new User { FullName = "HR Integration", PhoneNumber = "01239999991", PasswordHash = "integration" };
        var profile = new EmployeeProfile { UserId = user.Id, User = user, BasicSalary = 5000 };
        profile.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(profile.Id);
        fixture.Db.Users.Add(user);
        fixture.Db.EmployeeProfiles.Add(profile);
        fixture.Db.AttendanceLogs.Add(new AttendanceLog
        {
            EmployeeId = profile.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            ClockIn = DateTime.UtcNow
        });
        await fixture.Db.SaveChangesAsync();

        await using var deleteDb = fixture.CreateContext();
        deleteDb.EmployeeProfiles.Remove(new EmployeeProfile { Id = profile.Id, UserId = user.Id, EmployeeNumber = profile.EmployeeNumber });
        await Assert.ThrowsAsync<DbUpdateException>(() => deleteDb.SaveChangesAsync());
    }
}
