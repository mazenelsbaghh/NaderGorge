using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.People;

public sealed record DeleteEmployeeProfileCommand(Guid EmployeeId, Guid ActorUserId)
    : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.EmployeeManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
}

public sealed class DeleteEmployeeProfileCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null)
    : IRequestHandler<DeleteEmployeeProfileCommand, ApiResponse<Guid>>
{
    private readonly IHrAuditWriter _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance);

    public async Task<ApiResponse<Guid>> Handle(DeleteEmployeeProfileCommand request, CancellationToken ct)
    {
        var employee = await db.EmployeeProfiles.SingleOrDefaultAsync(item => item.Id == request.EmployeeId, ct);
        if (employee is null) return ApiResponse<Guid>.Fail("الموظف غير موجود", ["EMPLOYEE_NOT_FOUND"]);

        if (await HasEmploymentHistoryAsync(request.EmployeeId, ct))
            return ApiResponse<Guid>.Fail("لا يمكن حذف موظف لديه سجل تشغيلي أو مالي", ["EMPLOYEE_DELETE_BLOCKED"]);

        var shiftAssignments = await db.ShiftAssignments.Where(item => item.EmployeeId == request.EmployeeId).ToListAsync(ct);
        var shiftAssignmentIds = shiftAssignments.Select(item => item.Id).ToList();
        if (await db.ShiftSwapRequests.AnyAsync(item => item.RequesterEmployeeId == request.EmployeeId || item.TargetEmployeeId == request.EmployeeId ||
            shiftAssignmentIds.Contains(item.RequesterAssignmentId) || shiftAssignmentIds.Contains(item.TargetAssignmentId), ct))
            return ApiResponse<Guid>.Fail("لا يمكن حذف موظف لديه طلبات تبديل شفت", ["EMPLOYEE_DELETE_BLOCKED"]);

        db.ShiftAssignments.RemoveRange(shiftAssignments);
        db.AttendancePolicyAssignments.RemoveRange(await db.AttendancePolicyAssignments.Where(item => item.EmployeeId == request.EmployeeId).ToListAsync(ct));
        db.TrustedAttendanceDevices.RemoveRange(await db.TrustedAttendanceDevices.Where(item => item.EmployeeId == request.EmployeeId).ToListAsync(ct));
        db.EmployeeProfiles.Remove(employee);
        await _audit.WriteMutationAsync("DeleteEmployeeProfile", nameof(EmployeeProfile), employee.Id, new { employee.UserId, employee.EmployeeNumber }, null,
            "Delete an employee profile with no operating history", ct, request.ActorUserId);
        await db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(employee.Id);
    }

    private Task<bool> HasEmploymentHistoryAsync(Guid employeeId, CancellationToken ct) => Task.WhenAll(
        db.EmploymentAssignments.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.EmploymentContracts.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.AttendanceSessions.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.AttendanceAttempts.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.AttendancePolicyExceptions.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.WorkdayClassifications.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.AttendanceCorrections.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.LeaveBalances.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.HrLeaveRequests.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.EmployeeCompensations.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.EmployeePayrolls.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.HrFinancialRequests.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.EmployeeDocuments.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.AssetCustodies.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.PerformanceReviews.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.EmployeeCases.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.EmployeeLifecycleTasks.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.OffboardingProcesses.AnyAsync(item => item.EmployeeId == employeeId, ct),
        db.OrganizationUnits.AnyAsync(item => item.ManagerEmployeeId == employeeId, ct),
        db.Candidates.AnyAsync(item => item.EmployeeProfileId == employeeId, ct),
        db.AttendanceLogs.AnyAsync(item => item.EmployeeId == employeeId, ct))
        .ContinueWith(checks => checks.Result.Any(value => value), ct);
}
