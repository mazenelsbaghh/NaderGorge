using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Scheduling;
using NaderGorge.Application.Features.HR.Scheduling.Commands;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/hr")]
[Authorize]
public sealed class HrShiftsController : ControllerBase
{
    private readonly IAppDbContext _db; private readonly IMediator _mediator;
    public HrShiftsController(IAppDbContext db, IMediator mediator) { _db = db; _mediator = mediator; }

    [HttpGet("admin/shifts/calendars")]
    [HasPermission(HrPermissions.ShiftRead)]
    public async Task<IActionResult> GetCalendars(CancellationToken ct) => Ok(await _db.WorkCalendars.AsNoTracking()
        .Where(item => item.IsActive).OrderBy(item => item.Name)
        .Select(item => new { item.Id, item.Code, item.Name, item.TimeZoneId, item.WorkingDaysMask }).ToListAsync(ct));

    [HttpGet("admin/shifts/templates")]
    [HasPermission(HrPermissions.ShiftRead)]
    public async Task<IActionResult> GetTemplates(CancellationToken ct) => Ok(await _db.ShiftTemplates.AsNoTracking()
        .OrderBy(item => item.Name).Select(item => new
        {
            item.Id, item.Code, item.Name, mode = item.Mode.ToString(), item.WorkCalendarId,
            item.GraceMinutes, item.MinimumBreakMinutes, item.OvertimeAfterMinutes, item.Version,
            segments = item.Segments.OrderBy(segment => segment.Sequence).Select(segment => new
            { segment.Id, segment.Sequence, segment.DayOfWeek, segment.StartsAt, segment.EndsAt, segment.UnpaidBreakMinutes, workDateRule = segment.WorkDateRule.ToString() })
        }).ToListAsync(ct));

    [HttpGet("admin/shifts/assignments")]
    [HasPermission(HrPermissions.ShiftRead)]
    public async Task<IActionResult> GetAssignments(DateOnly? from, DateOnly? to, CancellationToken ct) => Ok(await _db.ShiftAssignments.AsNoTracking()
        .Where(item => item.Status == ShiftAssignmentStatus.Published &&
            (!from.HasValue || !item.EffectiveTo.HasValue || item.EffectiveTo > from) && (!to.HasValue || item.EffectiveFrom < to))
        .OrderBy(item => item.EffectiveFrom).Select(item => new
        {
            item.Id, item.EmployeeId, employee = item.Employee!.User!.FullName, item.ShiftTemplateId,
            shift = item.ShiftTemplate!.Name, item.EffectiveFrom, item.EffectiveTo, status = item.Status.ToString(), item.Reason
        }).ToListAsync(ct));

    [HttpPost("admin/shifts/templates")]
    [HasPermission(HrPermissions.ShiftManage)]
    public async Task<IActionResult> CreateTemplate(CreateShiftTemplateRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateShiftTemplateCommand(request.Code, request.Name, request.Mode,
            request.WorkCalendarId, request.GraceMinutes, request.MinimumBreakMinutes, request.OvertimeAfterMinutes,
            request.Segments.Select(item => new ShiftSegmentInput(item.Sequence, item.DayOfWeek, item.StartsAt, item.EndsAt, item.UnpaidBreakMinutes, item.WorkDateRule)).ToList(), User.RequireUserId()), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("admin/shifts/assignments/validate")]
    [HasPermission(HrPermissions.ShiftManage)]
    public async Task<IActionResult> ValidateAssignments(IReadOnlyList<ShiftAssignmentInput> assignments, CancellationToken ct)
    {
        var employeeIds = assignments.Select(item => item.EmployeeId).Distinct().ToList();
        var existing = await _db.ShiftAssignments.AsNoTracking().Where(item => employeeIds.Contains(item.EmployeeId) && item.Status == ShiftAssignmentStatus.Published)
            .Select(item => new { item.Id, item.EmployeeId, item.EffectiveFrom, item.EffectiveTo }).ToListAsync(ct);
        var conflicts = assignments.SelectMany(row => existing.Where(item => item.EmployeeId == row.EmployeeId && ShiftScheduleRules.PeriodsOverlap(item.EffectiveFrom, item.EffectiveTo, row.EffectiveFrom, row.EffectiveTo))
            .Select(item => new { row.EmployeeId, existingAssignmentId = item.Id, row.EffectiveFrom, row.EffectiveTo })).ToList();
        return Ok(new { valid = conflicts.Count == 0, conflicts });
    }

    [HttpPost("admin/shifts/assignments/publish")]
    [HasPermission(HrPermissions.ShiftManage)]
    public async Task<IActionResult> PublishAssignments(IReadOnlyList<ShiftAssignmentInput> assignments,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return BadRequest(new { errors = new[] { "IDEMPOTENCY_KEY_REQUIRED" } });
        var result = await _mediator.Send(new PublishShiftAssignmentsCommand(assignments, User.RequireUserId(), idempotencyKey), ct);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("self/shift-swaps")]
    [HasPermission(HrPermissions.ShiftRead)]
    public async Task<IActionResult> SubmitSwap(SubmitShiftSwapRequest request, CancellationToken ct)
    {
        var actorId = User.RequireUserId();
        var employeeId = await _db.EmployeeProfiles.Where(item => item.UserId == actorId).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(ct);
        if (!employeeId.HasValue) return Forbid();
        var result = await _mediator.Send(new SubmitShiftSwapCommand(employeeId.Value, request.RequesterAssignmentId,
            request.TargetEmployeeId, request.TargetAssignmentId, request.Reason, actorId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("admin/shifts/swaps/{requestId:guid}/decision")]
    [HasPermission(HrPermissions.ShiftManage)]
    public async Task<IActionResult> DecideSwap(Guid requestId, DecideShiftSwapRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new DecideShiftSwapCommand(requestId, request.Approve, request.Reason,
            User.RequireUserId(), request.IsHrDecision, request.ExpectedVersion), ct);
        return result.Success ? Ok(result) : Conflict(result);
    }
}

public sealed record ShiftSegmentRequest(int Sequence, DayOfWeek? DayOfWeek, TimeSpan StartsAt, TimeSpan EndsAt, int UnpaidBreakMinutes, ShiftWorkDateRule WorkDateRule);
public sealed record CreateShiftTemplateRequest(string Code, string Name, ShiftTemplateMode Mode, Guid WorkCalendarId,
    int GraceMinutes, int MinimumBreakMinutes, int OvertimeAfterMinutes, IReadOnlyList<ShiftSegmentRequest> Segments);
public sealed record SubmitShiftSwapRequest(Guid RequesterAssignmentId, Guid TargetEmployeeId, Guid TargetAssignmentId, string Reason);
public sealed record DecideShiftSwapRequest(bool Approve, string Reason, bool IsHrDecision, int ExpectedVersion);
