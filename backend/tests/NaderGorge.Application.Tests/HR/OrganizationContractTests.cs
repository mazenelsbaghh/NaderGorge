using NaderGorge.Application.Features.HR.Organization;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Entities;
using NaderGorge.Application.Features.HR.People;
using Microsoft.EntityFrameworkCore;

namespace NaderGorge.Application.Tests.HR;

public sealed class OrganizationContractTests
{
    [Fact]
    public async Task AssignmentHandler_RejectsOverlapAndExitDisablesLoginWithoutDeletingProfile()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR Actor", "01239999981");
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Employee", "01239999982");
        var profile = new EmployeeProfile { UserId = user.Id, User = user, BasicSalary = 5000 };
        profile.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(profile.Id);
        var unit = new OrganizationUnit { Code = "OPS", Name = "Operations", EffectiveFrom = new DateOnly(2026, 1, 1) };
        db.EmployeeProfiles.Add(profile);
        db.OrganizationUnits.Add(unit);
        await db.SaveChangesAsync();
        var handler = new CreateEmploymentAssignmentCommandHandler(db);

        var first = await handler.Handle(new CreateEmploymentAssignmentCommand(
            profile.Id, unit.Id, null, null, null, null, null,
            new DateOnly(2026, 1, 1), null, "initial", actor.Id), CancellationToken.None);
        var overlap = await handler.Handle(new CreateEmploymentAssignmentCommand(
            profile.Id, unit.Id, null, null, null, null, null,
            new DateOnly(2026, 6, 1), null, "overlap", actor.Id), CancellationToken.None);

        Assert.True(first.Success);
        Assert.False(overlap.Success);
        Assert.Contains("ASSIGNMENT_PERIOD_OVERLAP", overlap.Errors!);

        var exit = await new CompleteEmployeeExitCommandHandler(db).Handle(
            new CompleteEmployeeExitCommand(profile.Id, new DateOnly(2026, 12, 31), actor.Id), CancellationToken.None);
        db.ChangeTracker.Clear();
        var retained = await db.EmployeeProfiles.Include(item => item.User).SingleAsync(item => item.Id == profile.Id);
        Assert.True(exit.Success);
        Assert.Equal(EmployeeEmploymentStatus.Terminated, retained.EmploymentStatus);
        Assert.False(retained.User!.IsActive);
    }

    [Fact]
    public async Task ScopeResolver_ReturnsUnitAndAllDescendantsOnly()
    {
        await using var db = TestAppDbContextFactory.Create();
        var root = new OrganizationUnit { Code = "ROOT", Name = "Root", EffectiveFrom = new DateOnly(2026, 1, 1) };
        var child = new OrganizationUnit { Code = "DEP", Name = "Department", ParentId = root.Id, EffectiveFrom = root.EffectiveFrom };
        var team = new OrganizationUnit { Code = "TEAM", Name = "Team", ParentId = child.Id, EffectiveFrom = root.EffectiveFrom };
        var unrelated = new OrganizationUnit { Code = "OTHER", Name = "Other", EffectiveFrom = root.EffectiveFrom };
        db.OrganizationUnits.AddRange(root, child, team, unrelated);
        await db.SaveChangesAsync();

        var scope = await new HrOrganizationScopeResolver(db).ResolveUnitScopeAsync(root.Id, CancellationToken.None);

        Assert.Equal(3, scope.Count);
        Assert.Contains(root.Id, scope);
        Assert.Contains(child.Id, scope);
        Assert.Contains(team.Id, scope);
        Assert.DoesNotContain(unrelated.Id, scope);
    }

    [Fact]
    public void ContractTransitions_AllowActivationAndRejectClosedReactivation()
    {
        Assert.True(HrOrganizationRules.CanTransitionContract(EmploymentContractStatus.Draft, EmploymentContractStatus.Active));
        Assert.True(HrOrganizationRules.CanTransitionContract(EmploymentContractStatus.Active, EmploymentContractStatus.Terminated));
        Assert.False(HrOrganizationRules.CanTransitionContract(EmploymentContractStatus.Terminated, EmploymentContractStatus.Active));
    }

    [Fact]
    public void ValidateManager_RejectsSelfManagement()
    {
        var employeeId = Guid.NewGuid();

        var error = HrOrganizationRules.ValidateManager(employeeId, employeeId);

        Assert.Equal("EMPLOYEE_SELF_MANAGER", error);
    }

    [Fact]
    public void EffectivePeriods_RejectOverlapButAllowAdjacentTransfer()
    {
        Assert.True(HrOrganizationRules.PeriodsOverlap(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30),
            new DateOnly(2026, 6, 1), null));
        Assert.False(HrOrganizationRules.PeriodsOverlap(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30),
            new DateOnly(2026, 7, 1), null));
    }

    [Fact]
    public void OrganizationParent_RejectsDirectAndIndirectCycles()
    {
        var root = Guid.NewGuid();
        var department = Guid.NewGuid();
        var team = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?>
        {
            [root] = null,
            [department] = root,
            [team] = department
        };

        Assert.Equal("ORGANIZATION_CYCLE", HrOrganizationRules.ValidateParent(root, team, parents));
        Assert.Equal("ORGANIZATION_CYCLE", HrOrganizationRules.ValidateParent(team, team, parents));
        Assert.Null(HrOrganizationRules.ValidateParent(team, root, parents));
    }
}
