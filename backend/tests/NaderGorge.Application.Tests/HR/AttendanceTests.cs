using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Application.Features.HR.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.HR;

public class AttendanceTests
{
    [Fact]
    public async Task ClockIn_GuaranteesLateStatus_WhenStartTimeIsMidnight()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee", "01234567895");

        // Seed Employee Profile with 00:00:00 start time (always late)
        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            BasicSalary = 5000,
            StandardStartTime = new TimeSpan(0, 0, 0),
            TargetDailyHours = 8
        };
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();

        var handler = new ClockInCommandHandler(db);
        var result = await handler.Handle(
            new ClockInCommand(user.Id, "127.0.0.1", "TestAgent"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.Data);

        var log = await db.AttendanceLogs.FirstOrDefaultAsync(al => al.Id == result.Data);
        Assert.NotNull(log);
        Assert.Equal(AttendanceStatus.Late, log!.Status);
        Assert.True(log.LateMinutes > 0);
        Assert.Null(log.ClockOut);
        Assert.Equal("127.0.0.1", log.IpAddress);
        Assert.Equal("TestAgent", log.UserAgent);
    }

    [Fact]
    public async Task ClockIn_GuaranteesPresentStatus_WhenStartTimeIsNearMidnightEnd()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee 2", "01234567896");

        // Seed Employee Profile with 23:59:59 start time (almost always on time/present)
        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            BasicSalary = 5000,
            StandardStartTime = new TimeSpan(23, 59, 59),
            TargetDailyHours = 8
        };
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();

        var handler = new ClockInCommandHandler(db);
        var result = await handler.Handle(
            new ClockInCommand(user.Id, "127.0.0.1", "TestAgent"),
            CancellationToken.None);

        Assert.True(result.Success);

        var log = await db.AttendanceLogs.FirstOrDefaultAsync(al => al.Id == result.Data);
        Assert.NotNull(log);
        Assert.Equal(AttendanceStatus.Present, log!.Status);
        Assert.Equal(0, log.LateMinutes);
    }

    [Fact]
    public async Task ClockIn_ThrowsKeyNotFoundException_WhenProfileDoesNotExist()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee 3", "01234567897");

        var handler = new ClockInCommandHandler(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            await handler.Handle(
                new ClockInCommand(user.Id, "127.0.0.1", "TestAgent"),
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task ClockIn_ThrowsInvalidOperationException_WhenActiveSessionAlreadyExists()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee 4", "01234567898");

        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            BasicSalary = 5000,
            StandardStartTime = new TimeSpan(9, 0, 0),
            TargetDailyHours = 8
        };
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();

        var handler = new ClockInCommandHandler(db);

        // First Clock In
        await handler.Handle(
            new ClockInCommand(user.Id, "127.0.0.1", "TestAgent"),
            CancellationToken.None);

        // Second Clock In (Without Clock Out)
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await handler.Handle(
                new ClockInCommand(user.Id, "127.0.0.1", "TestAgent"),
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task ClockOut_UpdatesSessionSuccessfully()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee 5", "01234567899");

        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            BasicSalary = 5000,
            StandardStartTime = new TimeSpan(9, 0, 0),
            TargetDailyHours = 8
        };
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();

        var clockInHandler = new ClockInCommandHandler(db);
        var clockOutHandler = new ClockOutCommandHandler(db);

        // Clock In
        var inResult = await clockInHandler.Handle(
            new ClockInCommand(user.Id, "127.0.0.1", "TestAgent"),
            CancellationToken.None);

        // Clock Out
        var outResult = await clockOutHandler.Handle(
            new ClockOutCommand(user.Id),
            CancellationToken.None);

        Assert.True(outResult.Success);
        Assert.Equal(inResult.Data, outResult.Data);

        var log = await db.AttendanceLogs.FirstOrDefaultAsync(al => al.Id == inResult.Data);
        Assert.NotNull(log);
        Assert.NotNull(log!.ClockOut);
        Assert.True(log.ClockOut >= log.ClockIn);
    }

    [Fact]
    public async Task ClockOut_ThrowsInvalidOperationException_WhenNoActiveSession()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee 6", "01234567900");

        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            BasicSalary = 5000,
            StandardStartTime = new TimeSpan(9, 0, 0),
            TargetDailyHours = 8
        };
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();

        var handler = new ClockOutCommandHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await handler.Handle(
                new ClockOutCommand(user.Id),
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task ClockIn_ThrowsInvalidOperationException_WhenCheckingInTwiceOnSameCalendarDay()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee 7", "01234567901");

        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            BasicSalary = 5000,
            StandardStartTime = new TimeSpan(9, 0, 0),
            TargetDailyHours = 8
        };
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();

        var clockInHandler = new ClockInCommandHandler(db);
        var clockOutHandler = new ClockOutCommandHandler(db);

        // First Clock In
        await clockInHandler.Handle(
            new ClockInCommand(user.Id, "127.0.0.1", "TestAgent"),
            CancellationToken.None);

        // Clock Out to complete the first session
        await clockOutHandler.Handle(
            new ClockOutCommand(user.Id),
            CancellationToken.None);

        // Attempt Second Clock In on the same day
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await clockInHandler.Handle(
                new ClockInCommand(user.Id, "127.0.0.1", "TestAgent"),
                CancellationToken.None);
        });

        Assert.Equal("لقد قمت بتسجيل الحضور بالفعل اليوم.", ex.Message);
    }

    [Fact]
    public async Task GetMyAttendance_ReturnsTargetDailyHours()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee 8", "01234567902");

        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            BasicSalary = 5500,
            StandardStartTime = new TimeSpan(8, 30, 0),
            TargetDailyHours = 7
        };
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();

        var queryHandler = new GetMyAttendanceQueryHandler(db);
        var result = await queryHandler.Handle(new GetMyAttendanceQuery(user.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.HasProfile);
        Assert.Equal(7, result.Data.TargetDailyHours);
    }
}
