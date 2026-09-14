using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Application.Common.HR;
using NaderGorge.API.Controllers;
using NaderGorge.API.Extensions;
using System.Reflection;

namespace NaderGorge.Application.Tests.HR;

public class EmployeeProvisioningTests
{
    [Fact]
    public void Provisioning_IsProtectedByEmployeeManageAtApiAndHandlerBoundaries()
    {
        var command = new CreateEmployeeCommand("Employee", "01011111111", "secret12", "Employee", 0, "09:00", 8, Guid.NewGuid(), "key");
        var protectedRequest = Assert.IsAssignableFrom<IHrAuthorizedRequest>(command);
        Assert.Equal(HrPermissions.EmployeeManage, protectedRequest.RequiredPermission);
        Assert.Equal(HrAccessScope.All, protectedRequest.RequiredScope);

        var endpoint = typeof(AdminHrController).GetMethod(nameof(AdminHrController.ProvisionEmployee));
        var permission = Assert.Single(endpoint!.GetCustomAttributes<HasPermissionAttribute>());
        Assert.Equal(typeof(PermissionFilter), permission.ImplementationType);
        Assert.Contains(HrPermissions.EmployeeManage, permission.Arguments!);
    }

    [Fact]
    public async Task CreateEmployee_CreatesAccountRoleProfileAndAuditInOneSave()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR Actor", "01234567701");
        var role = new Role { Id = Guid.NewGuid(), Name = "Technical Support" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var cairoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var earliestHireDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cairoTimeZone));
        var result = await new CreateEmployeeCommandHandler(db).Handle(
            new CreateEmployeeCommand(
                "Support Employee",
                "01234567702",
                "secret12",
                role.Name,
                7000,
                "09:00:00",
                8,
                actor.Id,
                "create-support-1"),
            CancellationToken.None);
        var latestHireDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cairoTimeZone));

        Assert.True(result.Success);
        var user = await db.Users.Include(item => item.EmployeeProfile).SingleAsync(item => item.Id == result.Data!.UserId);
        Assert.NotNull(user.EmployeeProfile);
        Assert.Equal(7000, user.EmployeeProfile!.BasicSalary);
        Assert.Matches("^EMP-[A-F0-9]{32}$", user.EmployeeProfile.EmployeeNumber);
        Assert.InRange(user.EmployeeProfile.HireDate, earliestHireDate, latestHireDate);
        Assert.Equal(EmployeeEmploymentStatus.Active, user.EmployeeProfile.EmploymentStatus);
        Assert.Contains(await db.UserRoles.ToListAsync(), item => item.UserId == user.Id && item.RoleId == role.Id);
        Assert.Contains(await db.AuditLogs.ToListAsync(), item =>
            item.Action == "CreateEmployee" && item.PerformedByUserId == actor.Id && item.EntityId == user.EmployeeProfile.Id);
    }

    [Fact]
    public async Task CreateEmployee_WithShiftCreatesPublishedAssignmentInSameSave()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR Shift Actor", "01234567710");
        var role = new Role { Id = Guid.NewGuid(), Name = "Shift Employee" };
        var calendar = new WorkCalendar { Code = "PROVISION", Name = "Provision calendar" };
        var shift = new ShiftTemplate
        {
            Code = "DAY-PROVISION",
            Name = "Day shift",
            WorkCalendarId = calendar.Id,
            WorkCalendar = calendar,
            IsActive = true
        };
        db.Roles.Add(role);
        db.WorkCalendars.Add(calendar);
        db.ShiftTemplates.Add(shift);
        await db.SaveChangesAsync();

        var effectiveFrom = new DateOnly(2026, 7, 23);
        var result = await new CreateEmployeeCommandHandler(db).Handle(
            new CreateEmployeeCommand(
                "Shifted Employee",
                "01234567711",
                "secret12",
                role.Name,
                7000,
                "09:00:00",
                8,
                actor.Id,
                "create-shifted-1",
                shift.Id,
                effectiveFrom),
            CancellationToken.None);

        Assert.True(result.Success);
        var assignment = await db.ShiftAssignments.SingleAsync();
        Assert.Equal(result.Data!.EmployeeId, assignment.EmployeeId);
        Assert.Equal(shift.Id, assignment.ShiftTemplateId);
        Assert.Equal(effectiveFrom, assignment.EffectiveFrom);
        Assert.Equal(ShiftAssignmentStatus.Published, assignment.Status);
    }

    [Fact]
    public async Task CreateEmployee_WithInvalidShiftLeavesNoPartialAccountOrProfile()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR Invalid Shift Actor", "01234567712");
        var role = new Role { Id = Guid.NewGuid(), Name = "Invalid Shift Employee" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var result = await new CreateEmployeeCommandHandler(db).Handle(
            new CreateEmployeeCommand(
                "Invalid Shift Employee",
                "01234567713",
                "secret12",
                role.Name,
                7000,
                "09:00:00",
                8,
                actor.Id,
                "create-invalid-shift-1",
                Guid.NewGuid(),
                new DateOnly(2026, 7, 23)),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("SHIFT_INVALID", result.Errors!);
        Assert.False(await db.Users.AnyAsync(item => item.PhoneNumber == "01234567713"));
        Assert.Empty(await db.EmployeeProfiles.ToListAsync());
        Assert.Empty(await db.ShiftAssignments.ToListAsync());
    }

    [Fact]
    public async Task CreateEmployee_ReturnsFailureWithoutPartialRowsWhenRoleIsMissing()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR Actor", "01234567703");

        var result = await new CreateEmployeeCommandHandler(db).Handle(
            new CreateEmployeeCommand(
                "No Role Employee",
                "01234567704",
                "secret12",
                "Missing Role",
                5000,
                "09:00:00",
                8,
                actor.Id,
                "create-no-role-1"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ROLE_NOT_FOUND", result.Errors!);
        Assert.False(await db.Users.AnyAsync(item => item.PhoneNumber == "01234567704"));
        Assert.Empty(await db.EmployeeProfiles.ToListAsync());
    }

    [Fact]
    public async Task CreateEmployee_ReplayReturnsOriginalWithoutDuplicateRows()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR Replay Actor", "01234567705");
        var role = new Role { Id = Guid.NewGuid(), Name = "Support Replay" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var command = new CreateEmployeeCommand(
            "Replay Employee", "01234567706", "secret12", role.Name, 6000, "09:00:00", 8, actor.Id, "replay-key");
        var handler = new CreateEmployeeCommandHandler(db);

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(replay.Success);
        Assert.Equal(first.Data!.EmployeeId, replay.Data!.EmployeeId);
        Assert.Single(await db.EmployeeProfiles.ToListAsync());
        Assert.Single(await db.Users.Where(item => item.PhoneNumber == "01234567706").ToListAsync());
    }

    [Fact]
    public async Task CreateEmployee_AssignsDifferentStableEmployeeNumbers()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR Number Actor", "01234567707");
        var role = new Role { Id = Guid.NewGuid(), Name = "Support Numbered" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var handler = new CreateEmployeeCommandHandler(db);

        var first = await handler.Handle(
            new CreateEmployeeCommand("First Employee", "01234567708", "secret12", role.Name, 6000, "09:00:00", 8, actor.Id, "number-1"),
            CancellationToken.None);
        var second = await handler.Handle(
            new CreateEmployeeCommand("Second Employee", "01234567709", "secret12", role.Name, 6000, "09:00:00", 8, actor.Id, "number-2"),
            CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.NotEqual(first.Data!.EmployeeNumber, second.Data!.EmployeeNumber);
        Assert.Equal(2, await db.EmployeeProfiles.Select(item => item.EmployeeNumber).Distinct().CountAsync());
    }
}
