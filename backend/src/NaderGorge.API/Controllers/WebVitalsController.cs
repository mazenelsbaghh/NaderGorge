using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Metrics.Commands;
using NaderGorge.Application.Features.Metrics.Queries;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/v1/metrics")]
public class WebVitalsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WebVitalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("web-vitals")]
    [AllowAnonymous]
    [EnableRateLimiting("web-vitals")]
    [RequestSizeLimit(4_096)]
    public async Task<IActionResult> ReportWebVitals(
        [FromBody] CreateWebVitalsMetricCommand command,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Accepted(response);
    }

    [HttpGet("web-vitals/summary")]
    [Authorize(Roles = "Admin,Supervisor,Staff")]
    [HasPermission("reports.manage")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? releaseId,
        [FromQuery] string? routeTemplate,
        [FromQuery] string? surface,
        [FromQuery] string? deviceClass,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new GetWebVitalsSummaryQuery(
                releaseId,
                routeTemplate,
                surface,
                deviceClass,
                from,
                to),
            cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
