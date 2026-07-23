using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Reports.Queries;
using NaderGorge.Application.Features.Reporting;
using System;
using System.Threading.Tasks;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = "Admin,Supervisor")]
[HasPermission("reports.manage")]
public class AdminReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IReportExportService _exporter;
    private readonly IReportQueryService _reports;
    private readonly IStudentLedgerExportService _studentLedger;

    public AdminReportsController(IMediator mediator, IReportExportService exporter, IReportQueryService reports, IStudentLedgerExportService studentLedger)
    {
        _mediator = mediator;
        _exporter = exporter;
        _reports = reports;
        _studentLedger = studentLedger;
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? performedByUserId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetAdminAuditLogsQuery(startDate, endDate, performedByUserId, entityType, page, pageSize);
        var result = await _mediator.Send(query);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("kpi")]
    public async Task<IActionResult> GetKpiDashboard(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? roleName = null,
        [FromQuery] Guid? employeeId = null)
    {
        var query = new GetAdminKpiDashboardQuery(startDate, endDate, roleName, employeeId);
        var result = await _mediator.Send(query);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetReportCatalogQuery(false), ct));

    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(CancellationToken ct) =>
        Ok(NaderGorge.Application.Common.ApiResponse<ReportFilterOptionsDto>.Ok(
            await _reports.GetFilterOptionsAsync(User.RequireUserId(), false, ct)));

    [HttpGet("student-ledger/export")]
    public async Task<IActionResult> ExportStudentLedger([FromQuery] Guid teacherId, CancellationToken ct)
    {
        var export = await _studentLedger.ExportAsync(teacherId, User.RequireUserId(), ct);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ExecuteReportRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExecuteReportQuery(User.RequireUserId(), false, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("export/{format}")]
    public async Task<IActionResult> Export(string format, [FromBody] ExecuteReportRequest request, CancellationToken ct)
    {
        try
        {
            var export = await _exporter.ExportAsync(format.ToLowerInvariant(), request, User.RequireUserId(), false, ct);
            return File(export.Content, export.ContentType, export.FileName);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("definitions")]
    public async Task<IActionResult> GetDefinitions(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetReportDefinitionsQuery(User.RequireUserId(), false), ct));

    [HttpGet("definitions/{id:guid}")]
    public async Task<IActionResult> GetDefinition(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetReportDefinitionQuery(id, User.RequireUserId(), false), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("definitions")]
    public async Task<IActionResult> CreateDefinition([FromBody] SaveReportDefinitionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateReportDefinitionCommand(User.RequireUserId(), false, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("definitions/{id:guid}")]
    public async Task<IActionResult> UpdateDefinition(Guid id, [FromBody] UpdateReportDefinitionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateReportDefinitionCommand(id, User.RequireUserId(), false, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("definitions/{id:guid}")]
    public async Task<IActionResult> DeleteDefinition(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteReportDefinitionCommand(id, User.RequireUserId()), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("definitions/{id:guid}/copy")]
    public async Task<IActionResult> CopyDefinition(Guid id, [FromBody] CopyReportDefinitionRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CopyReportDefinitionCommand(id, User.RequireUserId(), false, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
