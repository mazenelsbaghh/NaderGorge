using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.API.Extensions;
using NaderGorge.Domain.Enums;
using NaderGorge.API.Filters;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/student/[controller]")]
[Authorize]
public class BalanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public BalanceController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId() => User.RequireUserId();

    [HttpGet]
    public async Task<IActionResult> GetBalance()
    {
        var result = await _mediator.Send(new GetStudentBalanceQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("purchase")]
    [Idempotent]
    public async Task<IActionResult> PurchaseContent([FromBody] PurchaseRequestDto request)
    {
        var result = await _mediator.Send(new PurchaseContentCommand(GetUserId(), request.ContentType, request.ContentId));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public class PurchaseRequestDto
{
    public CodeType ContentType { get; set; }
    public Guid ContentId { get; set; }
}
