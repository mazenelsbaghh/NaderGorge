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
            new AdminSaveEmployeeProfileCommand(user.Id, 6000, "08:30:00", 9),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.Data);

        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(ep => ep.UserId == user.Id);
        Assert.NotNull(profile);
        Assert.Equal(6000, profile!.BasicSalary);
        Assert.Equal(new TimeSpan(8, 30, 0), profile.StandardStartTime);
        Assert.Equal(9, profile.TargetDailyHours);

        Assert.Single(audit.Logs);
        Assert.Equal("CreateEmployeeProfile", audit.Logs[0].Action);
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
            new AdminSaveEmployeeProfileCommand(user.Id, 7000, "09:00:00", 8),
            CancellationToken.None);

        // Second Save (Update)
        var result = await handler.Handle(
            new AdminSaveEmployeeProfileCommand(user.Id, 8500, "10:00:00", 7),
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
                new AdminSaveEmployeeProfileCommand(user.Id, 5000, "09:00:00", 8),
                CancellationToken.None);
        });
    }
}
