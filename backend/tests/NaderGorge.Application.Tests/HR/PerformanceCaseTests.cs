using NaderGorge.Application.Features.HR.Performance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.HR;

public sealed class PerformanceCaseTests
{
    [Fact]
    public async Task WeightedReviewRequiresOneHundredPercentAndSupportsAppeal()
    {
        await using var db = TestAppDbContextFactory.Create(); var seed = await SeedAsync(db); var service = new PerformanceCaseService(db);
        var cycle = new PerformanceCycle { Name = "2026", StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2026, 12, 31) };
        cycle.Goals.Add(new PerformanceGoal { PerformanceCycleId = cycle.Id, Name = "Quality", Weight = 60 }); cycle.Goals.Add(new PerformanceGoal { PerformanceCycleId = cycle.Id, Name = "Delivery", Weight = 40 }); db.PerformanceCycles.Add(cycle); await db.SaveChangesAsync();
        Assert.True((await service.ActivateCycleAsync(cycle.Id, default)).Success);
        var scores = new Dictionary<Guid, decimal> { [cycle.Goals.First().Id] = 90, [cycle.Goals.Last().Id] = 80 };
        var review = await service.PublishReviewAsync(cycle.Id, seed.Employee.Id, seed.Manager.Id, scores, default); Assert.True(review.Success);
        Assert.Equal(86, db.PerformanceReviews.Single().WeightedScore);
        Assert.True((await service.AppealAsync(review.Data, seed.User.Id, "أطلب مراجعة الهدف", 1, default)).Success);
        Assert.Equal(PerformanceReviewState.Appealed, db.PerformanceReviews.Single().State);
    }

    [Fact]
    public async Task ConfidentialCaseRequiresPermissionAndPenaltyLinksToPayrollOnce()
    {
        await using var db = TestAppDbContextFactory.Create(); var seed = await SeedAsync(db); var service = new PerformanceCaseService(db);
        var caseId = await service.OpenCaseAsync(seed.Employee.Id, seed.Manager.Id, "Confidential", "facts", true, default);
        Assert.False(await service.CanViewCaseAsync(caseId.Data, seed.User.Id, false, default)); Assert.True(await service.CanViewCaseAsync(caseId.Data, seed.Manager.Id, true, default));
        var action = await service.DecideCaseAsync(caseId.Data, DisciplinaryActionType.FinancialPenalty, 100, "approved penalty", seed.Manager.Id, 1, default);
        var run = new HrPayrollRun { RunNumber = "CASE-PAY", PeriodStart = new DateOnly(2026, 8, 1), PeriodEnd = new DateOnly(2026, 8, 31), Status = HrPayrollRunStatus.Prepared };
        var payroll = new EmployeePayroll { PayrollRunId = run.Id, PayrollRun = run, EmployeeId = seed.Employee.Id, EmployeeNumberSnapshot = seed.Employee.EmployeeNumber, EmployeeNameSnapshot = seed.User.FullName, Gross = 1000, Net = 1000 };
        run.Employees.Add(payroll); db.HrPayrollRuns.Add(run); await db.SaveChangesAsync();
        Assert.Equal(1, await service.ApplyPenaltyAsync(action.Data, run.Id, default)); Assert.Equal(0, await service.ApplyPenaltyAsync(action.Data, run.Id, default)); Assert.Equal(900, payroll.Net);
    }

    [Fact]
    public async Task CycleWithInvalidWeightCannotActivate()
    {
        await using var db = TestAppDbContextFactory.Create(); var cycle = new PerformanceCycle { Name = "Bad", StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2026, 2, 1) };
        cycle.Goals.Add(new PerformanceGoal { PerformanceCycleId = cycle.Id, Name = "Only", Weight = 90 }); db.PerformanceCycles.Add(cycle); await db.SaveChangesAsync();
        Assert.False((await new PerformanceCaseService(db).ActivateCycleAsync(cycle.Id, default)).Success);
    }

    private static async Task<(User User, User Manager, EmployeeProfile Employee)> SeedAsync(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Reviewed Employee", "01074444441"); var manager = await TestAppDbContextFactory.SeedUserAsync(db, "Review Manager", "01074444442");
        var employee = new EmployeeProfile { UserId = user.Id, User = user }; employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id); db.EmployeeProfiles.Add(employee); await db.SaveChangesAsync(); return (user, manager, employee);
    }
}
