using System.Text.Json;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.HR;

public sealed class HrAuthorizationBehaviorTests
{
    [Fact]
    public async Task SelfScope_AllowsOnlyActorEmployee()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await SeedActorAsync(db, HrPermissions.AttendanceSelf + "@self");
        var other = await TestAppDbContextFactory.SeedUserAsync(db, "Other", "01011111112");
        var otherProfile = NewProfile(other);
        db.EmployeeProfiles.Add(otherProfile);
        await db.SaveChangesAsync();
        var service = new HrAuthorizationService(db, new Context(actor.User.Id));

        await service.EnsureAuthorizedAsync(new ProtectedRequest(
            HrPermissions.AttendanceSelf, HrAccessScope.Self, null, actor.User.Id), default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EnsureAuthorizedAsync(new ProtectedRequest(
            HrPermissions.AttendanceSelf, HrAccessScope.Self, otherProfile.Id, null), default));
    }

    [Fact]
    public async Task DirectTeamScope_AllowsReportAndRejectsUnrelatedEmployee()
    {
        await using var db = TestAppDbContextFactory.Create();
        var manager = await SeedActorAsync(db, HrPermissions.EmployeeRead + "@direct-team");
        var reportUser = await TestAppDbContextFactory.SeedUserAsync(db, "Report", "01011111113");
        var outsiderUser = await TestAppDbContextFactory.SeedUserAsync(db, "Outsider", "01011111114");
        var report = NewProfile(reportUser);
        var outsider = NewProfile(outsiderUser);
        var unit = new OrganizationUnit { Code = "AUTH", Name = "Auth", EffectiveFrom = new DateOnly(2020, 1, 1) };
        db.EmployeeProfiles.AddRange(report, outsider);
        db.OrganizationUnits.Add(unit);
        db.EmploymentAssignments.Add(new EmploymentAssignment
        {
            EmployeeId = report.Id, ManagerEmployeeId = manager.Profile.Id, OrganizationUnitId = unit.Id,
            EffectiveFrom = new DateOnly(2020, 1, 1), ChangeReason = "test"
        });
        await db.SaveChangesAsync();
        var service = new HrAuthorizationService(db, new Context(manager.User.Id));

        await service.EnsureAuthorizedAsync(new ProtectedRequest(
            HrPermissions.EmployeeRead, HrAccessScope.DirectTeam, report.Id, null), default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EnsureAuthorizedAsync(new ProtectedRequest(
            HrPermissions.EmployeeRead, HrAccessScope.DirectTeam, outsider.Id, null), default));
    }

    [Fact]
    public async Task MissingPermission_IsDeniedBeforeHandler()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await SeedActorAsync(db, HrPermissions.EmployeeRead + "@all");
        var service = new HrAuthorizationService(db, new Context(actor.User.Id));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EnsureAuthorizedAsync(new ProtectedRequest(
            HrPermissions.PayrollView, HrAccessScope.All, null, null), default));
    }

    private static async Task<(User User, EmployeeProfile Profile)> SeedActorAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db, string permission)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Actor", $"010{Random.Shared.Next(10000000, 99999999)}");
        var role = new Role { Name = "Manager", PermissionsJson = JsonSerializer.Serialize(new[] { permission }) };
        var profile = NewProfile(user);
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();
        return (user, profile);
    }

    private static EmployeeProfile NewProfile(User user)
    {
        var profile = new EmployeeProfile { UserId = user.Id, User = user, BasicSalary = 0 };
        profile.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(profile.Id);
        return profile;
    }

    private sealed record ProtectedRequest(
        string RequiredPermission,
        HrAccessScope RequiredScope,
        Guid? ResourceEmployeeId,
        Guid? ResourceUserId) : IHrAuthorizedRequest;

    private sealed class Context(Guid actorId) : IHrRequestContext
    {
        public Guid? ActorUserId => actorId;
        public string CorrelationId => "auth-test";
    }
}
