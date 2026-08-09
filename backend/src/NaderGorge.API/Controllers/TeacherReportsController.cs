using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Reporting;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/teacher/reports")]
[Authorize(Roles = "Teacher")]
public sealed class TeacherReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IReportExportService _exporter;
    private readonly IReportQueryService _reports;
    private readonly IStudentLedgerExportService _studentLedger;

    public TeacherReportsController(IMediator mediator, IReportExportService exporter, IReportQueryService reports, IStudentLedgerExportService studentLedger)
    {
        _mediator = mediator;
        _exporter = exporter;
        _reports = reports;
        _studentLedger = studentLedger;
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var catalog = ReportCatalog.Get(true);
        if (!await _reports.CanAccessTeacherFinanceAsync(User.RequireUserId(), ct))
            catalog = catalog with { Domains = catalog.Domains.Where(domain => domain.Key != ReportDomains.TeachersFinance).ToArray() };
        return Ok(NaderGorge.Application.Common.ApiResponse<ReportCatalogDto>.Ok(catalog));
    }

    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        return Ok(NaderGorge.Application.Common.ApiResponse<ReportFilterOptionsDto>.Ok(
            await _reports.GetFilterOptionsAsync(User.RequireUserId(), true, ct)));
    }

    [HttpGet("student-ledger/export")]
    public async Task<IActionResult> ExportStudentLedger(
        [FromQuery] NaderGorge.Domain.Enums.EducationStage? stage,
        [FromQuery] NaderGorge.Domain.Enums.StudyTrack? studyTrack,
        CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        try
        {
            var export = await _studentLedger.ExportForTeacherAsync(User.RequireUserId(), new StudentLedgerFilter(stage, studyTrack), ct);
            return File(export.Content, export.ContentType, export.FileName);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ExecuteReportRequest request, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var result = await _mediator.Send(new ExecuteReportQuery(User.RequireUserId(), true, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("export/{format}")]
    public async Task<IActionResult> Export(string format, [FromBody] ExecuteReportRequest request, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        try
        {
            var export = await _exporter.ExportAsync(format.ToLowerInvariant(), request, User.RequireUserId(), true, ct);
            return File(export.Content, export.ContentType, export.FileName);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("definitions")]
    public async Task<IActionResult> GetDefinitions(CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        return Ok(await _mediator.Send(new GetReportDefinitionsQuery(User.RequireUserId(), true), ct));
    }

    [HttpGet("definitions/{id:guid}")]
    public async Task<IActionResult> GetDefinition(Guid id, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var result = await _mediator.Send(new GetReportDefinitionQuery(id, User.RequireUserId(), true), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("definitions")]
    public async Task<IActionResult> CreateDefinition([FromBody] SaveReportDefinitionRequest request, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var result = await _mediator.Send(new CreateReportDefinitionCommand(User.RequireUserId(), true, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("definitions/{id:guid}")]
    public async Task<IActionResult> UpdateDefinition(Guid id, [FromBody] UpdateReportDefinitionRequest request, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var result = await _mediator.Send(new UpdateReportDefinitionCommand(id, User.RequireUserId(), true, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("definitions/{id:guid}")]
    public async Task<IActionResult> DeleteDefinition(Guid id, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var result = await _mediator.Send(new DeleteReportDefinitionCommand(id, User.RequireUserId()), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("definitions/{id:guid}/copy")]
    public async Task<IActionResult> CopyDefinition(Guid id, [FromBody] CopyReportDefinitionRequest request, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var result = await _mediator.Send(new CopyReportDefinitionCommand(id, User.RequireUserId(), true, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private async Task<bool> CanAccessAsync(CancellationToken ct)
    {
        return await _reports.CanAccessTeacherReportsAsync(User.RequireUserId(), ct);
    }
}
