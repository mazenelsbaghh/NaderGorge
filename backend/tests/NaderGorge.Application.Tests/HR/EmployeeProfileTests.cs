using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Application.Features.HR.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.HR;

public class TestAuditRepository : IAuditRepository
{
    public List<AuditLog> Logs { get; } = new();

    public Task AddAsync(AuditLog log)
    {
        Logs.Add(log);
        return Task.CompletedTask;
    }
}

public class EmployeeProfileTests
{
    [Fact]
    public async Task GetEmployees_ReturnsOnlyUsersWithExplicitEmployeeProfiles()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var employee = await TestAppDbContextFactory.SeedUserAsync(db, "Explicit Employee", "01234567884");
        var teacherOnly = await TestAppDbContextFactory.SeedUserAsync(db, "Teacher Only", "01234567885");
        var role = new Role { Id = Guid.NewGuid(), Name = "Teacher" };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = teacherOnly.Id, RoleId = role.Id });
        var profile = new EmployeeProfile { UserId = employee.Id, BasicSalary = 5000 };
        profile.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(profile.Id);
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();

        var result = await new AdminGetEmployeesQueryHandler(db)
            .Handle(new AdminGetEmployeesQuery(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(profile.Id, result.Data![0].Id);
        Assert.Equal(employee.Id, result.Data![0].UserId);
    }

    [Fact]
    public async Task GetEmployees_HidesInactiveHistoricalProfiles()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var active = await TestAppDbContextFactory.SeedUserAsync(db, "Active Worker", "01234567886");
        var inactive = await TestAppDbContextFactory.SeedUserAsync(db, "Inactive Historical Worker", "01234567887");
        inactive.IsActive = false;

        db.EmployeeProfiles.AddRange(
            new EmployeeProfile { UserId = active.Id, BasicSalary = 5000 },
            new EmployeeProfile { UserId = inactive.Id, BasicSalary = 5000 });
        await db.SaveChangesAsync();

        var result = await new AdminGetEmployeesQueryHandler(db)
            .Handle(new AdminGetEmployeesQuery(), CancellationToken.None);

        Assert.True(result.Success);
        var employee = Assert.Single(result.Data!);
        Assert.Equal(active.Id, employee.UserId);
    }

    [Fact]
    public async Task SaveProfile_CreatesProfileForNonStudentUser()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Staff", "01234567890");

        // Seed Staff Role
        var role = new Role { Id = Guid.NewGuid(), Name = "Staff" };
        var userRole = new UserRole { UserId = user.Id, RoleId = role.Id };
        db.Roles.Add(role);
        db.UserRoles.Add(userRole);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new AdminSaveEmployeeProfileCommandHandler(db, audit);

        var result = await handler.Handle(
            new AdminSaveEmployeeProfileCommand(user.Id, 6000, "08:30:00", 9, ActorUserId: user.Id),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.Data!.Id);

        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(ep => ep.UserId == user.Id);
        Assert.NotNull(profile);
        Assert.Equal(6000, profile!.BasicSalary);
        Assert.Equal(new TimeSpan(8, 30, 0), profile.StandardStartTime);
        Assert.Equal(9, profile.TargetDailyHours);

        Assert.Single(audit.Logs);
        Assert.Equal("CreateEmployeeProfile", audit.Logs[0].Action);
        Assert.Equal(user.Id, audit.Logs[0].PerformedByUserId);
    }

    [Fact]
    public async Task SaveProfile_UpdatesExistingProfileSuccessfully()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Supervisor", "01234567891");

        // Seed Role
        var role = new Role { Id = Guid.NewGuid(), Name = "Supervisor" };
        var userRole = new UserRole { UserId = user.Id, RoleId = role.Id };
        db.Roles.Add(role);
        db.UserRoles.Add(userRole);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new AdminSaveEmployeeProfileCommandHandler(db, audit);

        // First Save (Create)
        await handler.Handle(
            new AdminSaveEmployeeProfileCommand(user.Id, 7000, "09:00:00", 8, ActorUserId: user.Id),
            CancellationToken.None);

        // Second Save (Update)
        var result = await handler.Handle(
            new AdminSaveEmployeeProfileCommand(user.Id, 8500, "10:00:00", 7, ActorUserId: user.Id),
            CancellationToken.None);

        Assert.True(result.Success);

        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(ep => ep.UserId == user.Id);
        Assert.NotNull(profile);
        Assert.Equal(8500, profile!.BasicSalary);
        Assert.Equal(new TimeSpan(10, 0, 0), profile.StandardStartTime);
        Assert.Equal(7, profile.TargetDailyHours);

        Assert.Equal(2, audit.Logs.Count);
        Assert.Equal("UpdateEmployeeProfile", audit.Logs[1].Action);
        Assert.Contains("BasicSalary: 7000", audit.Logs[1].OldValues);
        Assert.Contains("BasicSalary: 8500", audit.Logs[1].NewValues);
    }

    [Fact]
    public async Task SaveProfile_ThrowsExceptionForStudentUser()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Student", "01234567892");

        // Seed Student Role ONLY
        var role = new Role { Id = Guid.NewGuid(), Name = "Student" };
        var userRole = new UserRole { UserId = user.Id, RoleId = role.Id };
        db.Roles.Add(role);
        db.UserRoles.Add(userRole);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new AdminSaveEmployeeProfileCommandHandler(db, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await handler.Handle(
                new AdminSaveEmployeeProfileCommand(user.Id, 5000, "09:00:00", 8, ActorUserId: user.Id),
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task SaveProfile_ReturnsConflictWithoutOverwritingWhenExpectedVersionIsStale()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Concurrent Staff", "01234567893");
        var role = new Role { Id = Guid.NewGuid(), Name = "Supervisor" };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            BasicSalary = 7000,
            StandardStartTime = new TimeSpan(9, 0, 0),
            TargetDailyHours = 8,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();

        var staleVersion = profile.UpdatedAt!.Value.AddTicks(-1);
        var result = await new AdminSaveEmployeeProfileCommandHandler(db, new TestAuditRepository())
            .Handle(new AdminSaveEmployeeProfileCommand(user.Id, 9000, "10:00:00", 7, staleVersion, user.Id), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("EMPLOYEE_PROFILE_CONFLICT", result.Errors!);
        var unchanged = await db.EmployeeProfiles.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(7000, unchanged.BasicSalary);
        Assert.Equal(new TimeSpan(9, 0, 0), unchanged.StandardStartTime);
        Assert.Equal(8, unchanged.TargetDailyHours);
    }
}
