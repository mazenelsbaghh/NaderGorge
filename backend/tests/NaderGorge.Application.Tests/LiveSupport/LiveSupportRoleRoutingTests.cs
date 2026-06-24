using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class LiveSupportRoleRoutingTests
{
    [Fact]
    public async Task AddingRoutingPermissionEnablesExistingEmployeesInThatRole()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actorId = Guid.NewGuid();
        var (role, employee) = await SeedEmployeeRoleAsync(db, []);
        var handler = new UpdateRoleCommandHandler(db);

        var response = await handler.Handle(
            new UpdateRoleCommand(role.Id, role.Name, [LiveSupportRoutingPermissions.ReceiveConversations], "all", [], actorId),
            CancellationToken.None);

        Assert.True(response.Success);
        var config = await db.LiveSupportStaffConfigs.SingleAsync(candidate => candidate.UserId == employee.UserId);
        Assert.True(config.IsEnabled);
        Assert.Equal(1, config.MaxActiveConversations);
        Assert.Equal(actorId, config.ConfiguredByUserId);
    }

    [Fact]
    public async Task SavingUnrelatedRolePermissionsPreservesManualRoutingActivation()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actorId = Guid.NewGuid();
        var (role, employee) = await SeedEmployeeRoleAsync(db, ["comments.manage"]);
        db.LiveSupportStaffConfigs.Add(new LiveSupportStaffConfig
        {
            UserId = employee.UserId,
            IsEnabled = true,
            MaxActiveConversations = 4,
            ConfiguredByUserId = actorId,
            Version = 3
        });
        await db.SaveChangesAsync();
        var handler = new UpdateRoleCommandHandler(db);

        await handler.Handle(
            new UpdateRoleCommand(role.Id, role.Name, ["comments.manage", "tasks.manage"], "all", [], actorId),
            CancellationToken.None);

        var config = await db.LiveSupportStaffConfigs.SingleAsync(candidate => candidate.UserId == employee.UserId);
        Assert.True(config.IsEnabled);
        Assert.Equal(4, config.MaxActiveConversations);
        Assert.Equal(3, config.Version);
    }

    private static async Task<(Role Role, EmployeeProfile Employee)> SeedEmployeeRoleAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        IReadOnlyCollection<string> permissions)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Support employee", $"01{Random.Shared.NextInt64(100000000, 999999999)}");
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = $"Support-{Guid.NewGuid():N}",
            Type = RoleType.Assistant,
            PermissionsJson = System.Text.Json.JsonSerializer.Serialize(permissions)
        };
        var employee = new EmployeeProfile { Id = Guid.NewGuid(), UserId = user.Id, BasicSalary = 1 };
        db.Roles.Add(role);
        db.EmployeeProfiles.Add(employee);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
        return (role, employee);
    }
}
