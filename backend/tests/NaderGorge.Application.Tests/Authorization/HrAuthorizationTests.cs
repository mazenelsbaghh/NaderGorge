using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Common.Configuration;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Tests.Authorization;

public sealed class HrAuthorizationTests
{
    [Theory]
    [InlineData(HrPermissions.AttendanceSelf)]
    [InlineData(HrPermissions.PayrollSelf)]
    public async Task HttpPermissionFilter_AllowsProvisionedEmployeeToAccessSelfServiceWithoutLegacyClaim(string permission)
    {
        await using var db = TestAppDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = userId, EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(Guid.NewGuid()) });
        await db.SaveChangesAsync();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
        };
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);

        await new PermissionFilter(permission, db).OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task HttpPermissionFilter_DoesNotGrantAdministrativePayrollViewToProvisionedEmployee()
    {
        await using var db = TestAppDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.EmployeeProfiles.Add(new EmployeeProfile { UserId = userId, EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(Guid.NewGuid()) });
        await db.SaveChangesAsync();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
        };
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);

        await new PermissionFilter(HrPermissions.PayrollView, db).OnAuthorizationAsync(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Theory]
    [InlineData("employee", HrPermissions.LeaveSelf, true)]
    [InlineData("employee", HrPermissions.LeaveManage, false)]
    [InlineData("support-employee", HrPermissions.LeaveSelf, true)]
    [InlineData("support-assistant", HrPermissions.LeaveSelf, true)]
    [InlineData("manager", HrPermissions.LeaveTeamReview, true)]
    [InlineData("hr", HrPermissions.EmployeeManage, true)]
    [InlineData("finance", HrPermissions.PayrollReview, true)]
    [InlineData("finance", HrPermissions.PayrollFinalApprove, false)]
    [InlineData("gm", HrPermissions.PayrollFinalApprove, true)]
    [InlineData("teacher", HrPermissions.EmployeeRead, false)]
    [InlineData("student", HrPermissions.EmployeeRead, false)]
    [InlineData("outsider", HrPermissions.EmployeeRead, false)]
    public async Task HttpPermissionFilter_EnforcesRoleMatrix(string role, string requestedPermission, bool allowed)
    {
        await using var db = TestAppDbContextFactory.Create();
        var grantedPermission = role switch
        {
            "employee" or "support-employee" or "support-assistant" => HrPermissions.LeaveSelf,
            "manager" => HrPermissions.LeaveTeamReview,
            "hr" => HrPermissions.EmployeeManage,
            "finance" => HrPermissions.PayrollReview,
            "gm" => HrPermissions.PayrollFinalApprove,
            _ => "none"
        };
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("permission", grantedPermission)
        };
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var context = new AuthorizationFilterContext(actionContext, []);

        await new PermissionFilter(requestedPermission, db).OnAuthorizationAsync(context);

        Assert.Equal(allowed, context.Result is null);
        if (!allowed) Assert.IsType<ForbidResult>(context.Result);
    }

    [Theory]
    [InlineData(PlatformFinancePermissions.DashboardView, true)]
    [InlineData(PlatformFinancePermissions.HistoricalMigration, true)]
    [InlineData(HrPermissions.PayrollReview, false)]
    public async Task HttpPermissionFilter_TreatsLegacyFinanceManageAsPlatformFinanceUmbrellaOnly(
        string requestedPermission,
        bool allowed)
    {
        await using var db = TestAppDbContextFactory.Create();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("permission", "finance.manage")
        };
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        var context = new AuthorizationFilterContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            []);

        await new PermissionFilter(requestedPermission, db).OnAuthorizationAsync(context);

        Assert.Equal(allowed, context.Result is null);
        if (!allowed) Assert.IsType<ForbidResult>(context.Result);
    }
}
