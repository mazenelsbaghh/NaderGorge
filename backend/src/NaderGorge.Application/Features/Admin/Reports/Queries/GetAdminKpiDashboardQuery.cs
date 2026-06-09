using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Application.Features.Admin.Reports.Queries;

public record GetAdminKpiDashboardQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    string? RoleName,
    Guid? EmployeeId
) : IRequest<ApiResponse<KpiDashboardDto>>;

public record KpiDashboardDto(
    AttendanceKpiDto Attendance,
    TaskKpiDto Tasks,
    List<CrmOutcomeKpiDto> CrmOutcomes,
    MediaKpiDto Media,
    PaymentKpiDto Payments,
    List<PayrollStatusKpiDto> PayrollStatus
);

public record AttendanceKpiDto(
    int TotalLogs,
    int PresentCount,
    int LateCount,
    int AbsentCount,
    decimal PresentRate,
    decimal LateRate,
    decimal AbsentRate
);

public record TaskKpiDto(
    int TotalTasks,
    int CompletedCount,
    int PendingCount,
    int OverdueCount,
    decimal CompletionRate
);

public record CrmOutcomeKpiDto(
    string Outcome,
    int Count
);

public record MediaKpiDto(
    int TotalItems,
    int PublishedCount,
    decimal AverageProductionDays
);

public record PaymentKpiDto(
    int TotalTransactions,
    int AutoMatchedCount,
    int CouponActivatedCount,
    decimal AutoMatchRate
);

public record PayrollStatusKpiDto(
    string Status,
    int Count
);

public class GetAdminKpiDashboardQueryHandler : IRequestHandler<GetAdminKpiDashboardQuery, ApiResponse<KpiDashboardDto>>
{
    private readonly IAppDbContext _db;

    public GetAdminKpiDashboardQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<KpiDashboardDto>> Handle(GetAdminKpiDashboardQuery request, CancellationToken ct)
    {
        Guid? targetUserId = null;
        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            var employeeProfile = await _db.EmployeeProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(ep => ep.Id == request.EmployeeId.Value, ct);
            if (employeeProfile != null)
            {
                targetUserId = employeeProfile.UserId;
            }
        }

        // 1. Attendance Metrics
        var attendanceQuery = _db.AttendanceLogs.AsNoTracking().AsQueryable();

        if (request.StartDate.HasValue)
        {
            var startDateOnly = DateOnly.FromDateTime(request.StartDate.Value);
            attendanceQuery = attendanceQuery.Where(al => al.Date >= startDateOnly);
        }
        if (request.EndDate.HasValue)
        {
            var endDateOnly = DateOnly.FromDateTime(request.EndDate.Value);
            attendanceQuery = attendanceQuery.Where(al => al.Date <= endDateOnly);
        }
        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            attendanceQuery = attendanceQuery.Where(al => al.EmployeeId == request.EmployeeId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.RoleName))
        {
            attendanceQuery = attendanceQuery.Where(al => al.Employee != null && al.Employee.User != null &&
                al.Employee.User.UserRoles.Any(ur => ur.Role.Name == request.RoleName));
        }

        var attendanceList = await attendanceQuery.ToListAsync(ct);
        var totalAttendanceLogs = attendanceList.Count;
        var presentCount = attendanceList.Count(al => al.Status == AttendanceStatus.Present);
        var lateCount = attendanceList.Count(al => al.Status == AttendanceStatus.Late);
        var absentCount = attendanceList.Count(al => al.Status == AttendanceStatus.Absent);

        var presentRate = totalAttendanceLogs > 0 ? Math.Round((decimal)presentCount / totalAttendanceLogs * 100m, 2) : 0m;
        var lateRate = totalAttendanceLogs > 0 ? Math.Round((decimal)lateCount / totalAttendanceLogs * 100m, 2) : 0m;
        var absentRate = totalAttendanceLogs > 0 ? Math.Round((decimal)absentCount / totalAttendanceLogs * 100m, 2) : 0m;

        var attendanceKpi = new AttendanceKpiDto(totalAttendanceLogs, presentCount, lateCount, absentCount, presentRate, lateRate, absentRate);

        // 2. Task Metrics
        var taskQuery = _db.TaskItems.AsNoTracking().AsQueryable();

        if (request.StartDate.HasValue)
        {
            var startUtc = request.StartDate.Value.ToUniversalTime();
            taskQuery = taskQuery.Where(t => t.CreatedAt >= startUtc);
        }
        if (request.EndDate.HasValue)
        {
            var endUtc = request.EndDate.Value.ToUniversalTime();
            taskQuery = taskQuery.Where(t => t.CreatedAt <= endUtc);
        }
        if (targetUserId.HasValue)
        {
            taskQuery = taskQuery.Where(t => t.AssigneeId == targetUserId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.RoleName))
        {
            taskQuery = taskQuery.Where(t => t.Assignee != null &&
                t.Assignee.UserRoles.Any(ur => ur.Role.Name == request.RoleName));
        }

        var taskList = await taskQuery.ToListAsync(ct);
        var totalTasks = taskList.Count;
        var completedCount = taskList.Count(t => t.Status == TaskStatus.Completed);
        var pendingCount = taskList.Count(t => t.Status != TaskStatus.Completed);
        var overdueCount = taskList.Count(t => t.Status != TaskStatus.Completed && t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);

        var completionRate = totalTasks > 0 ? Math.Round((decimal)completedCount / totalTasks * 100m, 2) : 0m;

        var taskKpi = new TaskKpiDto(totalTasks, completedCount, pendingCount, overdueCount, completionRate);

        // 3. CRM Outcome Distribution
        var crmQuery = _db.CrmCallLogs.AsNoTracking().AsQueryable();

        if (request.StartDate.HasValue)
        {
            var startUtc = request.StartDate.Value.ToUniversalTime();
            crmQuery = crmQuery.Where(c => c.CallDate >= startUtc);
        }
        if (request.EndDate.HasValue)
        {
            var endUtc = request.EndDate.Value.ToUniversalTime();
            crmQuery = crmQuery.Where(c => c.CallDate <= endUtc);
        }
        if (targetUserId.HasValue)
        {
            crmQuery = crmQuery.Where(c => c.AgentId == targetUserId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.RoleName))
        {
            crmQuery = crmQuery.Where(c => c.Agent != null &&
                c.Agent.UserRoles.Any(ur => ur.Role.Name == request.RoleName));
        }

        var crmGrouped = await crmQuery
            .GroupBy(c => c.Outcome)
            .Select(g => new CrmOutcomeKpiDto(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        // Fill in missing outcomes to ensure frontend displays consistent categories if desired
        var allOutcomes = Enum.GetNames(typeof(CallOutcome));
        foreach (var outcomeName in allOutcomes)
        {
            if (!crmGrouped.Any(cg => cg.Outcome == outcomeName))
            {
                crmGrouped.Add(new CrmOutcomeKpiDto(outcomeName, 0));
            }
        }

        // 4. Media Production KPIs
        var mediaQuery = _db.MediaProductionPipelines.AsNoTracking().AsQueryable();

        if (request.StartDate.HasValue)
        {
            var startUtc = request.StartDate.Value.ToUniversalTime();
            mediaQuery = mediaQuery.Where(m => m.CreatedAt >= startUtc);
        }
        if (request.EndDate.HasValue)
        {
            var endUtc = request.EndDate.Value.ToUniversalTime();
            mediaQuery = mediaQuery.Where(m => m.CreatedAt <= endUtc);
        }
        if (targetUserId.HasValue)
        {
            mediaQuery = mediaQuery.Where(m => m.AssignedAgentId == targetUserId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.RoleName))
        {
            mediaQuery = mediaQuery.Where(m => m.AssignedAgent != null &&
                m.AssignedAgent.UserRoles.Any(ur => ur.Role.Name == request.RoleName));
        }

        var mediaList = await mediaQuery.ToListAsync(ct);
        var totalMediaItems = mediaList.Count;
        var publishedCount = mediaList.Count(m => m.Stage == MediaStage.Published);

        var publishedItemsWithTimestamps = mediaList
            .Where(m => m.Stage == MediaStage.Published && m.PublishedAt.HasValue)
            .ToList();

        decimal avgProductionDays = 0m;
        if (publishedItemsWithTimestamps.Any())
        {
            var totalDays = publishedItemsWithTimestamps
                .Sum(m => (m.PublishedAt!.Value - m.CreatedAt).TotalDays);
            avgProductionDays = Math.Round((decimal)totalDays / publishedItemsWithTimestamps.Count, 2);
        }

        var mediaKpi = new MediaKpiDto(totalMediaItems, publishedCount, avgProductionDays);

        // 5. Payment KPIs (Auto-matching vs Code Activation)
        // Note: For payment KPI, we look at activations vs balance purchases.
        // If employeeId or roleName are specified, payment KPIs are typically empty or general since they are platform-wide.
        // However, we still apply Date Filters.
        var btQuery = _db.BalanceTransactions.AsNoTracking().Where(t => t.TransactionType == "ContentPurchase").AsQueryable();
        var codQuery = _db.AccessCodeActivationLogs.AsNoTracking().AsQueryable();

        if (request.StartDate.HasValue)
        {
            var startUtc = request.StartDate.Value.ToUniversalTime();
            btQuery = btQuery.Where(t => t.CreatedAt >= startUtc);
            codQuery = codQuery.Where(c => c.ActivatedAt >= startUtc);
        }
        if (request.EndDate.HasValue)
        {
            var endUtc = request.EndDate.Value.ToUniversalTime();
            btQuery = btQuery.Where(t => t.CreatedAt <= endUtc);
            codQuery = codQuery.Where(c => c.ActivatedAt <= endUtc);
        }

        var autoMatchedCount = await btQuery.CountAsync(ct);
        var couponActivatedCount = await codQuery.CountAsync(ct);
        var totalTransactions = autoMatchedCount + couponActivatedCount;
        var autoMatchRate = totalTransactions > 0 ? Math.Round((decimal)autoMatchedCount / totalTransactions * 100m, 2) : 0m;

        var paymentKpi = new PaymentKpiDto(totalTransactions, autoMatchedCount, couponActivatedCount, autoMatchRate);

        // 6. Payroll Status Distribution
        var payrollQuery = _db.PayrollRecords.AsNoTracking().AsQueryable();

        if (request.StartDate.HasValue)
        {
            var startUtc = request.StartDate.Value.ToUniversalTime();
            payrollQuery = payrollQuery.Where(p => p.CreatedAt >= startUtc);
        }
        if (request.EndDate.HasValue)
        {
            var endUtc = request.EndDate.Value.ToUniversalTime();
            payrollQuery = payrollQuery.Where(p => p.CreatedAt <= endUtc);
        }
        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            payrollQuery = payrollQuery.Where(p => p.EmployeeProfileId == request.EmployeeId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.RoleName))
        {
            payrollQuery = payrollQuery.Where(p => p.EmployeeProfile != null && p.EmployeeProfile.User != null &&
                p.EmployeeProfile.User.UserRoles.Any(ur => ur.Role.Name == request.RoleName));
        }

        var payrollGrouped = await payrollQuery
            .GroupBy(p => p.Status)
            .Select(g => new PayrollStatusKpiDto(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        var allPayrollStatuses = Enum.GetNames(typeof(PayrollStatus));
        foreach (var statusName in allPayrollStatuses)
        {
            if (!payrollGrouped.Any(pg => pg.Status == statusName))
            {
                payrollGrouped.Add(new PayrollStatusKpiDto(statusName, 0));
            }
        }

        var dto = new KpiDashboardDto(attendanceKpi, taskKpi, crmGrouped, mediaKpi, paymentKpi, payrollGrouped);
        return ApiResponse<KpiDashboardDto>.Ok(dto);
    }
}
