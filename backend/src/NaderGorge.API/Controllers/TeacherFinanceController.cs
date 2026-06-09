using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Teacher.Finance.Commands;
using NaderGorge.Application.Features.Teacher.Finance.Queries;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/teacher/finance")]
[Authorize(Roles = "Teacher")]
public class TeacherFinanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeacherFinanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId() => User.RequireUserId();

    [HttpGet("account")]
    public async Task<IActionResult> GetAccountSummary()
    {
        var result = await _mediator.Send(new GetTeacherAccountQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetTeacherTransactionsQuery(GetUserId(), page, pageSize));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayouts()
    {
        var result = await _mediator.Send(new GetTeacherPayoutsQuery(GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("payouts")]
    public async Task<IActionResult> RequestPayout([FromBody] TeacherRequestPayoutDto dto)
    {
        var result = await _mediator.Send(new RequestPayoutCommand(GetUserId(), dto.Amount));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public class TeacherRequestPayoutDto
{
    public decimal Amount { get; set; }
}
