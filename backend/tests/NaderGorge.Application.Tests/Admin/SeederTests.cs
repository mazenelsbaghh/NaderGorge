using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Admin;

public sealed class SeederTests
{
    [Fact]
    public async Task Seed_preserves_staff_permissions_and_domain_selected_by_admin()
    {
        await using var db = TestAppDbContextFactory.Create();
        db.Roles.Add(new Role
        {
            Id = Guid.NewGuid(),
            Name = "Staff",
            Type = RoleType.Staff,
            AllowedDomain = "admin",
            PermissionsJson = "[\"content.manage\",\"codes.manage\"]"
        });
        await db.SaveChangesAsync();

        await Seeder.SeedAsync(db);

        var staff = await db.Roles.SingleAsync(role => role.Name == "Staff");
        Assert.Equal("admin", staff.AllowedDomain);
        Assert.Equal("[\"content.manage\",\"codes.manage\"]", staff.PermissionsJson);
    }
}
