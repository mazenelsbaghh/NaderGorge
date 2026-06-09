using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Reports.Queries;
using System;
using System.Threading.Tasks;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = "Admin,Supervisor")]
public class AdminReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminReportsController(IMediator mediator)
    {
        _mediator = mediator;
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
}
