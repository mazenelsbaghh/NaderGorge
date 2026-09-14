using System.Data;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Attendance.Commands;

public sealed record CancelAttendanceEventCommand(Guid SessionId, AttendanceEventType EventType,
    int ExpectedVersion, string Reason, Guid ActorUserId) : IRequest<ApiResponse<bool>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.AttendanceManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
}

public sealed class CancelAttendanceEventCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null,
    ILiveSupportService? liveSupport = null, ILiveSupportAssignmentCoordinator? coordinator = null)
    : IRequestHandler<CancelAttendanceEventCommand, ApiResponse<bool>>
{
    private readonly IHrAuditWriter _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance);

    public async Task<ApiResponse<bool>> Handle(CancelAttendanceEventCommand request, CancellationToken ct)
    {
        if (!await db.UserRoles.AnyAsync(x => x.UserId == request.ActorUserId && x.Role.Type == RoleType.Admin, ct))
            return ApiResponse<bool>.Fail("إلغاء الحضور والانصراف متاح للأدمن فقط", ["ADMIN_REQUIRED"]);
        if (request.EventType is not (AttendanceEventType.ClockIn or AttendanceEventType.ClockOut)
            || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            return ApiResponse<bool>.Fail("حدد الحضور أو الانصراف واكتب سبب الإلغاء (حتى 1000 حرف)", ["CANCELLATION_INVALID"]);

        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var session = await db.AttendanceSessions.Include(x => x.Employee).Include(x => x.Breaks)
            .SingleOrDefaultAsync(x => x.Id == request.SessionId, ct);
        if (session is null) return ApiResponse<bool>.Fail("جلسة الحضور غير موجودة", ["ATTENDANCE_SESSION_NOT_FOUND"]);
        if (session.Version != request.ExpectedVersion)
            return ApiResponse<bool>.Fail("السجل اتغير؛ حدّث الصفحة وأعد المحاولة", ["CONCURRENCY_CONFLICT"]);
        if (session.State == AttendanceSessionState.Cancelled ||
            (request.EventType == AttendanceEventType.ClockOut && !session.ClockedOutAt.HasValue))
            return ApiResponse<bool>.Fail("التسجيل ملغى بالفعل أو غير موجود", ["ATTENDANCE_EVENT_NOT_ACTIVE"]);
        if (await db.EmployeePayrolls.AnyAsync(x => x.EmployeeId == session.EmployeeId
            && x.PayrollRun!.PeriodStart <= session.WorkDate && x.PayrollRun.PeriodEnd >= session.WorkDate, ct))
            return ApiResponse<bool>.Fail("اليوم مدرج في كشف رواتب جاهز؛ يلزم تصحيح كشف الرواتب قبل تغيير الحضور", ["ATTENDANCE_PAYROLL_LOCKED"]);
        if (request.EventType == AttendanceEventType.ClockOut && await db.AttendanceSessions.AnyAsync(x =>
            x.EmployeeId == session.EmployeeId && x.Id != session.Id && x.State != AttendanceSessionState.Cancelled
            && (x.State == AttendanceSessionState.Open || x.ClockedInAt >= session.ClockedInAt), ct))
            return ApiResponse<bool>.Fail("توجد جلسة أحدث أو مفتوحة؛ لا يمكن إعادة فتح هذه الجلسة", ["ATTENDANCE_LATER_SESSION_EXISTS"]);

        var wasOpen = session.State == AttendanceSessionState.Open;
        var before = new { session.ClockedInAt, session.ClockedOutAt, session.State, session.Version,
            session.WorkedMinutes, session.LateMinutes, session.EarlyLeaveMinutes, session.OvertimeMinutes };
        ApplyCancellation(session, request.EventType);
        var pending = await db.AttendanceCorrections.Where(x => x.AttendanceSessionId == session.Id
            && (x.State == AttendanceCorrectionState.PendingManager || x.State == AttendanceCorrectionState.PendingHr)).ToListAsync(ct);
        foreach (var correction in pending)
        {
            correction.State = AttendanceCorrectionState.Withdrawn;
            correction.DecisionReason = $"إلغاء تسجيل بواسطة الأدمن: {request.Reason.Trim()}";
            correction.Version++;
        }
        await _audit.WriteMutationAsync(request.EventType == AttendanceEventType.ClockIn ? "CancelAttendanceClockIn" : "CancelAttendanceClockOut",
            nameof(AttendanceSession), session.Id, before,
            new { session.ClockedInAt, session.ClockedOutAt, session.State, session.Version, withdrawnCorrections = pending.Select(x => x.Id).ToArray() },
            request.Reason.Trim(), ct, request.ActorUserId);
        if ((wasOpen || request.EventType == AttendanceEventType.ClockOut)
            && await db.LiveSupportStaffConfigs.AnyAsync(x => x.UserId == session.Employee!.UserId && x.IsEnabled, ct))
            db.OutboxEvents.Add(new OutboxEvent
            {
                Type = "LiveSupportEvent", TargetGroup = $"LiveSupport:Staff:{session.Employee!.UserId:N}",
                PayloadJson = JsonSerializer.Serialize(new { eventId = Guid.NewGuid(), occurredAt = DateTime.UtcNow,
                    type = "StaffEligibilityChanged", payload = new { userId = session.Employee.UserId,
                        checkedIn = request.EventType == AttendanceEventType.ClockOut } })
            });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (wasOpen && request.EventType == AttendanceEventType.ClockIn && liveSupport is not null)
            await liveSupport.ReleaseStaffAssignmentsAsync(session.Employee!.UserId, LiveSupportAssignmentEndReason.AttendanceCheckout, ct);
        if (request.EventType == AttendanceEventType.ClockOut && coordinator is not null)
            await coordinator.AssignWaitingAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    private static void ApplyCancellation(AttendanceSession session, AttendanceEventType eventType)
    {
        if (eventType == AttendanceEventType.ClockIn)
        {
            session.State = AttendanceSessionState.Cancelled;
            session.LateMinutes = 0;
        }
        else
        {
            session.ClockedOutAt = null;
            session.State = AttendanceSessionState.Open;
        }
        session.WorkedMinutes = 0;
        session.EarlyLeaveMinutes = 0;
        session.OvertimeMinutes = 0;
        session.Version++;
        session.UpdatedAt = DateTime.UtcNow;
    }
}
