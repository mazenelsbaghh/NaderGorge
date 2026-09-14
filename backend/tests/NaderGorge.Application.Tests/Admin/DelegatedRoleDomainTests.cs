using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Admin;

public sealed class DelegatedRoleDomainTests
{
    [Theory]
    [InlineData("admin")]
    [InlineData("ASSISTANT")]
    public async Task CreateRoleStoresSupportedDomain(string requestedDomain)
    {
        await using var db = TestAppDbContextFactory.Create();
        var response = await new CreateRoleCommandHandler(db).Handle(
            new CreateRoleCommand("Delegated role", [], requestedDomain, []),
            CancellationToken.None);

        Assert.True(response.Success);
        var role = await db.Roles.SingleAsync();
        Assert.Equal(requestedDomain.ToLowerInvariant(), role.AllowedDomain);
        Assert.Equal(RoleType.Assistant, role.Type);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("teacher")]
    [InlineData("student")]
    [InlineData("unknown")]
    public async Task CreateRoleRejectsUnsupportedDomain(string requestedDomain)
    {
        await using var db = TestAppDbContextFactory.Create();
        var response = await new CreateRoleCommandHandler(db).Handle(
            new CreateRoleCommand("Delegated role", [], requestedDomain, []),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("ROLE_DOMAIN_INVALID", response.Errors ?? []);
        Assert.Empty(await db.Roles.ToListAsync());
    }

    [Fact]
    public async Task UpdateRoleRejectsUnsupportedDomainWithoutChangingRole()
    {
        await using var db = TestAppDbContextFactory.Create();
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = "Delegated role",
            Type = RoleType.Assistant,
            AllowedDomain = "assistant"
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var response = await new UpdateRoleCommandHandler(db).Handle(
            new UpdateRoleCommand(role.Id, role.Name, [], "teacher", [], Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("assistant", (await db.Roles.SingleAsync()).AllowedDomain);
    }
}
