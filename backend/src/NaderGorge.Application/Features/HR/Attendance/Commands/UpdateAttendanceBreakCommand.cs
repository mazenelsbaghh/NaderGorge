using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Scheduling;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Attendance.Commands;

public sealed record UpdateAttendanceBreakCommand(Guid BreakId, DateTime StartedAt, DateTime? EndedAt, Guid ActorUserId)
    : IRequest<ApiResponse<bool>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.AttendanceManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
}

public sealed class UpdateAttendanceBreakCommandHandler : IRequestHandler<UpdateAttendanceBreakCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IHrAuditWriter _audit;

    public UpdateAttendanceBreakCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null)
    {
        _db = db;
        _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance);
    }

    public async Task<ApiResponse<bool>> Handle(UpdateAttendanceBreakCommand request, CancellationToken ct)
    {
        var attendanceBreak = await _db.AttendanceBreaks.Include(entry => entry.AttendanceSession!)
            .ThenInclude(session => session.ShiftAssignment!).ThenInclude(assignment => assignment!.ShiftTemplate!)
            .ThenInclude(template => template!.Segments).Include(entry => entry.AttendanceSession!.Breaks)
            .SingleOrDefaultAsync(entry => entry.Id == request.BreakId, ct);
        if (attendanceBreak is null) return ApiResponse<bool>.Fail("سجل البريك غير موجود", ["BREAK_NOT_FOUND"]);

        var startedAt = ToUtc(request.StartedAt);
        DateTime? endedAt = request.EndedAt.HasValue ? ToUtc(request.EndedAt.Value) : null;
        var session = attendanceBreak.AttendanceSession!;
        if (!IsValidRange(session, startedAt, endedAt)) return ApiResponse<bool>.Fail("وقت البريك خارج نطاق جلسة الدوام", ["BREAK_TIME_INVALID"]);

        var before = new { attendanceBreak.StartedAt, attendanceBreak.EndedAt, attendanceBreak.Version };
        attendanceBreak.StartedAt = startedAt;
        attendanceBreak.EndedAt = endedAt;
        attendanceBreak.Version++;
        RecalculateCompletedSession(session);
        await _audit.WriteMutationAsync("UpdateAttendanceBreak", nameof(AttendanceBreak), attendanceBreak.Id, before,
            new { attendanceBreak.StartedAt, attendanceBreak.EndedAt, attendanceBreak.Version }, "Admin break time adjustment", ct, request.ActorUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    private static bool IsValidRange(AttendanceSession session, DateTime startedAt, DateTime? endedAt)
    {
        var now = DateTime.UtcNow;
        return startedAt >= session.ClockedInAt && startedAt <= now &&
            (!endedAt.HasValue || (endedAt > startedAt && endedAt <= now)) &&
            (!session.ClockedOutAt.HasValue || (endedAt.HasValue && endedAt <= session.ClockedOutAt));
    }

    private static void RecalculateCompletedSession(AttendanceSession session)
    {
        if (!session.ClockedOutAt.HasValue || session.ShiftAssignment?.ShiftTemplate is not { } template) return;
        var segment = ShiftScheduleRules.SegmentForWorkDate(template.Segments, session.WorkDate);
        if (segment is null) return;
        var (scheduledStart, scheduledEnd) = ShiftScheduleRules.ScheduledRangeUtc(session.WorkDate, segment, ResolveCairo());
        var breakMinutes = session.Breaks.Where(entry => entry.EndedAt.HasValue).Sum(entry => (int)(entry.EndedAt!.Value - entry.StartedAt).TotalMinutes);
        var calculation = AttendanceCalculator.Calculate(new(session.ClockedInAt, session.ClockedOutAt.Value, scheduledStart, scheduledEnd,
            breakMinutes, template.GraceMinutes, template.OvertimeAfterMinutes));
        session.WorkedMinutes = calculation.WorkedMinutes;
        session.LateMinutes = calculation.LateMinutes;
        session.EarlyLeaveMinutes = calculation.EarlyLeaveMinutes;
        session.OvertimeMinutes = calculation.OvertimeMinutes;
        session.Version++;
    }

    private static DateTime ToUtc(DateTime dateTime) => dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
    private static TimeZoneInfo ResolveCairo() { try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); } catch { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); } }
}
