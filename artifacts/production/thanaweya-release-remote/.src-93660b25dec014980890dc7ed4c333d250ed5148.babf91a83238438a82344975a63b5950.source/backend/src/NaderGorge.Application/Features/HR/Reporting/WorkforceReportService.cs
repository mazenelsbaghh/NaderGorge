using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Reporting;

public sealed record WorkforceReportFilter(DateOnly? From, DateOnly? To, Guid? OrganizationUnitId, string? Search, int Page = 1, int PageSize = 50);
public sealed record WorkforceReportRow(Guid EmployeeId, string EmployeeNumber, string FullName, string Status, DateOnly HireDate,
    string? OrganizationUnit, int AttendanceDays, int LateMinutes, decimal ApprovedLeaveDays, decimal? LastNetPayroll);
public sealed record WorkforceReportPage(IReadOnlyList<WorkforceReportRow> Items, int Total, int Page, int PageSize);

public sealed class WorkforceReportService(IAppDbContext db)
{
    public async Task<WorkforceReportPage> QueryAsync(Guid actorUserId, WorkforceReportFilter filter, CancellationToken ct)
    {
        var today = CairoTime.GetCurrentDate();
        var allowed = await ResolveAllowedEmployeesAsync(actorUserId, filter.OrganizationUnitId, ct); var from = filter.From ?? today.AddMonths(-1); var to = filter.To ?? today;
        var query = db.EmployeeProfiles.AsNoTracking().Where(item => allowed.Contains(item.Id));
        if (!string.IsNullOrWhiteSpace(filter.Search)) { var search = filter.Search.Trim().ToLower(); query = query.Where(item => item.EmployeeNumber.ToLower().Contains(search) || item.User!.FullName.ToLower().Contains(search)); }
        var total = await query.CountAsync(ct); var page = Math.Max(1, filter.Page); var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var items = await query.OrderBy(item => item.EmployeeNumber).Skip((page - 1) * pageSize).Take(pageSize).Select(item => new WorkforceReportRow(
            item.Id, item.EmployeeNumber, item.User!.FullName, item.EmploymentStatus.ToString(), item.HireDate,
            db.EmploymentAssignments.Where(assignment => assignment.EmployeeId == item.Id && assignment.EffectiveFrom <= to && (!assignment.EffectiveTo.HasValue || assignment.EffectiveTo >= from)).OrderByDescending(assignment => assignment.EffectiveFrom).Select(assignment => assignment.OrganizationUnit!.Name).FirstOrDefault(),
            db.AttendanceSessions.Count(session => session.EmployeeId == item.Id && session.WorkDate >= from && session.WorkDate <= to),
            db.AttendanceSessions.Where(session => session.EmployeeId == item.Id && session.WorkDate >= from && session.WorkDate <= to).Sum(session => session.LateMinutes),
            db.HrLeaveRequests.Where(leave => leave.EmployeeId == item.Id && leave.State == Domain.Enums.LeaveRequestState.Approved && leave.StartDate <= to && leave.EndDate >= from).Sum(leave => leave.Workdays),
            db.EmployeePayrolls.Where(payroll => payroll.EmployeeId == item.Id && payroll.PayrollRun!.PeriodStart <= to && payroll.PayrollRun.PeriodEnd >= from).OrderByDescending(payroll => payroll.PayrollRun!.PeriodEnd).Select(payroll => (decimal?)payroll.Net).FirstOrDefault())).ToListAsync(ct);
        return new WorkforceReportPage(items, total, page, pageSize);
    }

    public async Task<string> ExportCsvAsync(Guid actorUserId, WorkforceReportFilter filter, string reason, CancellationToken ct)
    {
        var all = await QueryAsync(actorUserId, filter with { Page = 1, PageSize = 200 }, ct); var builder = new StringBuilder("EmployeeNumber,FullName,Status,HireDate,OrganizationUnit,AttendanceDays,LateMinutes,ApprovedLeaveDays,LastNetPayroll\n");
        foreach (var row in all.Items) builder.AppendLine(string.Join(',', Csv(row.EmployeeNumber), Csv(row.FullName), Csv(row.Status), row.HireDate, Csv(row.OrganizationUnit ?? ""), row.AttendanceDays, row.LateMinutes, row.ApprovedLeaveDays, row.LastNetPayroll));
        db.AuditLogs.Add(new AuditLog { Action = "ExportWorkforceReport", EntityType = "WorkforceReport", PerformedByUserId = actorUserId,
            ActorSnapshot = actorUserId.ToString(), Reason = reason.Trim(), NewValues = JsonSerializer.Serialize(new { filter, rows = all.Items.Count, all.Total }) }); await db.SaveChangesAsync(ct); return builder.ToString();
    }

    private async Task<List<Guid>> ResolveAllowedEmployeesAsync(Guid actorUserId, Guid? requestedUnitId, CancellationToken ct)
    {
        var roles = await db.UserRoles.AsNoTracking().Where(item => item.UserId == actorUserId).Select(item => new { item.Role.Name, item.Role.PermissionsJson }).ToListAsync(ct);
        if (roles.Any(item => item.Name == "Admin") || roles.Any(item => HasAllScope(item.PermissionsJson)))
        {
            var all = db.EmployeeProfiles.AsNoTracking(); if (requestedUnitId.HasValue) all = all.Where(employee => db.EmploymentAssignments.Any(assignment => assignment.EmployeeId == employee.Id && assignment.OrganizationUnitId == requestedUnitId)); return await all.Select(item => item.Id).ToListAsync(ct);
        }
        var actorEmployeeId = await db.EmployeeProfiles.Where(item => item.UserId == actorUserId).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(ct); if (!actorEmployeeId.HasValue) return [];
        var today = CairoTime.GetCurrentDate(); var root = await db.EmploymentAssignments.Where(item => item.EmployeeId == actorEmployeeId && item.EffectiveFrom <= today && (!item.EffectiveTo.HasValue || item.EffectiveTo >= today)).OrderByDescending(item => item.EffectiveFrom).Select(item => (Guid?)item.OrganizationUnitId).FirstOrDefaultAsync(ct);
        if (!root.HasValue) return [actorEmployeeId.Value]; var units = await db.OrganizationUnits.Select(item => new { item.Id, item.ParentId }).ToListAsync(ct); var allowedUnits = new HashSet<Guid> { root.Value }; bool changed;
        do { changed = false; foreach (var unit in units.Where(item => item.ParentId.HasValue && allowedUnits.Contains(item.ParentId.Value))) changed |= allowedUnits.Add(unit.Id); } while (changed);
        if (requestedUnitId.HasValue && allowedUnits.Contains(requestedUnitId.Value)) allowedUnits = [requestedUnitId.Value];
        return await db.EmploymentAssignments.Where(item => allowedUnits.Contains(item.OrganizationUnitId) && item.EffectiveFrom <= today && (!item.EffectiveTo.HasValue || item.EffectiveTo >= today)).Select(item => item.EmployeeId).Distinct().ToListAsync(ct);
    }
    private static bool HasAllScope(string? json) => (json ?? "").Contains(HrPermissions.ReportRead + "@all", StringComparison.OrdinalIgnoreCase) || (json ?? "").Contains('"' + HrPermissions.ReportRead + '"', StringComparison.OrdinalIgnoreCase);
    private static string Csv(object value) => '"' + value.ToString()!.Replace("\"", "\"\"") + '"';
}
