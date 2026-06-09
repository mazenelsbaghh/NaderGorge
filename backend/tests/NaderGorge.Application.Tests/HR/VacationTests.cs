using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Application.Features.HR.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.HR;

public class VacationTests
{
    private class TestAuditRepository : IAuditRepository
    {
        public List<AuditLog> Logs { get; } = new();

        public Task AddAsync(AuditLog log)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SubmitVacation_SavesRequestSuccessfully_WhenNoOverlap()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Staff", "01234567901");

        // Seed profile
        var profile = new EmployeeProfile { UserId = user.Id, BasicSalary = 4000, StandardStartTime = new TimeSpan(9, 0, 0), TargetDailyHours = 8 };
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();

        var handler = new SubmitVacationCommandHandler(db);
        var result = await handler.Handle(
            new SubmitVacationCommand(user.Id, new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 12), "Family trip"),
            CancellationToken.None);

        Assert.True(result.Success);

        var vacation = await db.EmployeeVacations.FirstOrDefaultAsync(ev => ev.Id == result.Data);
        Assert.NotNull(vacation);
        Assert.Equal(VacationStatus.Pending, vacation!.Status);
        Assert.Equal(new DateOnly(2026, 6, 10), vacation.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 12), vacation.EndDate);
        Assert.Equal("Family trip", vacation.Reason);
    }

    [Fact]
    public async Task SubmitVacation_ThrowsInvalidOperationException_WhenOverlappingRequestExists()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Staff 2", "01234567902");

        // Seed profile
        var profile = new EmployeeProfile { UserId = user.Id, BasicSalary = 4000, StandardStartTime = new TimeSpan(9, 0, 0), TargetDailyHours = 8 };
        db.EmployeeProfiles.Add(profile);

        // Seed existing pending vacation for June 10-15
        var existingVacation = new EmployeeVacation
        {
            EmployeeId = profile.Id,
            StartDate = new DateOnly(2026, 6, 10),
            EndDate = new DateOnly(2026, 6, 15),
            Reason = "Sick leave",
            Status = VacationStatus.Pending
        };
        db.EmployeeVacations.Add(existingVacation);
        await db.SaveChangesAsync();

        var handler = new SubmitVacationCommandHandler(db);

        // Attempting to request June 12-14 (Overlaps entirely inside the range)
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await handler.Handle(
                new SubmitVacationCommand(user.Id, new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 14), "Overlap check"),
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task ApproveVacation_SetsStatusApprovedAndSeedsLeaveLogs()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Staff 3", "01234567903");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "HR Admin", "01234567904");

        // Seed profile
        var profile = new EmployeeProfile { UserId = user.Id, BasicSalary = 4000, StandardStartTime = new TimeSpan(9, 0, 0), TargetDailyHours = 8 };
        db.EmployeeProfiles.Add(profile);

        // Seed vacation request for June 20-21 (2 days)
        var vacation = new EmployeeVacation
        {
            EmployeeId = profile.Id,
            StartDate = new DateOnly(2026, 6, 20),
            EndDate = new DateOnly(2026, 6, 21),
            Reason = "Rest days",
            Status = VacationStatus.Pending
        };
        db.EmployeeVacations.Add(vacation);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new AdminApproveVacationCommandHandler(db, audit);
        var result = await handler.Handle(new AdminApproveVacationCommand(vacation.Id, admin.Id), CancellationToken.None);

        Assert.True(result.Success);

        // Check vacation status
        var updatedVacation = await db.EmployeeVacations.FirstOrDefaultAsync(ev => ev.Id == vacation.Id);
        Assert.NotNull(updatedVacation);
        Assert.Equal(VacationStatus.Approved, updatedVacation!.Status);
        Assert.Equal(admin.Id, updatedVacation.HandledBy);
        Assert.NotNull(updatedVacation.HandledAt);

        // Check generated leave logs (2 days)
        var leaveLogs = await db.AttendanceLogs
            .Where(al => al.EmployeeId == profile.Id && al.Status == AttendanceStatus.Leave)
            .ToListAsync();
        Assert.Equal(2, leaveLogs.Count);
        Assert.Contains(leaveLogs, al => al.Date == new DateOnly(2026, 6, 20));
        Assert.Contains(leaveLogs, al => al.Date == new DateOnly(2026, 6, 21));

        // Check Audit Log
        Assert.Single(audit.Logs);
        Assert.Equal("ApproveVacation", audit.Logs[0].Action);
        Assert.Equal(admin.Id, audit.Logs[0].PerformedByUserId);
    }

    [Fact]
    public async Task RejectVacation_SetsStatusRejectedAndDoesNotSeedLogs()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Staff 4", "01234567905");
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "HR Admin 2", "01234567906");

        // Seed profile
        var profile = new EmployeeProfile { UserId = user.Id, BasicSalary = 4000, StandardStartTime = new TimeSpan(9, 0, 0), TargetDailyHours = 8 };
        db.EmployeeProfiles.Add(profile);

        // Seed vacation request
        var vacation = new EmployeeVacation
        {
            EmployeeId = profile.Id,
            StartDate = new DateOnly(2026, 6, 25),
            EndDate = new DateOnly(2026, 6, 26),
            Reason = "Rejected request test",
            Status = VacationStatus.Pending
        };
        db.EmployeeVacations.Add(vacation);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new AdminRejectVacationCommandHandler(db, audit);
        var result = await handler.Handle(new AdminRejectVacationCommand(vacation.Id, admin.Id), CancellationToken.None);

        Assert.True(result.Success);

        var updatedVacation = await db.EmployeeVacations.FirstOrDefaultAsync(ev => ev.Id == vacation.Id);
        Assert.NotNull(updatedVacation);
        Assert.Equal(VacationStatus.Rejected, updatedVacation!.Status);
        Assert.Equal(admin.Id, updatedVacation.HandledBy);

        // Verify NO leave logs were seeded
        var leaveLogsCount = await db.AttendanceLogs
            .CountAsync(al => al.EmployeeId == profile.Id && al.Status == AttendanceStatus.Leave);
        Assert.Equal(0, leaveLogsCount);

        // Check Audit Log
        Assert.Single(audit.Logs);
        Assert.Equal("RejectVacation", audit.Logs[0].Action);
    }
}
