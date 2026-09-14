using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Sales;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/public-exams")]
[Authorize]
[HasPermission("public_exams.manage")]
public sealed class AdminPublicExamsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminPublicExamsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _mediator.Send(new GetPublicExamProductsQuery(PublishedOnly: false), ct));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] PublicExamProductRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SavePublicExamProductCommand(request, User.RequireUserId()), ct);
        return response.Success ? StatusCode(StatusCodes.Status201Created, response) : BadRequest(response);
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create([FromBody] CreatePublicExamRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreatePublicExamProductCommand(request, User.RequireUserId()), ct);
        return response.Success ? StatusCode(StatusCodes.Status201Created, response) : BadRequest(response);
    }

    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, [FromBody] DisableRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new DisablePublicExamProductCommand(id, User.RequireUserId(), request.Reason), ct);
        if (response.Errors?.Contains("NOT_FOUND") == true) return NotFound(response);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("{id:guid}/results")]
    public async Task<IActionResult> Results(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetPublicExamResultsQuery(id), ct);
        if (response.Errors?.Contains("NOT_FOUND") == true) return NotFound(response);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
