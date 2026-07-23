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
        var result = await _mediator.Send(new PurchaseContentCommand(GetUserId(), request.ContentType, request.ContentId, request.CouponCodes ?? new(), request.PrintableCodes ?? new()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("purchase-preview")]
    public async Task<IActionResult> PurchasePreview(
        [FromQuery] CodeType contentType,
        [FromQuery] Guid contentId,
        [FromQuery] string[]? couponCodes,
        [FromQuery] string[]? printableCodes)
    {
        var result = await _mediator.Send(new GetPurchaseFundingPreviewQuery(GetUserId(), contentType, contentId, couponCodes ?? Array.Empty<string>(), printableCodes ?? Array.Empty<string>()));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public class PurchaseRequestDto
{
    public CodeType ContentType { get; set; }
    public Guid ContentId { get; set; }
    public List<string>? CouponCodes { get; set; }
    public List<string>? PrintableCodes { get; set; }
}
