using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Finance.Commands;
using NaderGorge.Application.Features.Admin.Finance.Queries;
using NaderGorge.Application.Tests.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Finance;

public class PayrollTests
{
    [Fact]
    [Trait("Category", "Finance")]
    public async Task GeneratePayroll_CreatesDraftRecordsForEmployees()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee", "01011111111");

        // Seed Employee Profile
        var employee = new EmployeeProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BasicSalary = 4000.00m,
            StandardStartTime = new TimeSpan(9, 0, 0),
            TargetDailyHours = 8
        };
        db.EmployeeProfiles.Add(employee);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new GeneratePayrollCommandHandler(db, audit);

        var result = await handler.Handle(
            new GeneratePayrollCommand(6, 2026, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data);

        var record = await db.PayrollRecords.FirstOrDefaultAsync(pr => pr.EmployeeProfileId == employee.Id && pr.Month == 6 && pr.Year == 2026);
        Assert.NotNull(record);
        Assert.Equal(4000.00m, record!.BasicSalary);
        Assert.Equal(PayrollStatus.Draft, record.Status);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task AddAdjustment_UpdatesNetSalaryAndPersists()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee 2", "01022222222");

        var employee = new EmployeeProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BasicSalary = 5000.00m
        };
        db.EmployeeProfiles.Add(employee);

        var payroll = new PayrollRecord
        {
            Id = Guid.NewGuid(),
            EmployeeProfileId = employee.Id,
            Month = 6,
            Year = 2026,
            BasicSalary = 5000.00m,
            Status = PayrollStatus.Draft
        };
        db.PayrollRecords.Add(payroll);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new AddPayrollAdjustmentCommandHandler(db, audit);

        // Add Addition
        var result1 = await handler.Handle(
            new AddPayrollAdjustmentCommand(payroll.Id, PayrollAdjustmentType.Addition, 500.00m, "Performance bonus", Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result1.Success);
        Assert.Equal(500.00m, result1.Data!.Amount);
        Assert.Equal("Addition", result1.Data.Type);

        // Query payroll list to verify calculations
        var queryHandler = new GetPayrollQueryHandler(db);
        var queryResult = await queryHandler.Handle(new GetPayrollQuery(6, 2026), CancellationToken.None);

        Assert.True(queryResult.Success);
        var prDto = queryResult.Data!.First(pr => pr.Id == payroll.Id);
        Assert.Equal(500.00m, prDto.Additions);
        Assert.Equal(0.00m, prDto.Deductions);
        Assert.Equal(5500.00m, prDto.NetSalary);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task AddAdjustment_FailsIfPayrollIsApproved()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee 3", "01033333333");

        var employee = new EmployeeProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BasicSalary = 5000.00m
        };
        db.EmployeeProfiles.Add(employee);

        var payroll = new PayrollRecord
        {
            Id = Guid.NewGuid(),
            EmployeeProfileId = employee.Id,
            Month = 6,
            Year = 2026,
            BasicSalary = 5000.00m,
            Status = PayrollStatus.Approved // Already Approved
        };
        db.PayrollRecords.Add(payroll);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new AddPayrollAdjustmentCommandHandler(db, audit);

        var result = await handler.Handle(
            new AddPayrollAdjustmentCommand(payroll.Id, PayrollAdjustmentType.Addition, 500.00m, "Late bonus attempt", Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("لا يمكن تعديل سجل مرتبات معتمد ومغلق", result.Message);
    }

    [Fact]
    [Trait("Category", "Finance")]
    public async Task ApprovePayroll_TransitionsStatusAndLocks()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Test Employee 4", "01044444444");

        var employee = new EmployeeProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BasicSalary = 6000.00m
        };
        db.EmployeeProfiles.Add(employee);

        var payroll = new PayrollRecord
        {
            Id = Guid.NewGuid(),
            EmployeeProfileId = employee.Id,
            Month = 6,
            Year = 2026,
            BasicSalary = 6000.00m,
            Status = PayrollStatus.Draft
        };
        db.PayrollRecords.Add(payroll);
        await db.SaveChangesAsync();

        var audit = new TestAuditRepository();
        var handler = new ApprovePayrollCommandHandler(db, audit);

        var result = await handler.Handle(
            new ApprovePayrollCommand(payroll.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.Success);
        var record = await db.PayrollRecords.FindAsync(payroll.Id);
        Assert.Equal(PayrollStatus.Approved, record!.Status);
    }
}
