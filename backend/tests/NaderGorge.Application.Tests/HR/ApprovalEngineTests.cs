using NaderGorge.Application.Features.HR.Approvals;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.HR;

public sealed class ApprovalEngineTests
{
    [Fact]
    public async Task DelegationWorksOnlyInsideWindowAndSelfApprovalIsForbidden()
    {
        await using var db = TestAppDbContextFactory.Create(); var seeded = await SeedAsync(db);
        var engine = new ApprovalEngine(db); var started = await engine.StartAsync("leave", Guid.NewGuid(), seeded.Employee.Id, default);
        Assert.True(started.Success); var instanceId = started.Data;
        db.ApprovalDelegations.Add(new ApprovalDelegation { PrincipalUserId = seeded.Manager.Id, DelegateUserId = seeded.Delegate.Id, Scope = "leave", StartsAt = DateTime.UtcNow.AddMinutes(-5), EndsAt = DateTime.UtcNow.AddMinutes(5), Reason = "coverage" }); await db.SaveChangesAsync();
        var self = await engine.DecideAsync(instanceId, seeded.EmployeeUser.Id, true, "self", 1, default);
        var delegated = await engine.DecideAsync(instanceId, seeded.Delegate.Id, true, "manager via delegate", 1, default);
        Assert.False(self.Success); Assert.Contains("SELF_APPROVAL_FORBIDDEN", self.Errors!); Assert.True(delegated.Success);
        var step = db.ApprovalStepInstances.Single(item => item.Order == 1); Assert.Equal(seeded.Manager.Id, step.OriginalApproverUserId); Assert.Equal(seeded.Delegate.Id, step.ActingUserId); Assert.NotNull(step.DelegationId);
    }

    [Fact]
    public async Task ManagerThenHrOrderIsEnforcedAndCompletesInstance()
    {
        await using var db = TestAppDbContextFactory.Create(); var seeded = await SeedAsync(db); var engine = new ApprovalEngine(db);
        var started = await engine.StartAsync("leave", Guid.NewGuid(), seeded.Employee.Id, default);
        Assert.True(started.Success); var instanceId = started.Data;
        var hrEarly = await engine.DecideAsync(instanceId, seeded.Hr.Id, true, "early", 1, default);
        var manager = await engine.DecideAsync(instanceId, seeded.Manager.Id, true, "ok", 1, default);
        var hr = await engine.DecideAsync(instanceId, seeded.Hr.Id, true, "ok", 2, default);
        Assert.False(hrEarly.Success); Assert.Contains("APPROVER_NOT_ELIGIBLE", hrEarly.Errors!); Assert.True(manager.Success); Assert.True(hr.Success);
        Assert.Equal(ApprovalInstanceState.Approved, db.ApprovalInstances.Single().State);
    }

    [Fact]
    public async Task DueStepEscalationIsIdempotentAndMovesManagerToHigherManager()
    {
        await using var db = TestAppDbContextFactory.Create(); var seeded = await SeedAsync(db); var engine = new ApprovalEngine(db);
        var started = await engine.StartAsync("leave", Guid.NewGuid(), seeded.Employee.Id, default);
        Assert.True(started.Success); var instanceId = started.Data;
        var step = db.ApprovalStepInstances.Single(item => item.ApprovalInstanceId == instanceId && item.Order == 1); step.DueAt = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        var first = await engine.EscalateDueAsync(DateTime.UtcNow, default); var replay = await engine.EscalateDueAsync(DateTime.UtcNow, default);
        Assert.Equal(1, first); Assert.Equal(0, replay); Assert.Equal(seeded.SeniorManager.Id, step.OriginalApproverUserId); Assert.Equal(1, step.EscalationLevel);
    }

    [Fact]
    public async Task PermissionBasedStepAppearsOnlyForEligibleReviewer()
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db);
        var role = new Role { Name = "HR reviewer", Type = RoleType.Staff, PermissionsJson = "[\"hr.leave.hr.review\"]" };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = seeded.Hr.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
        var engine = new ApprovalEngine(db);
        var started = await engine.StartAsync("leave", Guid.NewGuid(), seeded.Employee.Id, default);
        Assert.True(started.Success);
        Assert.True((await engine.DecideAsync(started.Data, seeded.Manager.Id, true, "manager", 1, default)).Success);
        Assert.Single(await engine.GetInboxAsync(seeded.Hr.Id, default));
        Assert.Empty(await engine.GetInboxAsync(seeded.Delegate.Id, default));
    }

    [Fact]
    public async Task DirectManagerStepWithoutManagerRejectsWorkflowStart()
    {
        await using var db = TestAppDbContextFactory.Create();
        var employeeUser = await TestAppDbContextFactory.SeedUserAsync(db, "No Manager", "01078888886");
        var employee = new EmployeeProfile { UserId = employeeUser.Id, User = employeeUser };
        employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
        var definition = new ApprovalDefinition { RequestType = "leave", Name = "Leave" };
        definition.Steps.Add(new ApprovalDefinitionStep
            { ApprovalDefinitionId = definition.Id, Order = 1, Name = "Manager", ApproverKind = ApprovalApproverKind.DirectManager, SlaMinutes = 60 });
        db.EmployeeProfiles.Add(employee);
        db.ApprovalDefinitions.Add(definition);
        await db.SaveChangesAsync();
        var started = await new ApprovalEngine(db).StartAsync("leave", Guid.NewGuid(), employee.Id, default);
        Assert.False(started.Success);
        Assert.Contains("APPROVER_NOT_FOUND", started.Errors!);
        Assert.Empty(db.ApprovalInstances);
    }

    private static async Task<(User EmployeeUser, EmployeeProfile Employee, User Manager, User SeniorManager, User Delegate, User Hr)> SeedAsync(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var employeeUser = await TestAppDbContextFactory.SeedUserAsync(db, "Employee", "01078888881"); var managerUser = await TestAppDbContextFactory.SeedUserAsync(db, "Manager", "01078888882");
        var seniorUser = await TestAppDbContextFactory.SeedUserAsync(db, "Senior", "01078888883"); var delegateUser = await TestAppDbContextFactory.SeedUserAsync(db, "Delegate", "01078888884"); var hr = await TestAppDbContextFactory.SeedUserAsync(db, "HR", "01078888885");
        var employee = new EmployeeProfile { UserId = employeeUser.Id, User = employeeUser }; employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
        var manager = new EmployeeProfile { UserId = managerUser.Id, User = managerUser }; manager.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(manager.Id);
        var senior = new EmployeeProfile { UserId = seniorUser.Id, User = seniorUser }; senior.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(senior.Id);
        var unit = new OrganizationUnit { Code = "APP", Name = "Approvals", EffectiveFrom = new DateOnly(2020, 1, 1) };
        db.EmployeeProfiles.AddRange(employee, manager, senior); db.OrganizationUnits.Add(unit);
        db.EmploymentAssignments.AddRange(
            new EmploymentAssignment { EmployeeId = employee.Id, OrganizationUnitId = unit.Id, ManagerEmployeeId = manager.Id, EffectiveFrom = new DateOnly(2020, 1, 1), ChangeReason = "test" },
            new EmploymentAssignment { EmployeeId = manager.Id, OrganizationUnitId = unit.Id, ManagerEmployeeId = senior.Id, EffectiveFrom = new DateOnly(2020, 1, 1), ChangeReason = "test" });
        var definition = new ApprovalDefinition { RequestType = "leave", Name = "Leave" };
        definition.Steps.Add(new ApprovalDefinitionStep { ApprovalDefinitionId = definition.Id, Order = 1, Name = "Manager", ApproverKind = ApprovalApproverKind.DirectManager, SlaMinutes = 60 });
        definition.Steps.Add(new ApprovalDefinitionStep { ApprovalDefinitionId = definition.Id, Order = 2, Name = "HR", ApproverKind = ApprovalApproverKind.SpecificUser, SpecificUserId = hr.Id, SlaMinutes = 60 });
        db.ApprovalDefinitions.Add(definition); await db.SaveChangesAsync(); return (employeeUser, employee, managerUser, seniorUser, delegateUser, hr);
    }
}
