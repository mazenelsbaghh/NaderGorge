using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NaderGorge.API.Controllers;

namespace NaderGorge.Application.Tests.Finance;

public sealed class AdminFinancePayrollRouteTests
{
    [Theory]
    [InlineData(nameof(AdminFinanceController.GetPayroll), "payroll", typeof(HttpGetAttribute))]
    [InlineData(nameof(AdminFinanceController.GeneratePayroll), "payroll/generate", typeof(HttpPostAttribute))]
    [InlineData(nameof(AdminFinanceController.AddPayrollAdjustment), "payroll/{payrollId:guid}/adjustments", typeof(HttpPostAttribute))]
    [InlineData(nameof(AdminFinanceController.DeletePayrollAdjustment), "payroll/{payrollId:guid}/adjustments/{adjustmentId:guid}", typeof(HttpDeleteAttribute))]
    [InlineData(nameof(AdminFinanceController.ApprovePayroll), "payroll/{payrollId:guid}/approve", typeof(HttpPostAttribute))]
    public void PayrollClientRoutes_AreExposedByAdminFinanceController(
        string methodName,
        string template,
        Type attributeType)
    {
        // Regression: the admin payroll screen shipped with client calls whose
        // controller routes were absent, surfacing as a misleading network toast.
        var method = typeof(AdminFinanceController).GetMethod(methodName);

        Assert.NotNull(method);
        var route = Assert.Single(method!.GetCustomAttributes(attributeType, inherit: true)
            .Cast<HttpMethodAttribute>());
        Assert.Equal(template, route.Template);
    }
}
