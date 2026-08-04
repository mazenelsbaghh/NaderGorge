using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Attendance.Commands;
using NaderGorge.Application.Features.HR.Attendance;
using NaderGorge.Application.Features.HR.Attendance.Corrections;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/hr")]
[Authorize]
public sealed class HrAttendanceController : ControllerBase
{
    private readonly IAppDbContext _db; private readonly IMediator _mediator;
    public HrAttendanceController(IAppDbContext db, IMediator mediator) { _db = db; _mediator = mediator; }

    [HttpGet("self/attendance/today")]
    [HasPermission(HrPermissions.AttendanceSelf)]
    public async Task<IActionResult> Today(CancellationToken ct)
    {
        var userId = User.RequireUserId();
        if (await IsGeneralAdminAsync(userId, ct)) return AdminAttendanceNotApplicable();
        var today = CairoTime.GetCurrentDate();
        var session = await _db.AttendanceSessions.AsNoTracking().Where(item =>
                item.Employee!.UserId == userId &&
                (item.State == AttendanceSessionState.Open || item.WorkDate == today))
            .OrderByDescending(item => item.State == AttendanceSessionState.Open)
            .ThenByDescending(item => item.ClockedInAt).Select(item => new
            {
                item.Id, item.WorkDate, item.ClockedInAt, item.ClockedOutAt, state = item.State.ToString(),
                item.WorkedMinutes, item.LateMinutes, item.EarlyLeaveMinutes, item.OvertimeMinutes,
                breakAllowanceMinutes = item.Employee!.DailyBreakAllowanceMinutes,
                shortPermissionMaxMinutes = item.Employee.ShortPermissionMaxMinutes,
                dailyShortPermissionAllowanceMinutes = item.Employee.DailyShortPermissionAllowanceMinutes,
                breaks = item.Breaks.OrderBy(row => row.StartedAt).Select(row => new { row.Id, row.StartedAt, row.EndedAt, kind = row.Kind.ToString(), row.AllowedMinutes })
            }).FirstOrDefaultAsync(ct);
        return Ok(session);
    }

    [HttpGet("self/attendance")]
    [HasPermission(HrPermissions.AttendanceSelf)]
    public async Task<IActionResult> History(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var userId = User.RequireUserId();
        if (await IsGeneralAdminAsync(userId, ct)) return AdminAttendanceNotApplicable();
        return Ok(await _db.AttendanceSessions.AsNoTracking().Where(item => item.Employee!.UserId == userId &&
            (!from.HasValue || item.WorkDate >= from) && (!to.HasValue || item.WorkDate <= to)).OrderByDescending(item => item.WorkDate)
            .Select(item => new { item.Id, item.WorkDate, item.ClockedInAt, item.ClockedOutAt, state = item.State.ToString(), item.WorkedMinutes, item.LateMinutes, item.EarlyLeaveMinutes, item.OvertimeMinutes }).ToListAsync(ct));
    }

    [HttpPost("self/attendance/corrections")]
    [HasPermission(HrPermissions.AttendanceSelf)]
    public async Task<IActionResult> SubmitCorrection(SubmitAttendanceCorrectionRequest request, CancellationToken ct)
    {
        if (await IsGeneralAdminAsync(User.RequireUserId(), ct)) return AdminAttendanceNotApplicable();
        var result = await _mediator.Send(new SubmitAttendanceCorrectionCommand(User.RequireUserId(), request.AttendanceSessionId,
            request.ProposedClockedInAt, request.ProposedClockedOutAt, request.Reason, request.EvidenceReference), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("self/attendance/clock-in")]
    [HasPermission(HrPermissions.AttendanceSelf)]
    public async Task<IActionResult> ClockIn(AttendanceEvidenceRequest request, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        if (await IsGeneralAdminAsync(User.RequireUserId(), ct)) return AdminAttendanceNotApplicable();
        if (string.IsNullOrWhiteSpace(key)) return BadRequest(new { errors = new[] { "IDEMPOTENCY_KEY_REQUIRED" } });
        var result = await _mediator.Send(new ClockInAttendanceCommand(User.RequireUserId(), key, DateTime.UtcNow,
            request.Latitude, request.Longitude, request.Accuracy, request.DeviceToken, ClientIp(), Request.Headers.UserAgent.ToString()), ct);
        return result.Success ? Ok(result) : StatusCode(StatusCodes.Status403Forbidden, result);
    }

    [HttpPost("self/attendance/breaks/start")]
    [HasPermission(HrPermissions.AttendanceSelf)]
    public async Task<IActionResult> StartBreak(StartAttendanceBreakRequest request, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        if (await IsGeneralAdminAsync(User.RequireUserId(), ct)) return AdminAttendanceNotApplicable();
        if (string.IsNullOrWhiteSpace(key)) return BadRequest();
        var result = await _mediator.Send(new StartAttendanceBreakCommand(User.RequireUserId(), key, DateTime.UtcNow, request.Kind), ct);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("self/attendance/breaks/{breakId:guid}/end")]
    [HasPermission(HrPermissions.AttendanceSelf)]
    public async Task<IActionResult> EndBreak(Guid breakId, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        if (await IsGeneralAdminAsync(User.RequireUserId(), ct)) return AdminAttendanceNotApplicable();
        if (string.IsNullOrWhiteSpace(key)) return BadRequest();
        var result = await _mediator.Send(new EndAttendanceBreakCommand(User.RequireUserId(), breakId, key, DateTime.UtcNow), ct);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("self/attendance/clock-out")]
    [HasPermission(HrPermissions.AttendanceSelf)]
    public async Task<IActionResult> ClockOut([FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        if (await IsGeneralAdminAsync(User.RequireUserId(), ct)) return AdminAttendanceNotApplicable();
        if (string.IsNullOrWhiteSpace(key)) return BadRequest();
        var result = await _mediator.Send(new ClockOutAttendanceCommand(User.RequireUserId(), key, DateTime.UtcNow), ct);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpGet("admin/attendance/sessions")]
    [HasPermission(HrPermissions.AttendanceTeamRead)]
    public async Task<IActionResult> Sessions(DateOnly? from, DateOnly? to, CancellationToken ct) => Ok(await _db.AttendanceSessions.AsNoTracking()
        .Where(item => (!from.HasValue || item.WorkDate >= from) && (!to.HasValue || item.WorkDate <= to))
        .OrderByDescending(item => item.WorkDate).Select(item => new { item.Id, item.EmployeeId, employee = item.Employee!.User!.FullName, item.WorkDate, item.ClockedInAt, item.ClockedOutAt, state = item.State.ToString(), item.WorkedMinutes,
            employeePhone = item.Employee!.User!.PhoneNumber, item.LateMinutes, item.EarlyLeaveMinutes, item.OvertimeMinutes,
            breakAllowanceMinutes = item.Employee!.DailyBreakAllowanceMinutes, shortPermissionMaxMinutes = item.Employee.ShortPermissionMaxMinutes,
            openBreak = item.Breaks.Where(b => !b.EndedAt.HasValue).Select(b => new { b.Id, b.StartedAt, kind = b.Kind.ToString(), b.AllowedMinutes }).FirstOrDefault() }).Take(100).ToListAsync(ct));

    [HttpGet("admin/attendance/daily-report")]
    [HasPermission(HrPermissions.AttendanceTeamRead)]
    public async Task<IActionResult> DailyReport(DateOnly? from, DateOnly? to, CancellationToken ct) => Ok(await _db.AttendanceSessions.AsNoTracking()
        .Where(item => (!from.HasValue || item.WorkDate >= from) && (!to.HasValue || item.WorkDate <= to))
        .GroupBy(item => new
        {
            item.EmployeeId,
            item.WorkDate,
            Employee = item.Employee!.User!.FullName,
            EmployeePhone = item.Employee.User!.PhoneNumber,
        })
        .OrderByDescending(group => group.Key.WorkDate)
        .ThenBy(group => group.Key.Employee)
        .Select(group => new
        {
            employeeId = group.Key.EmployeeId,
            employee = group.Key.Employee,
            employeePhone = group.Key.EmployeePhone,
            workDate = group.Key.WorkDate,
            clockedInAt = group.Min(item => item.ClockedInAt),
            clockedOutAt = group.Max(item => item.ClockedOutAt),
            workedMinutes = group.Sum(item => item.WorkedMinutes),
            lateMinutes = group.Sum(item => item.LateMinutes),
            earlyLeaveMinutes = group.Sum(item => item.EarlyLeaveMinutes),
            overtimeMinutes = group.Sum(item => item.OvertimeMinutes),
            hasOpenSession = group.Any(item => !item.ClockedOutAt.HasValue),
        })
        .Take(1000)
        .ToListAsync(ct));

    [HttpGet("admin/attendance/attempts")]
    [HasPermission(HrPermissions.AttendanceManage)]
    public async Task<IActionResult> Attempts(CancellationToken ct) => Ok(await _db.AttendanceAttempts.AsNoTracking().OrderByDescending(item => item.OccurredAt)
        .Select(item => new { item.Id, item.EmployeeId, employee = item.Employee!.User!.FullName, eventType = item.EventType.ToString(), item.OccurredAt, item.Accepted, item.DecisionCode }).Take(100).ToListAsync(ct));

    [HttpGet("admin/attendance/corrections")]
    [HasPermission(HrPermissions.AttendanceReview)]
    public async Task<IActionResult> Corrections(CancellationToken ct) => Ok(await _db.AttendanceCorrections.AsNoTracking().OrderByDescending(item => item.CreatedAt)
        .Select(item => new { item.Id, item.EmployeeId, employee = item.Employee!.User!.FullName, item.AttendanceSessionId,
            item.ProposedClockedInAt, item.ProposedClockedOutAt, item.Reason, item.EvidenceReference, state = item.State.ToString(), item.BeforeJson, item.AppliedJson, item.Version }).Take(100).ToListAsync(ct));

    [HttpPost("admin/attendance/corrections/{correctionId:guid}/decision")]
    [HasPermission(HrPermissions.AttendanceReview)]
    public async Task<IActionResult> DecideCorrection(Guid correctionId, DecideAttendanceCorrectionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new DecideAttendanceCorrectionCommand(correctionId, request.Approve, request.IsHrDecision,
            request.Reason, User.RequireUserId(), request.ExpectedVersion), ct);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("admin/attendance/recalculate")]
    [HasPermission(HrPermissions.AttendanceManage)]
    public IActionResult Recalculate(AttendanceRecalculationRequest request)
    {
        var result = AttendanceCalculator.Calculate(new AttendanceCalculationInput(request.ClockedInAt, request.ClockedOutAt,
            request.ScheduledStart, request.ScheduledEnd, request.BreakMinutes, request.GraceMinutes, request.ExpectedMinutes));
        return Ok(new { dryRun = true, result });
    }

    [HttpPost("admin/attendance/trusted-devices")]
    [HasPermission(HrPermissions.AttendanceManage)]
    public async Task<IActionResult> RegisterDevice(RegisterTrustedDeviceRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RegisterTrustedAttendanceDeviceCommand(request.EmployeeId, request.DeviceToken, request.Name, request.ExpiresAt, User.RequireUserId()), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private string ClientIp() => Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim() ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

    private Task<bool> IsGeneralAdminAsync(Guid userId, CancellationToken ct) => _db.Users
        .Where(user => user.Id == userId)
        .AnyAsync(user => user.EmployeeProfile == null && user.UserRoles.Any(userRole => userRole.Role.Type == RoleType.Admin), ct);

    private ObjectResult AdminAttendanceNotApplicable() => StatusCode(StatusCodes.Status403Forbidden,
        new { errors = new[] { "ADMIN_ATTENDANCE_NOT_APPLICABLE" }, message = "المدير العام غير مشمول بنظام الحضور." });
}

public sealed record AttendanceEvidenceRequest(double? Latitude, double? Longitude, double? Accuracy, string? DeviceToken);
public sealed record StartAttendanceBreakRequest(AttendanceBreakKind Kind = AttendanceBreakKind.Regular);
public sealed record RegisterTrustedDeviceRequest(Guid EmployeeId, string DeviceToken, string Name, DateTime? ExpiresAt);
public sealed record SubmitAttendanceCorrectionRequest(Guid AttendanceSessionId, DateTime? ProposedClockedInAt, DateTime? ProposedClockedOutAt, string Reason, string? EvidenceReference);
public sealed record DecideAttendanceCorrectionRequest(bool Approve, bool IsHrDecision, string Reason, int ExpectedVersion);
public sealed record AttendanceRecalculationRequest(DateTime ClockedInAt, DateTime ClockedOutAt, DateTime ScheduledStart, DateTime ScheduledEnd,
    int BreakMinutes, int GraceMinutes, int ExpectedMinutes);
