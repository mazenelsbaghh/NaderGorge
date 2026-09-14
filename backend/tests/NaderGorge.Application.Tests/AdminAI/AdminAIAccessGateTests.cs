using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIAccessGateTests
{
    [Fact]
    public async Task ActiveAdmin_WithMatchingSecurityVersion_IsAccepted()
    {
        await using var db = CreateDb();
        var user = await SeedAsync(db, RoleType.Admin, active: true, deleted: false, securityVersion: 7);
        var result = await new AdminAIAccessGate(db).RequireCurrentAdminAsync(user.Id, 7, default);
        Assert.Equal(user.Id, result.UserId); Assert.Equal(7, result.SecurityVersion);
    }

    [Theory]
    [InlineData(RoleType.Student, true, false, 0)]
    [InlineData(RoleType.Admin, false, false, 0)]
    [InlineData(RoleType.Admin, true, true, 0)]
    [InlineData(RoleType.Admin, true, false, 99)]
    public async Task InvalidOrChangedAdminState_IsRejected(RoleType role, bool active, bool deleted, int expectedVersion)
    {
        await using var db = CreateDb();
        var user = await SeedAsync(db, role, active, deleted, securityVersion: 3);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new AdminAIAccessGate(db).RequireCurrentAdminAsync(user.Id, expectedVersion, default));
    }

    [Fact]
    public async Task RemovedAdminRole_IsRejectedImmediatelyFromDatabase()
    {
        await using var db = CreateDb();
        var user = await SeedAsync(db, RoleType.Admin, true, false, 1);
        db.UserRoles.RemoveRange(db.UserRoles.Where(link => link.UserId == user.Id));
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new AdminAIAccessGate(db).RequireCurrentAdminAsync(user.Id, null, default));
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"admin-ai-access-{Guid.NewGuid()}").Options);

    private static async Task<User> SeedAsync(AppDbContext db, RoleType roleType, bool active, bool deleted, int securityVersion)
    {
        var user = new User { FullName = "Access Test", PhoneNumber = Guid.NewGuid().ToString("N"), PasswordHash = "not-used", IsActive = active, IsDeleted = deleted, SecurityStampVersion = securityVersion };
        var role = new Role { Name = roleType.ToString(), Type = roleType };
        db.AddRange(user, role, new UserRole { User = user, Role = role });
        await db.SaveChangesAsync();
        return user;
    }
}
