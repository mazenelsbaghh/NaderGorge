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
    string? OrganizationUnit, string? ShiftName, int AttendanceDays, int LateMinutes, decimal ApprovedLeaveDays, decimal? LastNetPayroll,
    int SupportConversations, int ClosedSupportConversations, int RespondedSupportConversations, decimal? AverageFirstResponseMinutes,
    int RatingCount, decimal? AverageStudentRating, int CompletedAttendanceDays, int EarlyLeaveMinutes, int WorkedMinutes);
public sealed record WorkforceReportPage(IReadOnlyList<WorkforceReportRow> Items, int Total, int Page, int PageSize);

public sealed class WorkforceReportService(IAppDbContext db)
{
    public async Task<WorkforceReportPage> QueryAsync(Guid actorUserId, WorkforceReportFilter filter, CancellationToken ct)
    {
        var today = CairoTime.GetCurrentDate();
        var allowed = await ResolveAllowedEmployeesAsync(actorUserId, filter.OrganizationUnitId, ct); var from = filter.From ?? today.AddMonths(-1); var to = filter.To ?? today;
        var fromUtc = CairoTime.ToUtc(from.ToDateTime(TimeOnly.MinValue)); var toUtc = CairoTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var query = db.EmployeeProfiles.AsNoTracking().Where(item => allowed.Contains(item.Id)
            && db.LiveSupportStaffConfigs.Any(config => config.UserId == item.UserId));
        if (!string.IsNullOrWhiteSpace(filter.Search)) { var search = filter.Search.Trim().ToLower(); query = query.Where(item => item.EmployeeNumber.ToLower().Contains(search) || item.User!.FullName.ToLower().Contains(search)); }
        var total = await query.CountAsync(ct); var page = Math.Max(1, filter.Page); var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var baseRows = await query.OrderBy(item => item.EmployeeNumber).Skip((page - 1) * pageSize).Take(pageSize).Select(item => new
        {
            item.Id, item.EmployeeNumber, item.User!.FullName, Status = item.EmploymentStatus.ToString(), item.HireDate,
            OrganizationUnit = db.EmploymentAssignments.Where(assignment => assignment.EmployeeId == item.Id && assignment.EffectiveFrom <= to && (!assignment.EffectiveTo.HasValue || assignment.EffectiveTo >= from)).OrderByDescending(assignment => assignment.EffectiveFrom).Select(assignment => assignment.OrganizationUnit!.Name).FirstOrDefault(),
            ShiftName = db.ShiftAssignments.Where(assignment => assignment.EmployeeId == item.Id && assignment.EffectiveFrom <= to && (!assignment.EffectiveTo.HasValue || assignment.EffectiveTo >= from)).OrderByDescending(assignment => assignment.EffectiveFrom).Select(assignment => assignment.ShiftTemplate!.Name).FirstOrDefault(),
            AttendanceDays = db.AttendanceSessions.Count(session => session.EmployeeId == item.Id && session.WorkDate >= from && session.WorkDate <= to),
            CompletedAttendanceDays = db.AttendanceSessions.Count(session => session.EmployeeId == item.Id && session.WorkDate >= from && session.WorkDate <= to && session.ClockedOutAt.HasValue),
            LateMinutes = db.AttendanceSessions.Where(session => session.EmployeeId == item.Id && session.WorkDate >= from && session.WorkDate <= to).Sum(session => session.LateMinutes),
            EarlyLeaveMinutes = db.AttendanceSessions.Where(session => session.EmployeeId == item.Id && session.WorkDate >= from && session.WorkDate <= to).Sum(session => session.EarlyLeaveMinutes),
            WorkedMinutes = db.AttendanceSessions.Where(session => session.EmployeeId == item.Id && session.WorkDate >= from && session.WorkDate <= to).Sum(session => session.WorkedMinutes),
            ApprovedLeaveDays = db.HrLeaveRequests.Where(leave => leave.EmployeeId == item.Id && leave.State == Domain.Enums.LeaveRequestState.Approved && leave.StartDate <= to && leave.EndDate >= from).Sum(leave => leave.Workdays),
            LastNetPayroll = db.EmployeePayrolls.Where(payroll => payroll.EmployeeId == item.Id && payroll.PayrollRun!.PeriodStart <= to && payroll.PayrollRun.PeriodEnd >= from).OrderByDescending(payroll => payroll.PayrollRun!.PeriodEnd).Select(payroll => (decimal?)payroll.Net).FirstOrDefault(),
            UserId = item.UserId
        }).ToListAsync(ct);
        var userIds = baseRows.Select(row => row.UserId).ToArray();
        var supportMetrics = await SupportMetricsAsync(userIds, fromUtc, toUtc, ct);
        var items = baseRows.Select(row =>
        {
            var support = supportMetrics.GetValueOrDefault(row.UserId, SupportMetric.Empty);
            return new WorkforceReportRow(row.Id, row.EmployeeNumber, row.FullName, row.Status, row.HireDate,
                row.OrganizationUnit, row.ShiftName, row.AttendanceDays, row.LateMinutes, row.ApprovedLeaveDays, row.LastNetPayroll,
                support.Conversations, support.ClosedConversations, support.RespondedConversations, support.AverageFirstResponseMinutes,
                support.RatingCount, support.AverageRating, row.CompletedAttendanceDays, row.EarlyLeaveMinutes, row.WorkedMinutes);
        }).ToList();
        return new WorkforceReportPage(items, total, page, pageSize);
    }

    public async Task<string> ExportCsvAsync(Guid actorUserId, WorkforceReportFilter filter, string reason, CancellationToken ct)
    {
        var all = await QueryAsync(actorUserId, filter with { Page = 1, PageSize = 200 }, ct); var builder = new StringBuilder("EmployeeNumber,FullName,Status,HireDate,OrganizationUnit,ShiftName,AttendanceDays,CompletedAttendanceDays,LateMinutes,EarlyLeaveMinutes,WorkedMinutes,ApprovedLeaveDays,LastNetPayroll,SupportConversations,ClosedSupportConversations,RespondedSupportConversations,AverageFirstResponseMinutes,RatingCount,AverageStudentRating\n");
        foreach (var row in all.Items) builder.AppendLine(string.Join(',', Csv(row.EmployeeNumber), Csv(row.FullName), Csv(row.Status), row.HireDate, Csv(row.OrganizationUnit ?? ""), Csv(row.ShiftName ?? ""), row.AttendanceDays, row.CompletedAttendanceDays, row.LateMinutes, row.EarlyLeaveMinutes, row.WorkedMinutes, row.ApprovedLeaveDays, row.LastNetPayroll, row.SupportConversations, row.ClosedSupportConversations, row.RespondedSupportConversations, row.AverageFirstResponseMinutes, row.RatingCount, row.AverageStudentRating));
        db.AuditLogs.Add(new AuditLog { Action = "ExportWorkforceReport", EntityType = "WorkforceReport", PerformedByUserId = actorUserId,
            ActorSnapshot = actorUserId.ToString(), Reason = reason.Trim(), NewValues = JsonSerializer.Serialize(new { filter, rows = all.Items.Count, all.Total }) }); await db.SaveChangesAsync(ct); return builder.ToString();
    }

    private async Task<Dictionary<Guid, SupportMetric>> SupportMetricsAsync(Guid[] userIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var assignments = await db.LiveSupportAssignments.AsNoTracking()
            .Where(assignment => userIds.Contains(assignment.StaffUserId) && assignment.StartedAt >= fromUtc && assignment.StartedAt < toUtc)
            .Select(assignment => new { assignment.StaffUserId, assignment.ConversationId, assignment.StartedAt, assignment.EndedAt, assignment.EndReason })
            .ToListAsync(ct);
        var conversationIds = assignments.Select(assignment => assignment.ConversationId).Distinct().ToArray();
        var responseMessages = await db.LiveSupportMessages.AsNoTracking()
            .Where(message => conversationIds.Contains(message.ConversationId) && message.SenderUserId.HasValue && userIds.Contains(message.SenderUserId.Value)
                && (message.SenderType == Domain.Enums.LiveSupportSenderType.Staff || message.SenderType == Domain.Enums.LiveSupportSenderType.Admin)
                && message.SentAt >= fromUtc && message.SentAt < toUtc && !message.DeletedAt.HasValue)
            .Select(message => new { message.ConversationId, message.SenderUserId, message.SentAt })
            .ToListAsync(ct);
        var ratings = await db.LiveSupportRatings.AsNoTracking()
            .Where(rating => conversationIds.Contains(rating.ConversationId) && rating.SubmittedAt >= fromUtc && rating.SubmittedAt < toUtc)
            .Select(rating => new { rating.ConversationId, rating.Stars })
            .ToListAsync(ct);
        var responseLookup = responseMessages.ToLookup(message => (message.ConversationId, message.SenderUserId!.Value));
        return assignments.GroupBy(assignment => assignment.StaffUserId).ToDictionary(group => group.Key, group =>
        {
            var staffAssignments = group.ToArray();
            var responseMinutes = staffAssignments.Select(assignment => responseLookup[(assignment.ConversationId, assignment.StaffUserId)]
                .Where(message => message.SentAt >= assignment.StartedAt && (!assignment.EndedAt.HasValue || message.SentAt <= assignment.EndedAt))
                .Select(message => (decimal?)(message.SentAt - assignment.StartedAt).TotalMinutes).Min()).Where(minutes => minutes.HasValue).Select(minutes => minutes!.Value).ToArray();
            var staffConversationIds = staffAssignments.Select(assignment => assignment.ConversationId).Distinct().ToHashSet();
            var staffRatings = ratings.Where(rating => staffConversationIds.Contains(rating.ConversationId)).Select(rating => rating.Stars).ToArray();
            var respondedConversations = staffAssignments.Where(assignment => responseLookup[(assignment.ConversationId, assignment.StaffUserId)].Any(message =>
                message.SentAt >= assignment.StartedAt && (!assignment.EndedAt.HasValue || message.SentAt <= assignment.EndedAt)))
                .Select(assignment => assignment.ConversationId).Distinct().Count();
            return new SupportMetric(staffConversationIds.Count, staffAssignments.Where(assignment => assignment.EndReason == Domain.Enums.LiveSupportAssignmentEndReason.Closed).Select(assignment => assignment.ConversationId).Distinct().Count(),
                respondedConversations, responseMinutes.Length == 0 ? null : decimal.Round(responseMinutes.Average(), 2), staffRatings.Length,
                staffRatings.Length == 0 ? null : decimal.Round((decimal)staffRatings.Average(), 2));
        });
    }

    private sealed record SupportMetric(int Conversations, int ClosedConversations, int RespondedConversations,
        decimal? AverageFirstResponseMinutes, int RatingCount, decimal? AverageRating)
    {
        public static readonly SupportMetric Empty = new(0, 0, 0, null, 0, null);
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
