using NaderGorge.Application.Features.HR.Payroll;
using NaderGorge.Application.Features.HR.Payroll.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class HrPayrollEngineTests
{
    [Fact]
    public void FormulaValidator_RejectsArbitraryCodeAndAcceptsConstrainedExpressions()
    {
        Assert.False(PayrollCalculationEngine.IsValidExpression("System.IO.File.Delete('*')"));
        Assert.True(PayrollCalculationEngine.IsValidExpression("base"));
        Assert.True(PayrollCalculationEngine.IsValidExpression("percentage:15"));
        Assert.True(PayrollCalculationEngine.IsValidExpression("attendance.late_minutes * rate"));
    }

    [Fact]
    public void Calculation_RoundsAndExplainsEveryRuleFromSnapshotInputs()
    {
        var engine = new PayrollCalculationEngine();
        var earning = new PayComponent { Code = "BASE", Name = "Basic", Classification = PayComponentClass.Earning };
        var deduction = new PayComponent { Code = "LATE", Name = "Late", Classification = PayComponentClass.Deduction };
        var rules = new[]
        {
            new PayrollRule { PayComponent = earning, PayComponentId = earning.Id, Name = "Base", Expression = "base", Version = 2 },
            new PayrollRule { PayComponent = deduction, PayComponentId = deduction.Id, Name = "Late", Expression = "attendance.late_minutes * rate", Rate = 1.25m, Version = 1 }
        };
        var result = engine.Calculate(new PayrollCalculationInput(Guid.NewGuid(), 10000.555m, 10, 0, 0), rules);
        Assert.Equal(10000.56m, result.Gross); Assert.Equal(12.50m, result.Deductions); Assert.Equal(9988.06m, result.Net);
        Assert.All(result.Lines, line => Assert.False(string.IsNullOrWhiteSpace(line.Explanation)));
    }

    [Fact]
    public void RuleChanges_DoNotMutatePreviouslyCalculatedSnapshot()
    {
        var engine = new PayrollCalculationEngine(); var component = new PayComponent { Classification = PayComponentClass.Earning, Code = "ALLOW", Name = "Allowance" };
        var rule = new PayrollRule { PayComponent = component, PayComponentId = component.Id, Expression = "fixed:500", Version = 1 };
        var first = engine.Calculate(new PayrollCalculationInput(Guid.NewGuid(), 1000, 0, 0, 0), [rule]);
        rule.Expression = "fixed:900"; rule.Version = 2;
        var second = engine.Calculate(new PayrollCalculationInput(Guid.NewGuid(), 1000, 0, 0, 0), [rule]);
        Assert.Equal(500, first.Net); Assert.Equal(900, second.Net); Assert.Equal(1, first.Lines.Single().RuleVersion);
    }

    [Fact]
    public void RunTransitions_RequireFinanceThenGmAndClosedIsImmutable()
    {
        var run = new HrPayrollRun { Status = HrPayrollRunStatus.Prepared };
        Assert.False(PayrollRunTransitions.TryMove(run, HrPayrollRunStatus.GMApproved, Guid.NewGuid(), DateTime.UtcNow));
        Assert.True(PayrollRunTransitions.TryMove(run, HrPayrollRunStatus.FinanceReview, Guid.NewGuid(), DateTime.UtcNow));
        Assert.True(PayrollRunTransitions.TryMove(run, HrPayrollRunStatus.FinanceApproved, Guid.NewGuid(), DateTime.UtcNow));
        Assert.True(PayrollRunTransitions.TryMove(run, HrPayrollRunStatus.GMApproved, Guid.NewGuid(), DateTime.UtcNow));
        Assert.True(PayrollRunTransitions.TryMove(run, HrPayrollRunStatus.Paid, Guid.NewGuid(), DateTime.UtcNow));
        Assert.True(PayrollRunTransitions.TryMove(run, HrPayrollRunStatus.Closed, Guid.NewGuid(), DateTime.UtcNow));
        Assert.False(PayrollRunTransitions.TryMove(run, HrPayrollRunStatus.Returned, Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public async Task PrepareReplay_ReturnsSameRunAndKeepsOneEmployeeSnapshot()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Payroll Employee", "01079999991");
        var employee = new EmployeeProfile { UserId = user.Id, User = user, BasicSalary = 7654.321m, HireDate = new DateOnly(2025, 1, 1) };
        employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id); db.EmployeeProfiles.Add(employee); await db.SaveChangesAsync();
        var service = new PayrollRunService(db, new PayrollCalculationEngine()); var actor = Guid.NewGuid();
        var first = await service.PrepareAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), DateTime.UtcNow, actor, default);
        var replay = await service.PrepareAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), DateTime.UtcNow, actor, default);
        Assert.True(first.Success); Assert.Equal(first.Data, replay.Data); Assert.Single(db.HrPayrollRuns); Assert.Single(db.EmployeePayrolls);
        Assert.Equal(7654.32m, db.EmployeePayrolls.Single().Net);
    }
}
