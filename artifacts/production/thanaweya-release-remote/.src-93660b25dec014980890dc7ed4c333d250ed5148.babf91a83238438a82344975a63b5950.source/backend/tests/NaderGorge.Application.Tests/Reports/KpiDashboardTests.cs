using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Reports.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Application.Tests.Reports;

public class KpiDashboardTests
{
    [Fact]
    public async Task Handle_CalculatesCorrectMathematicalAggregates_WithFilters()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();

        // 1. Seed Roles and Users
        var supervisorRole = new Role { Name = "Supervisor", Type = RoleType.Supervisor };
        var assistantRole = new Role { Name = "Assistant", Type = RoleType.Assistant };
        db.Roles.AddRange(supervisorRole, assistantRole);
        await db.SaveChangesAsync();

        var supervisorUser = await TestAppDbContextFactory.SeedUserAsync(db, "Supervisor User", "01020202021");
        var assistantUser = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant User", "01020202022");

        db.UserRoles.AddRange(
            new UserRole { UserId = supervisorUser.Id, RoleId = supervisorRole.Id },
            new UserRole { UserId = assistantUser.Id, RoleId = assistantRole.Id }
        );
        await db.SaveChangesAsync();

        // Seed Employee Profiles
        var supervisorEmp = new EmployeeProfile { UserId = supervisorUser.Id, BasicSalary = 10000 };
        var assistantEmp = new EmployeeProfile { UserId = assistantUser.Id, BasicSalary = 5000 };
        db.EmployeeProfiles.AddRange(supervisorEmp, assistantEmp);
        await db.SaveChangesAsync();

        // 2. Seed Attendance Logs
        // 3 logs: 2 present, 1 late for supervisorEmp
        db.AttendanceLogs.AddRange(
            new AttendanceLog { EmployeeId = supervisorEmp.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)), ClockIn = DateTime.UtcNow.AddDays(-5), Status = AttendanceStatus.Present },
            new AttendanceLog { EmployeeId = supervisorEmp.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4)), ClockIn = DateTime.UtcNow.AddDays(-4), Status = AttendanceStatus.Present },
            new AttendanceLog { EmployeeId = supervisorEmp.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)), ClockIn = DateTime.UtcNow.AddDays(-3), Status = AttendanceStatus.Late }
        );

        // 2 logs: 1 absent, 1 present for assistantEmp
        db.AttendanceLogs.AddRange(
            new AttendanceLog { EmployeeId = assistantEmp.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), ClockIn = DateTime.UtcNow.AddDays(-2), Status = AttendanceStatus.Absent },
            new AttendanceLog { EmployeeId = assistantEmp.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), ClockIn = DateTime.UtcNow.AddDays(-1), Status = AttendanceStatus.Present }
        );
        await db.SaveChangesAsync();

        // 3. Seed Tasks
        // supervisorEmp (UserId = supervisorUser.Id) tasks: 2 total, 1 completed, 1 pending
        db.TaskItems.AddRange(
            new TaskItem { Title = "Task 1", AssigneeId = supervisorUser.Id, Status = TaskStatus.Completed, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new TaskItem { Title = "Task 2", AssigneeId = supervisorUser.Id, Status = TaskStatus.InProgress, CreatedAt = DateTime.UtcNow.AddDays(-4), DueDate = DateTime.UtcNow.AddDays(-1) } // Overdue
        );
        // assistantEmp tasks: 1 total, 1 pending
        db.TaskItems.Add(
            new TaskItem { Title = "Task 3", AssigneeId = assistantUser.Id, Status = TaskStatus.New, CreatedAt = DateTime.UtcNow.AddDays(-2), DueDate = DateTime.UtcNow.AddDays(2) } // Not overdue
        );
        await db.SaveChangesAsync();

        // 4. Seed CRM Call Logs
        // 4 calls: 2 Completed, 1 NoAnswer, 1 Postponed
        db.CrmCallLogs.AddRange(
            new CrmCallLog { StudentId = Guid.NewGuid(), AgentId = supervisorUser.Id, Outcome = CallOutcome.Completed, CallDate = DateTime.UtcNow.AddDays(-3) },
            new CrmCallLog { StudentId = Guid.NewGuid(), AgentId = supervisorUser.Id, Outcome = CallOutcome.Completed, CallDate = DateTime.UtcNow.AddDays(-2) },
            new CrmCallLog { StudentId = Guid.NewGuid(), AgentId = assistantUser.Id, Outcome = CallOutcome.NoAnswer, CallDate = DateTime.UtcNow.AddDays(-2) },
            new CrmCallLog { StudentId = Guid.NewGuid(), AgentId = assistantUser.Id, Outcome = CallOutcome.Postponed, CallDate = DateTime.UtcNow.AddDays(-1) }
        );
        await db.SaveChangesAsync();

        // 5. Seed Media Production Pipelines
        // 2 items: 1 Published (took 4 days), 1 Preparation
        var media1 = new MediaProductionPipeline
        {
            Title = "Video A",
            Stage = MediaStage.Published,
            AssignedAgentId = assistantUser.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-6),
            PublishedAt = DateTime.UtcNow.AddDays(-2) // 4 days duration
        };
        var media2 = new MediaProductionPipeline
        {
            Title = "Video B",
            Stage = MediaStage.Preparation,
            AssignedAgentId = assistantUser.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.MediaProductionPipelines.AddRange(media1, media2);
        await db.SaveChangesAsync();

        // 6. Seed Payment transactions
        // 2 ContentPurchase balance transactions, 3 AccessCode activations
        var studentBalance = new StudentBalance { UserId = Guid.NewGuid(), CurrentBalance = 100m };
        db.StudentBalances.Add(studentBalance);
        await db.SaveChangesAsync();

        db.BalanceTransactions.AddRange(
            new BalanceTransaction { StudentBalanceId = studentBalance.Id, Amount = -20m, TransactionType = "ContentPurchase", CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new BalanceTransaction { StudentBalanceId = studentBalance.Id, Amount = -30m, TransactionType = "ContentPurchase", CreatedAt = DateTime.UtcNow.AddDays(-2) }
        );

        db.AccessCodeActivationLogs.AddRange(
            new AccessCodeActivationLog { StudentId = Guid.NewGuid(), Price = 50m, ActivatedAt = DateTime.UtcNow.AddDays(-3) },
            new AccessCodeActivationLog { StudentId = Guid.NewGuid(), Price = 50m, ActivatedAt = DateTime.UtcNow.AddDays(-2) },
            new AccessCodeActivationLog { StudentId = Guid.NewGuid(), Price = 50m, ActivatedAt = DateTime.UtcNow.AddDays(-1) }
        );
        await db.SaveChangesAsync();

        // 7. Seed Payroll Records
        // 2 records: 1 Approved, 1 Draft
        db.PayrollRecords.AddRange(
            new PayrollRecord { EmployeeProfileId = supervisorEmp.Id, Month = 5, Year = 2026, BasicSalary = 10000, Status = PayrollStatus.Approved, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new PayrollRecord { EmployeeProfileId = assistantEmp.Id, Month = 5, Year = 2026, BasicSalary = 5000, Status = PayrollStatus.Draft, CreatedAt = DateTime.UtcNow.AddDays(-4) }
        );
        await db.SaveChangesAsync();

        var handler = new GetAdminKpiDashboardQueryHandler(db);

        // Test 1: No filters, overall aggregates
        var resultAll = await handler.Handle(new GetAdminKpiDashboardQuery(null, null, null, null), CancellationToken.None);
        Assert.True(resultAll.Success);
        Assert.NotNull(resultAll.Data);
        var allKpis = resultAll.Data!;

        // Attendance check: 5 total logs (3 present, 1 late, 1 absent)
        Assert.Equal(5, allKpis.Attendance.TotalLogs);
        Assert.Equal(3, allKpis.Attendance.PresentCount);
        Assert.Equal(1, allKpis.Attendance.LateCount);
        Assert.Equal(1, allKpis.Attendance.AbsentCount);
        Assert.Equal(60m, allKpis.Attendance.PresentRate); // 3/5 = 60%
        Assert.Equal(20m, allKpis.Attendance.LateRate); // 1/5 = 20%
        Assert.Equal(20m, allKpis.Attendance.AbsentRate); // 1/5 = 20%

        // Tasks check: 3 total, 1 completed, 2 pending, 1 overdue
        Assert.Equal(3, allKpis.Tasks.TotalTasks);
        Assert.Equal(1, allKpis.Tasks.CompletedCount);
        Assert.Equal(2, allKpis.Tasks.PendingCount);
        Assert.Equal(1, allKpis.Tasks.OverdueCount);
        Assert.Equal(33.33m, allKpis.Tasks.CompletionRate); // 1/3 = 33.33%

        // CRM outcomes check: should have outcome records with counts
        var completedOutcome = allKpis.CrmOutcomes.FirstOrDefault(c => c.Outcome == "Completed");
        Assert.NotNull(completedOutcome);
        Assert.Equal(2, completedOutcome!.Count);

        // Media check: 2 items, 1 published, average production days = 4
        Assert.Equal(2, allKpis.Media.TotalItems);
        Assert.Equal(1, allKpis.Media.PublishedCount);
        Assert.Equal(4m, allKpis.Media.AverageProductionDays);

        // Payments check: 2 auto-matched, 3 coupon activated, autoMatchRate = 2/5 = 40%
        Assert.Equal(5, allKpis.Payments.TotalTransactions);
        Assert.Equal(2, allKpis.Payments.AutoMatchedCount);
        Assert.Equal(3, allKpis.Payments.CouponActivatedCount);
        Assert.Equal(40m, allKpis.Payments.AutoMatchRate);

        // Payroll check: 1 Approved, 1 Draft
        var approvedPayroll = allKpis.PayrollStatus.FirstOrDefault(p => p.Status == "Approved");
        Assert.NotNull(approvedPayroll);
        Assert.Equal(1, approvedPayroll!.Count);

        // Test 2: Filter by RoleName = "Supervisor"
        var resultSupervisor = await handler.Handle(new GetAdminKpiDashboardQuery(null, null, "Supervisor", null), CancellationToken.None);
        Assert.NotNull(resultSupervisor.Data);
        var supervisorKpis = resultSupervisor.Data!;
        Assert.Equal(3, supervisorKpis.Attendance.TotalLogs); // supervisor logs only
        Assert.Equal(2, supervisorKpis.Tasks.TotalTasks); // supervisor tasks only

        // Test 3: Filter by Employee = assistantEmp.Id
        var resultAssistant = await handler.Handle(new GetAdminKpiDashboardQuery(null, null, null, assistantEmp.Id), CancellationToken.None);
        Assert.NotNull(resultAssistant.Data);
        var assistantKpis = resultAssistant.Data!;
        Assert.Equal(2, assistantKpis.Attendance.TotalLogs); // assistant logs only
        Assert.Equal(1, assistantKpis.Tasks.TotalTasks); // assistant tasks only (UserId matches assistantUser.Id)
    }
}
