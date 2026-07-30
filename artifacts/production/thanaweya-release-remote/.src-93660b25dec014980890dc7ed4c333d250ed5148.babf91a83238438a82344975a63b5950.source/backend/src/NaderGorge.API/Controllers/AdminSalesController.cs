using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Sales;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/sales")]
[Authorize]
[HasPermission("sales.manage")]
public sealed class AdminSalesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminSalesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("rules")]
    public async Task<IActionResult> Rules(CancellationToken ct) => Ok(await _mediator.Send(new GetSalesRulesQuery(), ct));

    [HttpPost("rules")]
    public async Task<IActionResult> SaveRule([FromBody] SalesRuleRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SaveSalesRuleCommand(request, User.RequireUserId()), ct);
        return response.Success ? StatusCode(StatusCodes.Status201Created, response) : BadRequest(response);
    }

    [HttpGet("coupons")]
    public async Task<IActionResult> Coupons(CancellationToken ct) => Ok(await _mediator.Send(new GetSalesCouponsQuery(), ct));

    [HttpGet("coupons/{id:guid}")]
    public async Task<IActionResult> Coupon(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetSalesCouponByIdQuery(id), ct);
        if (response.Errors?.Contains("NOT_FOUND") == true) return NotFound(response);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon([FromBody] SalesCouponRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateSalesCouponCommand(request, User.RequireUserId()), ct);
        return response.Success ? StatusCode(StatusCodes.Status201Created, response) : BadRequest(response);
    }

    [HttpPut("coupons/{id:guid}")]
    public async Task<IActionResult> UpdateCoupon(Guid id, [FromBody] SalesCouponRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateSalesCouponCommand(id, request, User.RequireUserId()), ct);
        if (response.Errors?.Contains("NOT_FOUND") == true) return NotFound(response);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("coupons/{id:guid}/disable")]
    public async Task<IActionResult> DisableCoupon(Guid id, [FromBody] DisableRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new DisableSalesCouponCommand(id, request.Reason), ct);
        if (response.Errors?.Contains("NOT_FOUND") == true) return NotFound(response);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("stacking-policies")]
    public async Task<IActionResult> StackingPolicies(CancellationToken ct) => Ok(await _mediator.Send(new GetStackingPoliciesQuery(), ct));

    [HttpPost("stacking-policies")]
    public async Task<IActionResult> SaveStackingPolicy([FromBody] StackingPolicyRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SaveStackingPolicyCommand(request, User.RequireUserId()), ct);
        return response.Success ? StatusCode(StatusCodes.Status201Created, response) : BadRequest(response);
    }

    [HttpGet("printable-batches")]
    public async Task<IActionResult> PrintableBatches(CancellationToken ct) => Ok(await _mediator.Send(new GetPrintableBatchesQuery(), ct));

    [HttpPost("printable-batches")]
    public async Task<IActionResult> CreatePrintableBatch([FromBody] PrintableBatchRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreatePrintableBatchCommand(request, User.RequireUserId()), ct);
        return response.Success ? StatusCode(StatusCodes.Status201Created, response) : BadRequest(response);
    }
}

[ApiController]
[Route("api/admin/sales/templates")]
[Authorize]
[HasPermission("sales.templates.manage")]
public sealed class AdminSalesTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;

    public AdminSalesTemplatesController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _mediator.Send(new GetPrintableTemplatesQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] PrintableTemplateRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SavePrintableTemplateCommand(request, User.RequireUserId()), ct);
        return response.Success ? StatusCode(StatusCodes.Status201Created, response) : BadRequest(response);
    }

    [HttpPost("background-image")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadBackgroundImage(IFormFile image, CancellationToken ct)
    {
        if (image.Length == 0)
        {
            return BadRequest(ApiResponse.Fail("Image file is required."));
        }

        byte[] imageBytes;
        await using (var input = image.OpenReadStream())
        {
            using var memory = new MemoryStream();
            await input.CopyToAsync(memory, ct);
            imageBytes = memory.ToArray();
        }

        SafeUploadResult validation;
        try
        {
            validation = UploadFileSafety.Validate(imageBytes, image.FileName, image.ContentType, SafeUploadKind.PublicImage);
        }
        catch (InvalidUploadContentException)
        {
            return BadRequest(ApiResponse.Fail("Allowed image types: JPG, PNG, WEBP."));
        }

        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
        var uploadFolder = Path.Combine(webRoot, "uploads", "code-templates");
        Directory.CreateDirectory(uploadFolder);

        var fileName = validation.SafeFileName;
        var physicalPath = Path.Combine(uploadFolder, fileName);
        await using (var stream = new FileStream(physicalPath, FileMode.CreateNew))
        {
            await stream.WriteAsync(imageBytes, ct);
        }

        return Ok(ApiResponse<object>.Ok(new { Url = $"/uploads/code-templates/{fileName}" }));
    }
}
