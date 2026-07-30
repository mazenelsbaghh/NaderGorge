using NaderGorge.Application.Features.HR.Payroll.FinancialRequests;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class HrFinancialRequestTests
{
    [Fact]
    public async Task ExpenseRequiresAttachmentAndLoanScheduleConservesAmount()
    {
        await using var db = TestAppDbContextFactory.Create(); var seeded = await SeedAsync(db); var service = new FinancialRequestService(db);
        var expense = await service.SubmitAsync(seeded.User.Id, HrFinancialRequestType.Expense, 300, 1, "travel", null, default);
        Assert.False(expense.Success); Assert.Contains("ATTACHMENT_REQUIRED", expense.Errors!);
        var loan = await service.SubmitAsync(seeded.User.Id, HrFinancialRequestType.Loan, 1000, 3, "loan", "contract.pdf", default);
        Assert.True(loan.Success); var approved = await service.ApproveAsync(loan.Data, seeded.Approver.Id, new DateOnly(2026, 8, 1), 1, default);
        Assert.True(approved.Success); var request = db.HrFinancialRequests.Single(); Assert.Equal(1000, request.OutstandingBalance);
        Assert.Equal(1000, request.Installments.Sum(item => item.Amount)); Assert.Equal([333.33m, 333.33m, 333.34m], request.Installments.OrderBy(item => item.Sequence).Select(item => item.Amount));
    }

    [Fact]
    public async Task ApplyingInstallmentIsReplaySafeAndReducesBalanceOnce()
    {
        await using var db = TestAppDbContextFactory.Create(); var seeded = await SeedAsync(db); var service = new FinancialRequestService(db);
        var loan = await service.SubmitAsync(seeded.User.Id, HrFinancialRequestType.Advance, 500, 1, "advance", "evidence.pdf", default);
        await service.ApproveAsync(loan.Data, seeded.Approver.Id, new DateOnly(2026, 8, 1), 1, default);
        var run = new HrPayrollRun { RunNumber = "PAY-TEST", PeriodStart = new DateOnly(2026, 8, 1), PeriodEnd = new DateOnly(2026, 8, 31), Status = HrPayrollRunStatus.Prepared };
        var payroll = new EmployeePayroll { PayrollRunId = run.Id, PayrollRun = run, EmployeeId = seeded.Employee.Id, Employee = seeded.Employee,
            EmployeeNumberSnapshot = seeded.Employee.EmployeeNumber, EmployeeNameSnapshot = seeded.User.FullName, Gross = 1000, Net = 1000 };
        run.Employees.Add(payroll); db.HrPayrollRuns.Add(run); await db.SaveChangesAsync();
        Assert.Equal(1, await service.ApplyDueInputsAsync(run.Id, default)); Assert.Equal(0, await service.ApplyDueInputsAsync(run.Id, default));
        Assert.Single(db.HrPayrollInputSources); Assert.Single(db.PayrollLineItems); Assert.Equal(0, db.HrFinancialRequests.Single().OutstandingBalance); Assert.Equal(500, payroll.Deductions); Assert.Equal(500, payroll.Net);
    }

    private static async Task<(User User, EmployeeProfile Employee, User Approver)> SeedAsync(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Finance Employee", "01076666661"); var approver = await TestAppDbContextFactory.SeedUserAsync(db, "Finance", "01076666662");
        var employee = new EmployeeProfile { UserId = user.Id, User = user, BasicSalary = 1000 }; employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
        db.EmployeeProfiles.Add(employee); await db.SaveChangesAsync(); return (user, employee, approver);
    }
}
