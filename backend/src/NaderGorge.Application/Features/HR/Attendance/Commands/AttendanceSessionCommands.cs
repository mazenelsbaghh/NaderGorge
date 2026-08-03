using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Scheduling;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Attendance.Commands;

public sealed record AttendanceMutationResult(Guid SessionId, Guid AttemptId, string DecisionCode, DateOnly WorkDate);
public sealed record ClockInAttendanceCommand(Guid UserId, string IdempotencyKey, DateTime OccurredAt, double? Latitude,
    double? Longitude, double? AccuracyMeters, string? DeviceToken, string IpAddress, string UserAgent)
    : IRequest<ApiResponse<AttendanceMutationResult>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.AttendanceSelf;
    public HrAccessScope RequiredScope => HrAccessScope.Self;
    public Guid? ResourceUserId => UserId;
}

public sealed class ClockInAttendanceCommandHandler : IRequestHandler<ClockInAttendanceCommand, ApiResponse<AttendanceMutationResult>>
{
    private readonly IAppDbContext _db; private readonly AttendancePolicyEvaluator _evaluator; private readonly IHrAuditWriter _audit;
    private readonly ILiveSupportAssignmentCoordinator? _coordinator;
    public ClockInAttendanceCommandHandler(IAppDbContext db, AttendancePolicyEvaluator evaluator,
        IHrAuditWriter? audit = null, ILiveSupportAssignmentCoordinator? coordinator = null)
    { _db = db; _evaluator = evaluator; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance); _coordinator = coordinator; }

    public async Task<ApiResponse<AttendanceMutationResult>> Handle(ClockInAttendanceCommand request, CancellationToken ct)
    {
        var employee = await _db.EmployeeProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == request.UserId, ct)
            ?? throw new KeyNotFoundException("Employee profile not found");
        var replay = await _db.AttendanceAttempts.AsNoTracking().SingleOrDefaultAsync(item => item.EmployeeId == employee.Id &&
            item.EventType == AttendanceEventType.ClockIn && item.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null)
        {
            if (!replay.Accepted || !replay.AttendanceSessionId.HasValue) return ApiResponse<AttendanceMutationResult>.Fail("تم رفض المحاولة السابقة", [replay.DecisionCode]);
            var replaySession = await _db.AttendanceSessions.AsNoTracking().SingleAsync(item => item.Id == replay.AttendanceSessionId, ct);
            return ApiResponse<AttendanceMutationResult>.Ok(new(replaySession.Id, replay.Id, replay.DecisionCode, replaySession.WorkDate));
        }
        var cairo = ResolveCairo();
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(request.OccurredAt), cairo));
        var shift = await _db.ShiftAssignments.AsNoTracking().Include(item => item.ShiftTemplate).ThenInclude(item => item!.Segments)
            .Where(item => item.EmployeeId == employee.Id && item.Status == ShiftAssignmentStatus.Published && item.EffectiveFrom <= localDate && (!item.EffectiveTo.HasValue || item.EffectiveTo > localDate))
            .OrderByDescending(item => item.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (shift is null) return await RejectAsync(employee.Id, request, "NO_SCHEDULE", null, ct);
        var evaluation = await _evaluator.EvaluateAsync(new(employee.Id, shift.ShiftTemplateId, EnsureUtc(request.OccurredAt), request.Latitude, request.Longitude, request.AccuracyMeters, request.DeviceToken), ct);
        if (!evaluation.Accepted) return await RejectAsync(employee.Id, request, evaluation.Code, evaluation.PolicyId, ct);
        if (await _db.AttendanceSessions.AnyAsync(item => item.EmployeeId == employee.Id && item.State == AttendanceSessionState.Open, ct))
            return await RejectAsync(employee.Id, request, "SESSION_ALREADY_OPEN", evaluation.PolicyId, ct);
        var segment = ShiftScheduleRules.SegmentForWorkDate(shift.ShiftTemplate!.Segments, localDate);
        if (segment is null) return await RejectAsync(employee.Id, request, "NO_SCHEDULE", evaluation.PolicyId, ct);
        var workDate = ShiftWorkDateResolver.Resolve(EnsureUtc(request.OccurredAt), segment, cairo);
        var (scheduledStart, _) = ShiftScheduleRules.ScheduledRangeUtc(workDate, segment, cairo);
        var lateMinutes = Math.Max(0,
            (int)(EnsureUtc(request.OccurredAt) - scheduledStart).TotalMinutes - shift.ShiftTemplate.GraceMinutes);
        var session = new AttendanceSession
        {
            EmployeeId = employee.Id,
            ShiftAssignmentId = shift.Id,
            WorkDate = workDate,
            ClockedInAt = EnsureUtc(request.OccurredAt),
            LateMinutes = lateMinutes,
        };
        var attempt = NewAttempt(employee.Id, request, true, evaluation.Code, evaluation.PolicyId); attempt.AttendanceSessionId = session.Id;
        _db.AttendanceSessions.Add(session); _db.AttendanceAttempts.Add(attempt);
        if (await _db.LiveSupportStaffConfigs.AnyAsync(item => item.UserId == request.UserId && item.IsEnabled, ct))
            _db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = $"LiveSupport:Staff:{request.UserId:N}", PayloadJson = JsonSerializer.Serialize(new { eventId = Guid.NewGuid(), occurredAt = request.OccurredAt, type = "StaffEligibilityChanged", payload = new { userId = request.UserId, checkedIn = true } }) });
        await _audit.WriteMutationAsync("AttendanceClockIn", nameof(AttendanceSession), session.Id, null,
            new { session.WorkDate, session.ClockedInAt, policy = evaluation.PolicyId }, "Employee clock-in accepted", ct, request.UserId);
        await _db.SaveChangesAsync(ct);
        if (_coordinator is not null) await _coordinator.AssignWaitingAsync(ct);
        return ApiResponse<AttendanceMutationResult>.Ok(new(session.Id, attempt.Id, evaluation.Code, session.WorkDate));
    }

    private async Task<ApiResponse<AttendanceMutationResult>> RejectAsync(Guid employeeId, ClockInAttendanceCommand request, string code, Guid? policyId, CancellationToken ct)
    {
        var attempt = NewAttempt(employeeId, request, false, code, policyId); _db.AttendanceAttempts.Add(attempt);
        await _audit.WriteMutationAsync("AttendanceClockInRejected", nameof(AttendanceAttempt), attempt.Id, null,
            new { attempt.EventType, attempt.DecisionCode, attempt.OccurredAt }, code, ct, request.UserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<AttendanceMutationResult>.Fail("تعذر تسجيل الحضور", [code]);
    }

    private static AttendanceAttempt NewAttempt(Guid employeeId, ClockInAttendanceCommand request, bool accepted, string code, Guid? policyId) => new()
    {
        EmployeeId = employeeId, EventType = AttendanceEventType.ClockIn, OccurredAt = EnsureUtc(request.OccurredAt), Accepted = accepted,
        DecisionCode = code, IdempotencyKey = request.IdempotencyKey, AttendancePolicyId = policyId,
        EvidenceJson = JsonSerializer.Serialize(new { request.Latitude, request.Longitude, request.AccuracyMeters,
            deviceTokenHash = string.IsNullOrWhiteSpace(request.DeviceToken) ? null : AttendancePolicyEvaluator.HashToken(request.DeviceToken), request.IpAddress, request.UserAgent })
    };
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static TimeZoneInfo ResolveCairo() { try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); } catch { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); } }
}

public sealed record StartAttendanceBreakCommand(Guid UserId, string IdempotencyKey, DateTime OccurredAt, AttendanceBreakKind Kind)
    : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.AttendanceSelf; public HrAccessScope RequiredScope => HrAccessScope.Self; public Guid? ResourceUserId => UserId;
}
public sealed record EndAttendanceBreakCommand(Guid UserId, Guid BreakId, string IdempotencyKey, DateTime OccurredAt)
    : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.AttendanceSelf; public HrAccessScope RequiredScope => HrAccessScope.Self; public Guid? ResourceUserId => UserId;
}
public sealed record ClockOutAttendanceCommand(Guid UserId, string IdempotencyKey, DateTime OccurredAt)
    : IRequest<ApiResponse<AttendanceMutationResult>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.AttendanceSelf; public HrAccessScope RequiredScope => HrAccessScope.Self; public Guid? ResourceUserId => UserId;
}

public sealed class StartAttendanceBreakCommandHandler : IRequestHandler<StartAttendanceBreakCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db; private readonly IHrAuditWriter _audit; private readonly ILiveSupportService? _liveSupport;
    public StartAttendanceBreakCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null, ILiveSupportService? liveSupport = null) { _db = db; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance); _liveSupport = liveSupport; }
    public async Task<ApiResponse<Guid>> Handle(StartAttendanceBreakCommand request, CancellationToken ct)
    {
        var employee = await _db.EmployeeProfiles.SingleAsync(item => item.UserId == request.UserId, ct);
        var employeeId = employee.Id;
        var replay = await _db.AttendanceAttempts.AsNoTracking().FirstOrDefaultAsync(item => item.EmployeeId == employeeId && item.EventType == AttendanceEventType.BreakStart && item.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay?.Accepted == true)
            return ApiResponse<Guid>.Ok(await _db.AttendanceBreaks.Where(item => item.AttendanceSessionId == replay.AttendanceSessionId && item.StartedAt == replay.OccurredAt).Select(item => item.Id).SingleAsync(ct));
        var session = await _db.AttendanceSessions
            .Include(item => item.Breaks)
            .Include(item => item.ShiftAssignment!).ThenInclude(item => item.ShiftTemplate!).ThenInclude(item => item.Segments)
            .SingleOrDefaultAsync(item => item.EmployeeId == employeeId && item.State == AttendanceSessionState.Open, ct);
        if (session is null) return ApiResponse<Guid>.Fail("لا توجد جلسة حضور مفتوحة", ["NO_OPEN_SESSION"]);
        if (session.Breaks.Any(item => !item.EndedAt.HasValue)) return ApiResponse<Guid>.Fail("توجد استراحة مفتوحة", ["BREAK_ALREADY_OPEN"]);
        var allowedMinutes = request.Kind == AttendanceBreakKind.ShortPermission ? employee.ShortPermissionMaxMinutes : employee.DailyBreakAllowanceMinutes;
        var dailyAllowance = request.Kind == AttendanceBreakKind.ShortPermission ? employee.DailyShortPermissionAllowanceMinutes : employee.DailyBreakAllowanceMinutes;
        var usedMinutes = session.Breaks.Where(item => item.Kind == request.Kind && item.EndedAt.HasValue).Sum(item => (int)(item.EndedAt!.Value - item.StartedAt).TotalMinutes);
        if (allowedMinutes <= 0 || dailyAllowance <= usedMinutes) return ApiResponse<Guid>.Fail("لا يوجد رصيد متاح لهذا النوع من الاستراحة", ["BREAK_ALLOWANCE_EXHAUSTED"]);
        allowedMinutes = Math.Min(allowedMinutes, dailyAllowance - usedMinutes);
        var occurredAt = EnsureUtc(request.OccurredAt); var attendanceBreak = new AttendanceBreak { AttendanceSessionId = session.Id, StartedAt = occurredAt, Kind = request.Kind, AllowedMinutes = allowedMinutes };
        _db.AttendanceBreaks.Add(attendanceBreak); _db.AttendanceAttempts.Add(new AttendanceAttempt { EmployeeId = employeeId, EventType = AttendanceEventType.BreakStart, OccurredAt = occurredAt, Accepted = true, DecisionCode = "ATTENDANCE_ACCEPTED", IdempotencyKey = request.IdempotencyKey, AttendanceSessionId = session.Id });
        await _audit.WriteMutationAsync("AttendanceBreakStart", nameof(AttendanceBreak), attendanceBreak.Id, null, new { attendanceBreak.StartedAt, attendanceBreak.Kind, attendanceBreak.AllowedMinutes }, "Employee break start", ct, request.UserId);
        await _db.SaveChangesAsync(ct);
        if (_liveSupport is not null) await _liveSupport.ReleaseStaffAssignmentsAsync(request.UserId, LiveSupportAssignmentEndReason.AttendanceCheckout, ct);
        return ApiResponse<Guid>.Ok(attendanceBreak.Id);
    }
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public sealed class EndAttendanceBreakCommandHandler : IRequestHandler<EndAttendanceBreakCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db; private readonly IHrAuditWriter _audit; private readonly ILiveSupportAssignmentCoordinator? _coordinator;
    public EndAttendanceBreakCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null, ILiveSupportAssignmentCoordinator? coordinator = null) { _db = db; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance); _coordinator = coordinator; }
    public async Task<ApiResponse<Guid>> Handle(EndAttendanceBreakCommand request, CancellationToken ct)
    {
        var employeeId = await _db.EmployeeProfiles.Where(item => item.UserId == request.UserId).Select(item => item.Id).SingleAsync(ct);
        var attendanceBreak = await _db.AttendanceBreaks.Include(item => item.AttendanceSession).SingleOrDefaultAsync(item => item.Id == request.BreakId && item.AttendanceSession!.EmployeeId == employeeId, ct);
        if (attendanceBreak is null || attendanceBreak.EndedAt.HasValue) return ApiResponse<Guid>.Fail("لا توجد استراحة مفتوحة", ["NO_OPEN_BREAK"]);
        attendanceBreak.EndedAt = request.OccurredAt.Kind == DateTimeKind.Utc ? request.OccurredAt : request.OccurredAt.ToUniversalTime(); attendanceBreak.Version++;
        _db.AttendanceAttempts.Add(new AttendanceAttempt { EmployeeId = employeeId, EventType = AttendanceEventType.BreakEnd, OccurredAt = attendanceBreak.EndedAt.Value, Accepted = true, DecisionCode = "ATTENDANCE_ACCEPTED", IdempotencyKey = request.IdempotencyKey, AttendanceSessionId = attendanceBreak.AttendanceSessionId });
        await _audit.WriteMutationAsync("AttendanceBreakEnd", nameof(AttendanceBreak), attendanceBreak.Id, new { endedAt = (DateTime?)null }, new { attendanceBreak.EndedAt }, "Employee break end", ct, request.UserId);
        await _db.SaveChangesAsync(ct); if (_coordinator is not null) await _coordinator.AssignWaitingAsync(ct); return ApiResponse<Guid>.Ok(attendanceBreak.Id);
    }
}

public sealed class ClockOutAttendanceCommandHandler : IRequestHandler<ClockOutAttendanceCommand, ApiResponse<AttendanceMutationResult>>
{
    private readonly IAppDbContext _db; private readonly IHrAuditWriter _audit; private readonly ILiveSupportService? _liveSupport;
    public ClockOutAttendanceCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null, ILiveSupportService? liveSupport = null) { _db = db; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance); _liveSupport = liveSupport; }
    public async Task<ApiResponse<AttendanceMutationResult>> Handle(ClockOutAttendanceCommand request, CancellationToken ct)
    {
        var employeeId = await _db.EmployeeProfiles.Where(item => item.UserId == request.UserId).Select(item => item.Id).SingleAsync(ct);
        var replay = await _db.AttendanceAttempts.AsNoTracking().SingleOrDefaultAsync(item => item.EmployeeId == employeeId && item.EventType == AttendanceEventType.ClockOut && item.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay?.AttendanceSessionId is { } replaySessionId)
        {
            var prior = await _db.AttendanceSessions.AsNoTracking().SingleAsync(item => item.Id == replaySessionId, ct);
            return ApiResponse<AttendanceMutationResult>.Ok(new(prior.Id, replay.Id, replay.DecisionCode, prior.WorkDate));
        }
        var session = await _db.AttendanceSessions.Include(item => item.Breaks).SingleOrDefaultAsync(item => item.EmployeeId == employeeId && item.State == AttendanceSessionState.Open, ct);
        if (session is null) return ApiResponse<AttendanceMutationResult>.Fail("لا توجد جلسة مفتوحة", ["NO_OPEN_SESSION"]);
        if (session.Breaks.Any(item => !item.EndedAt.HasValue)) return ApiResponse<AttendanceMutationResult>.Fail("أنهِ الاستراحة أولًا", ["BREAK_ALREADY_OPEN"]);
        var occurredAt = request.OccurredAt.Kind == DateTimeKind.Utc ? request.OccurredAt : request.OccurredAt.ToUniversalTime();
        if (occurredAt <= session.ClockedInAt) return ApiResponse<AttendanceMutationResult>.Fail("وقت الانصراف غير صالح", ["ATTENDANCE_TIME_INVALID"]);
        session.ClockedOutAt = occurredAt; session.State = AttendanceSessionState.Completed; session.Version++;
        var breakMinutes = session.Breaks.Where(item => item.EndedAt.HasValue).Sum(item => (int)(item.EndedAt!.Value - item.StartedAt).TotalMinutes);
        var segment = ShiftScheduleRules.SegmentForWorkDate(session.ShiftAssignment!.ShiftTemplate!.Segments, session.WorkDate)
            ?? throw new InvalidOperationException("Attendance shift segment is missing.");
        var (scheduledStart, scheduledEnd) = ShiftScheduleRules.ScheduledRangeUtc(session.WorkDate, segment, ResolveCairo());
        var calculation = AttendanceCalculator.Calculate(new(
            session.ClockedInAt,
            occurredAt,
            scheduledStart,
            scheduledEnd,
            breakMinutes,
            session.ShiftAssignment.ShiftTemplate.GraceMinutes,
            session.ShiftAssignment.ShiftTemplate.OvertimeAfterMinutes));
        session.WorkedMinutes = calculation.WorkedMinutes;
        session.LateMinutes = calculation.LateMinutes;
        session.EarlyLeaveMinutes = calculation.EarlyLeaveMinutes;
        session.OvertimeMinutes = calculation.OvertimeMinutes;
        var attempt = new AttendanceAttempt { EmployeeId = employeeId, EventType = AttendanceEventType.ClockOut, OccurredAt = occurredAt, Accepted = true, DecisionCode = "ATTENDANCE_ACCEPTED", IdempotencyKey = request.IdempotencyKey, AttendanceSessionId = session.Id };
        _db.AttendanceAttempts.Add(attempt);
        await _audit.WriteMutationAsync("AttendanceClockOut", nameof(AttendanceSession), session.Id, new { clockedOutAt = (DateTime?)null }, new { session.ClockedOutAt, session.WorkedMinutes }, "Employee clock-out", ct, request.UserId);
        await _db.SaveChangesAsync(ct);
        if (_liveSupport is not null) await _liveSupport.ReleaseStaffAssignmentsAsync(request.UserId, LiveSupportAssignmentEndReason.AttendanceCheckout, ct);
        return ApiResponse<AttendanceMutationResult>.Ok(new(session.Id, attempt.Id, attempt.DecisionCode, session.WorkDate));
    }
    private static TimeZoneInfo ResolveCairo() { try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); } catch { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); } }
}

public sealed record RegisterTrustedAttendanceDeviceCommand(Guid EmployeeId, string DeviceToken, string Name, DateTime? ExpiresAt, Guid ActorUserId)
    : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.AttendanceManage; public HrAccessScope RequiredScope => HrAccessScope.All; public Guid? ResourceEmployeeId => EmployeeId;
}
public sealed class RegisterTrustedAttendanceDeviceCommandHandler : IRequestHandler<RegisterTrustedAttendanceDeviceCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db; private readonly IHrAuditWriter _audit;
    public RegisterTrustedAttendanceDeviceCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null) { _db = db; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance); }
    public async Task<ApiResponse<Guid>> Handle(RegisterTrustedAttendanceDeviceCommand request, CancellationToken ct)
    {
        if (!await _db.EmployeeProfiles.AnyAsync(item => item.Id == request.EmployeeId, ct)) return ApiResponse<Guid>.Fail("الموظف غير موجود", ["EMPLOYEE_NOT_FOUND"]);
        var hash = AttendancePolicyEvaluator.HashToken(request.DeviceToken);
        if (await _db.TrustedAttendanceDevices.AnyAsync(item => item.EmployeeId == request.EmployeeId && item.TokenHash == hash, ct)) return ApiResponse<Guid>.Fail("الجهاز مسجل بالفعل", ["TRUSTED_DEVICE_EXISTS"]);
        var device = new TrustedAttendanceDevice { EmployeeId = request.EmployeeId, TokenHash = hash, Name = request.Name.Trim(), ExpiresAt = request.ExpiresAt, ApprovedByUserId = request.ActorUserId };
        _db.TrustedAttendanceDevices.Add(device);
        await _audit.WriteMutationAsync("RegisterTrustedAttendanceDevice", nameof(TrustedAttendanceDevice), device.Id, null,
            new { device.EmployeeId, device.Name, device.ExpiresAt }, "Approve trusted attendance device", ct, request.ActorUserId);
        await _db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(device.Id);
    }
}
