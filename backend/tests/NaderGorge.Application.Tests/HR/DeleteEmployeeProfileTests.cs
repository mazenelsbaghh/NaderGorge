using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.People;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.HR;

public sealed class DeleteEmployeeProfileTests
{
    [Fact]
    public async Task InactivePlaceholderEmployeeWithoutHistoryCanBeRemoved()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR administrator", "01070000000");
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Placeholder employee", "01070000001");
        user.IsActive = false;
        var employee = NewEmployee(user);
        db.EmployeeProfiles.Add(employee);
        await db.SaveChangesAsync();

        var result = await new DeleteEmployeeProfileCommandHandler(db)
            .Handle(new DeleteEmployeeProfileCommand(employee.Id, actor.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(await db.EmployeeProfiles.AnyAsync(item => item.Id == employee.Id));
        Assert.True(await db.Users.AnyAsync(item => item.Id == user.Id));
        Assert.Contains(db.AuditLogs, item => item.Action == "DeleteEmployeeProfile" && item.EntityId == employee.Id);
    }

    [Fact]
    public async Task EmployeeWithOperationalHistoryIsNotRemoved()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Historic employee", "01070000002");
        var employee = NewEmployee(user);
        db.EmployeeProfiles.Add(employee);
        db.AttendanceLogs.Add(new AttendanceLog
        {
            EmployeeId = employee.Id,
            Date = new DateOnly(2026, 9, 4),
            ClockIn = new DateTime(2026, 9, 4, 9, 0, 0),
            IpAddress = "127.0.0.1",
            UserAgent = "test"
        });
        await db.SaveChangesAsync();

        var result = await new DeleteEmployeeProfileCommandHandler(db)
            .Handle(new DeleteEmployeeProfileCommand(employee.Id, user.Id), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("EMPLOYEE_DELETE_BLOCKED", result.Errors!);
        Assert.True(await db.EmployeeProfiles.AnyAsync(item => item.Id == employee.Id));
    }

    private static EmployeeProfile NewEmployee(User user)
    {
        var employee = new EmployeeProfile { UserId = user.Id, User = user };
        employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
        return employee;
    }
}
