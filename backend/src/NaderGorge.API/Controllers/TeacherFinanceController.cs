using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Teacher.Finance.Commands;
using NaderGorge.Application.Features.Teacher.Finance.Queries;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/teacher/finance")]
[Authorize(Roles = "Teacher")]
public class TeacherFinanceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly TeacherAuthorizationService _teacherAuthorization;
    private readonly ITeacherFinanceExportService _exportService;
    private readonly ITeacherStatementService _statement;

    public TeacherFinanceController(
        IMediator mediator,
        TeacherAuthorizationService teacherAuthorization,
        ITeacherFinanceExportService exportService,
        ITeacherStatementService statement)
    {
        _mediator = mediator;
        _teacherAuthorization = teacherAuthorization;
        _exportService = exportService;
        _statement = statement;
    }

    private Guid GetUserId() => User.RequireUserId();

    [HttpGet("account")]
    public async Task<IActionResult> GetAccountSummary(CancellationToken ct)
    {
        var teacherUserId = await GetAuthorizedTeacherUserIdAsync(ct);
        if (teacherUserId == null) return Forbid();
        var result = await _mediator.Send(new GetTeacherAccountQuery(teacherUserId.Value), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("statement")]
    public async Task<IActionResult> GetStatement([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        if (from > to || page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            return BadRequest(new { success = false, message = "فترة أو صفحة كشف الحساب غير صالحة" });
        var workspace = await _teacherAuthorization.GetWorkspaceAccessAsync(GetUserId(), ct);
        if (workspace is null || (!workspace.IsOwner && !workspace.PermissionKeys.Contains("finance"))) return Forbid();
        var statement = await _statement.GetAsync(workspace.TeacherId, from, to, page, pageSize, ct);
        return statement is null ? NotFound() : Ok(new { success = true, data = statement });
    }

    [HttpGet("statement/pdf")]
    public async Task<IActionResult> ExportStatement([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (from > to) return BadRequest(new { success = false, message = "فترة كشف الحساب غير صالحة" });
        var workspace = await _teacherAuthorization.GetWorkspaceAccessAsync(GetUserId(), ct);
        if (workspace is null || (!workspace.IsOwner && !workspace.PermissionKeys.Contains("finance"))) return Forbid();
        var result = await _statement.ExportPdfAsync(workspace.TeacherId, from, to, ct);
        return result is null ? NotFound() : File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var teacherUserId = await GetAuthorizedTeacherUserIdAsync(HttpContext.RequestAborted);
        if (teacherUserId == null) return Forbid();
        var result = await _mediator.Send(new GetTeacherFinanceCalendarQuery(teacherUserId.Value, from, to));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("calendar/export")]
    public async Task<IActionResult> ExportCalendarDay([FromQuery] DateTime date, CancellationToken ct)
    {
        var teacherUserId = await GetAuthorizedTeacherUserIdAsync(ct);
        if (teacherUserId == null) return Forbid();
        var result = await _exportService.ExportDayAsync(teacherUserId.Value, date, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] DateTime? date = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var teacherUserId = await GetAuthorizedTeacherUserIdAsync(HttpContext.RequestAborted);
        if (teacherUserId == null) return Forbid();
        var result = await _mediator.Send(new GetTeacherTransactionsQuery(teacherUserId.Value, date, from, to, status, page, pageSize));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayouts(CancellationToken ct)
    {
        var teacherUserId = await GetAuthorizedTeacherUserIdAsync(ct);
        if (teacherUserId == null) return Forbid();
        var result = await _mediator.Send(new GetTeacherPayoutsQuery(teacherUserId.Value), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("payouts")]
    public async Task<IActionResult> RequestPayout([FromBody] TeacherRequestPayoutDto dto, CancellationToken ct)
    {
        var teacherUserId = await GetAuthorizedTeacherUserIdAsync(ct);
        if (teacherUserId == null) return Forbid();
        var result = await _mediator.Send(new RequestPayoutCommand(teacherUserId.Value, dto.Amount), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private async Task<Guid?> GetAuthorizedTeacherUserIdAsync(CancellationToken ct)
    {
        var workspace = await _teacherAuthorization.GetWorkspaceAccessAsync(GetUserId(), ct);
        return workspace != null && (workspace.IsOwner || workspace.PermissionKeys.Contains("finance"))
            ? workspace.TeacherUserId
            : null;
    }
}

public class TeacherRequestPayoutDto
{
    public decimal Amount { get; set; }
}
