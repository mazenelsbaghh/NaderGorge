using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Finance.Commands;
using NaderGorge.Application.Features.Admin.Finance.Queries;
using NaderGorge.Domain.Enums;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/finance")]
[Authorize(Roles = "Admin,Supervisor")]
public class AdminFinanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminFinanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId() => User.RequireUserId();

    [HttpGet("payroll")]
    public async Task<IActionResult> GetPayroll([FromQuery] int month, [FromQuery] int year)
    {
        var result = await _mediator.Send(new GetPayrollQuery(month, year));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("payroll/generate")]
    public async Task<IActionResult> GeneratePayroll([FromBody] GeneratePayrollDto dto)
    {
        var result = await _mediator.Send(new GeneratePayrollCommand(dto.Month, dto.Year, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("payroll/{id:guid}/adjustments")]
    public async Task<IActionResult> AddPayrollAdjustment([FromRoute] Guid id, [FromBody] AddAdjustmentDto dto)
    {
        var result = await _mediator.Send(new AddPayrollAdjustmentCommand(id, dto.Type, dto.Amount, dto.Reason, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("payroll/{id:guid}/adjustments/{adjustmentId:guid}")]
    public async Task<IActionResult> DeletePayrollAdjustment([FromRoute] Guid id, [FromRoute] Guid adjustmentId)
    {
        var result = await _mediator.Send(new DeletePayrollAdjustmentCommand(id, adjustmentId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("payroll/{id:guid}/approve")]
    public async Task<IActionResult> ApprovePayroll([FromRoute] Guid id)
    {
        var result = await _mediator.Send(new ApprovePayrollCommand(id, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayouts([FromQuery] PayoutStatus? status = null)
    {
        var result = await _mediator.Send(new GetPayoutsQuery(status));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("payouts/{id:guid}/resolve")]
    public async Task<IActionResult> ResolvePayout([FromRoute] Guid id, [FromBody] ResolvePayoutDto dto)
    {
        var result = await _mediator.Send(new ResolvePayoutCommand(id, dto.Status, dto.RejectionReason, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("code-accounting")]
    public async Task<IActionResult> GetCodeAccounting(
        [FromQuery] Guid? teacherId = null,
        [FromQuery] Guid? packageId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetCodeAccountingQuery(teacherId, packageId, startDate, endDate, page, pageSize));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public class GeneratePayrollDto
{
    public int Month { get; set; }
    public int Year { get; set; }
}

public class AddAdjustmentDto
{
    public PayrollAdjustmentType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = null!;
}

public class ResolvePayoutDto
{
    public PayoutStatus Status { get; set; }
    public string? RejectionReason { get; set; }
}
