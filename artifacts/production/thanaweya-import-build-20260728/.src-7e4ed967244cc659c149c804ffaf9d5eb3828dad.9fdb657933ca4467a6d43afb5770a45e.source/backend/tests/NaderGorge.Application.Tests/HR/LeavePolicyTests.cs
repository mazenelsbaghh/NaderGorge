using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.Leave;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.HR;

public sealed class LeavePolicyTests
{
    [Fact]
    public void WorkdayCalculator_ExcludesWeekendAndConfiguredHoliday()
    {
        var calendar = new WorkCalendar { WorkingDaysMask = 62, HolidaysJson = "[\"2026-07-22\"]" };
        Assert.Equal(4, LeaveWorkdayCalculator.Calculate(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 26), 1, calendar));
        Assert.Equal(0.5m, LeaveWorkdayCalculator.Calculate(new DateOnly(2026, 7, 21), new DateOnly(2026, 7, 21), 0.5m, calendar));
    }

    [Fact]
    public async Task SubmitReservesAndWithdrawReleasesBalanceWithoutAttendanceSession()
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db);
        var service = new LeaveRequestService(db);
        var submitted = await service.SubmitAsync(seeded.User.Id, seeded.Type.Id, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 21), 1, "annual", null, default);
        Assert.True(submitted.Success);
        var balance = await db.LeaveBalances.SingleAsync(); Assert.Equal(2, balance.Reserved); Assert.Equal(8, balance.Available);
        var withdrawn = await service.WithdrawAsync(seeded.User.Id, submitted.Data, "plans changed", default);
        Assert.True(withdrawn.Success); Assert.Equal(0, balance.Reserved); Assert.Equal(10, balance.Available);
        Assert.Empty(await db.AttendanceSessions.ToListAsync());
        Assert.Equal(2, await db.LeaveLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task FinalizeDebitsReservedBalanceExactlyOnceAndCreatesClassificationsOnly()
    {
        await using var db = TestAppDbContextFactory.Create(); var seeded = await SeedAsync(db); var service = new LeaveRequestService(db);
        var submitted = await service.SubmitAsync(seeded.User.Id, seeded.Type.Id, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 21), 1, "annual", null, default);
        var first = await service.FinalizeApprovedAsync(submitted.Data, seeded.Hr.Id, default); var replay = await service.FinalizeApprovedAsync(submitted.Data, seeded.Hr.Id, default);
        Assert.True(first.Success); Assert.True(replay.Success);
        var balance = await db.LeaveBalances.SingleAsync(); Assert.Equal(0, balance.Reserved); Assert.Equal(2, balance.Used);
        Assert.Equal(2, await db.WorkdayClassifications.CountAsync()); Assert.Empty(await db.AttendanceSessions.ToListAsync());
        Assert.Single(await db.LeaveLedgerEntries.Where(item => item.EntryType == LeaveLedgerEntryType.Debit).ToListAsync());
    }

    [Theory]
    [InlineData("2026-12-31", "2027-01-01", 1, "LEAVE_DATE_INVALID")]
    [InlineData("2026-07-20", "2026-07-21", 0.5, "HALF_DAY_RANGE_INVALID")]
    [InlineData("2026-07-20", "2026-07-20", 0.25, "LEAVE_DATE_INVALID")]
    public async Task InvalidLeaveRangesAreRejectedWithoutReservingBalance(
        string start, string end, decimal fraction, string expectedError)
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db);
        var response = await new LeaveRequestService(db).SubmitAsync(
            seeded.User.Id, seeded.Type.Id, DateOnly.Parse(start), DateOnly.Parse(end), fraction, "annual", null, default);
        Assert.False(response.Success);
        Assert.Contains(expectedError, response.Errors!);
        Assert.Equal(0, (await db.LeaveBalances.SingleAsync()).Reserved);
    }

    private static async Task<(User User, User Hr, EmployeeProfile Employee, LeaveType Type)> SeedAsync(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Leave Employee", "01077777771"); var hr = await TestAppDbContextFactory.SeedUserAsync(db, "HR", "01077777772");
        var employee = new EmployeeProfile { UserId = user.Id, User = user }; employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
        var type = new LeaveType { Code = "ANNUAL", Name = "Annual" }; var calendar = new WorkCalendar { Code = "LEAVE", Name = "Calendar", WorkingDaysMask = 62 };
        db.EmployeeProfiles.Add(employee); db.LeaveTypes.Add(type); db.WorkCalendars.Add(calendar);
        db.LeavePolicies.Add(new LeavePolicy { Name = "Annual", LeaveTypeId = type.Id, WorkCalendarId = calendar.Id, AnnualEntitlement = 10, EffectiveFrom = new DateOnly(2026, 1, 1) });
        db.LeaveBalances.Add(new LeaveBalance { EmployeeId = employee.Id, LeaveTypeId = type.Id, Year = 2026, Granted = 10 }); await db.SaveChangesAsync();
        return (user, hr, employee, type);
    }
}
