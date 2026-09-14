using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class CodeGroupExpiryUpdateTests
{
    [Fact]
    public async Task UpdateExpiry_SynchronizesUnusedCodesAndActiveGrantsWhenAccessExpiresWithGroup()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAdminAsync(db);
        var originalExpiry = DateTime.UtcNow.AddDays(1);
        var updatedExpiry = originalExpiry.AddHours(3);
        var group = new CodeGroup
        {
            Name = "ترم تجريبي",
            CodeType = CodeType.Term,
            CreatedByUserId = admin.Id,
            ExpiresAt = originalExpiry,
            ExpireActivatedAccess = true,
        };
        var unusedCode = new AccessCode { CodeGroup = group, ExpiresAt = originalExpiry };
        var consumedCode = new AccessCode
        {
            CodeGroup = group,
            IsConsumed = true,
            ConsumedByUserId = Guid.NewGuid(),
            ConsumedAt = DateTime.UtcNow,
            ExpiresAt = originalExpiry,
        };
        var activeGrant = new StudentAccessGrant
        {
            UserId = consumedCode.ConsumedByUserId.Value,
            GrantType = CodeType.Term,
            AccessCode = consumedCode,
            ExpiresAt = originalExpiry,
            IsActive = true,
        };
        db.AccessCodes.AddRange(unusedCode, consumedCode);
        db.StudentAccessGrants.Add(activeGrant);
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(CreateCommand(group.Id, admin.Id, updatedExpiry), default);

        Assert.True(result.Success);
        Assert.Equal(updatedExpiry, (await db.CodeGroups.SingleAsync()).ExpiresAt);
        Assert.Equal(updatedExpiry, (await db.AccessCodes.SingleAsync(code => !code.IsConsumed)).ExpiresAt);
        Assert.Equal(originalExpiry, (await db.AccessCodes.SingleAsync(code => code.IsConsumed)).ExpiresAt);
        Assert.Equal(updatedExpiry, (await db.StudentAccessGrants.SingleAsync()).ExpiresAt);
    }

    [Fact]
    public async Task UpdateExpiry_DoesNotChangeActivatedAccessWhenGroupKeepsExistingAccess()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAdminAsync(db);
        var originalExpiry = DateTime.UtcNow.AddDays(1);
        var group = new CodeGroup
        {
            Name = "ترم دائم بعد التفعيل",
            CodeType = CodeType.Term,
            CreatedByUserId = admin.Id,
            ExpiresAt = originalExpiry,
            ExpireActivatedAccess = false,
        };
        var code = new AccessCode { CodeGroup = group, IsConsumed = true };
        var grant = new StudentAccessGrant
        {
            UserId = Guid.NewGuid(),
            GrantType = CodeType.Term,
            AccessCode = code,
            ExpiresAt = null,
            IsActive = true,
        };
        db.StudentAccessGrants.Add(grant);
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(CreateCommand(group.Id, admin.Id, originalExpiry.AddDays(1)), default);

        Assert.True(result.Success);
        Assert.Null((await db.StudentAccessGrants.SingleAsync()).ExpiresAt);
    }

    [Fact]
    public async Task UpdateExpiry_RejectsPastDateWithoutChangingGroup()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAdminAsync(db);
        var originalExpiry = DateTime.UtcNow.AddDays(1);
        var group = new CodeGroup
        {
            Name = "مجموعة صالحة",
            CodeType = CodeType.Term,
            CreatedByUserId = admin.Id,
            ExpiresAt = originalExpiry,
        };
        db.CodeGroups.Add(group);
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(CreateCommand(group.Id, admin.Id, DateTime.UtcNow.AddMinutes(-1)), default);

        Assert.False(result.Success);
        Assert.Equal(originalExpiry, (await db.CodeGroups.SingleAsync()).ExpiresAt);
    }

    [Fact]
    public async Task GenerateCodes_RejectsPastExpiry()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new BulkGenerateCodesCommandHandler(db, new NoOpAuditService());

        var result = await handler.Handle(new BulkGenerateCodesCommand(
            GroupName: "مجموعة منتهية",
            CodeType: CodeType.Balance,
            Count: 1,
            CodeLength: 12,
            AdminId: Guid.NewGuid(),
            BalanceAmount: 100,
            ExpiresAt: DateTime.UtcNow.AddMinutes(-1)), default);

        Assert.False(result.Success);
        Assert.Empty(await db.CodeGroups.ToListAsync());
    }

    private static UpdateCodeGroupSettingsCommandHandler CreateHandler(AppDbContext db) =>
        new(db, new NoOpAuditService());

    private static UpdateCodeGroupSettingsCommand CreateCommand(Guid groupId, Guid adminId, DateTime expiresAt) =>
        new(groupId, adminId, null, null, expiresAt, null, null, null, CodeAccountingTiming.OnActivation);

    private static async Task<User> SeedAdminAsync(AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Expiry Admin", Guid.NewGuid().ToString("N")[..11]);
        var role = new Role { Name = "Admin", Type = RoleType.Admin, PermissionsJson = "[]" };
        db.UserRoles.Add(new UserRole { UserId = user.Id, Role = role });
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(
            string action,
            string entityType,
            Guid? entityId,
            Guid? userId,
            object? oldValues = null,
            object? newValues = null,
            string? ipAddress = null,
            string? correlationId = null) => Task.CompletedTask;
    }
}
