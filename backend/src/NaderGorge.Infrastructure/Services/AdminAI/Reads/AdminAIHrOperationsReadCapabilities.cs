using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIHrOperationsSummary(int ShiftAssignments, int AttendanceRecords, int Breaks, int AttendanceCorrections, int LeaveRequests, int LeaveBalances, int Approvals, int PayrollRuns, int EmployeePayrolls, int Compensations, DateTime DataAsOf);

public sealed class AdminAIHrOperationsSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "hr-operations.summary";
    public Type OutputType => typeof(AdminAIHrOperationsSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAIHrOperationsSummary(
            await db.ShiftAssignments.AsNoTracking().CountAsync(ct),
            await db.AttendanceSessions.AsNoTracking().CountAsync(ct),
            await db.AttendanceBreaks.AsNoTracking().CountAsync(ct),
            await db.AttendanceCorrections.AsNoTracking().CountAsync(ct),
            await db.HrLeaveRequests.AsNoTracking().CountAsync(ct),
            await db.LeaveBalances.AsNoTracking().CountAsync(ct),
            await db.ApprovalInstances.AsNoTracking().CountAsync(ct),
            await db.HrPayrollRuns.AsNoTracking().CountAsync(ct),
            await db.EmployeePayrolls.AsNoTracking().CountAsync(ct),
            await db.EmployeeCompensations.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.hr.operations"]);
    }
}
