using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class StaffAccountManagementTests
{
    [Fact]
    public async Task Archive_PreservesEmployeeAndRolesButHidesAccountAndRevokesSessions()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAccount(db, RoleType.Admin);
        var employee = await SeedAccount(db, RoleType.Staff);
        db.RefreshTokens.Add(new RefreshToken { UserId = employee.Id, Token = "test-session" });
        await db.SaveChangesAsync();

        var response = await new ArchiveStaffCommandHandler(db).Handle(new(employee.Id, admin.Id), default);

        Assert.True(response.Success, response.Message);
        db.ChangeTracker.Clear();
        var retained = await db.Users.Include(u => u.UserRoles).SingleAsync(u => u.Id == employee.Id);
        Assert.True(retained.IsDeleted);
        Assert.False(retained.IsActive);
        Assert.NotNull(retained.DeletedAt);
        Assert.Single(retained.UserRoles);
        Assert.True((await db.RefreshTokens.SingleAsync()).IsRevoked);
        var list = await new ListUsersQueryHandler(db).Handle(new(StaffOnly: true), default);
        Assert.DoesNotContain(list.Data!.Items, u => u.Id == employee.Id);
        var reactivate = await new UpdateUserStatusCommandHandler(db).Handle(new(employee.Id, "Active", admin.Id), default);
        Assert.False(reactivate.Success);
    }

    [Theory]
    [InlineData(RoleType.Admin, RoleType.Admin)]
    [InlineData(RoleType.Admin, RoleType.Student)]
    [InlineData(RoleType.Admin, RoleType.Teacher)]
    [InlineData(RoleType.Staff, RoleType.Staff)]
    public async Task Archive_RejectsProtectedTargetsAndNonAdminActors(RoleType actorRole, RoleType targetRole)
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await SeedAccount(db, actorRole);
        var target = await SeedAccount(db, targetRole);
        var response = await new ArchiveStaffCommandHandler(db).Handle(new(target.Id, actor.Id), default);
        Assert.False(response.Success);
        Assert.False(target.IsDeleted);
        Assert.True(target.IsActive);
    }

    [Fact]
    public async Task AdminPasswordReset_ChangesHashInvalidatesSessionsAndDoesNotAuditPassword()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAccount(db, RoleType.Admin);
        db.RefreshTokens.Add(new RefreshToken { UserId = admin.Id, Token = "test-session" });
        await db.SaveChangesAsync();
        const string password = "New-test-password!";
        var response = await new ResetAdminPasswordCommandHandler(db).Handle(new(admin.Id, password, admin.Id), default);
        Assert.True(response.Success, response.Message);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash));
        Assert.Equal(1, admin.PasswordResetVersion);
        Assert.Equal(1, admin.SecurityStampVersion);
        Assert.True((await db.RefreshTokens.SingleAsync()).IsRevoked);
        Assert.DoesNotContain(password, (await db.AuditLogs.SingleAsync()).NewValues!);
    }

    [Theory]
    [InlineData(RoleType.Staff, "Valid-password!")]
    [InlineData(RoleType.Admin, "short")]
    public async Task AdminPasswordReset_RejectsUnauthorizedActorOrWeakPassword(RoleType role, string password)
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await SeedAccount(db, role);
        var target = await SeedAccount(db, RoleType.Admin);
        var response = await new ResetAdminPasswordCommandHandler(db).Handle(new(target.Id, password, actor.Id), default);
        Assert.False(response.Success);
        Assert.Equal("original-hash", target.PasswordHash);
    }

    private static async Task<User> SeedAccount(AppDbContext db, RoleType type)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Type == type)
            ?? new Role { Name = type.ToString(), Type = type };
        var user = new User { FullName = "Test account", PhoneNumber = Guid.NewGuid().ToString(), PasswordHash = "original-hash" };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
