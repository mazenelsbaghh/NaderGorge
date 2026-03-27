using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Reports.Queries;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/parent/reports")]
public class ParentController : ControllerBase
{
    private readonly IMediator _mediator;

    public ParentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{studentId}/summary")]
    [AllowAnonymous] // Assuming MVP parent link is unauthenticated (or uses a secure token/hash later)
    public async Task<IActionResult> GetSummaryReport(Guid studentId, CancellationToken ct)
    {
        var query = new GetParentReportQuery(studentId);
        var result = await _mediator.Send(query, ct);

        return result.Success ? Ok(result) : NotFound(result);
    }
}
