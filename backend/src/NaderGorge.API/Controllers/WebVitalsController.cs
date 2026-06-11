using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Metrics.Commands;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/v1/metrics")]
[Authorize]
public class WebVitalsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WebVitalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("web-vitals")]
    public async Task<IActionResult> ReportWebVitals([FromBody] CreateWebVitalsMetricCommand command)
    {
        var response = await _mediator.Send(command);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }
}
