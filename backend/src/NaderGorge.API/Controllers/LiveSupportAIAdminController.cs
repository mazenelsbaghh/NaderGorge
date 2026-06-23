using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/live-support/admin/ai")]
[Authorize(Roles = "Admin")]
public sealed class LiveSupportAIAdminController(ILiveSupportAIAdminService service) : ControllerBase
{
    [HttpGet("config")]
    public async Task<IActionResult> Config(CancellationToken ct) => Ok(ApiResponse<LiveSupportAIConfigDto>.Ok(await service.GetConfigAsync(ct)));

    [HttpGet("catalogs")]
    public IActionResult Catalogs() => Ok(ApiResponse<LiveSupportAICatalogsDto>.Ok(service.GetCatalogs()));

    [HttpPut("config")]
    public async Task<IActionResult> Save(SaveLiveSupportAIDraftRequest request, CancellationToken ct) =>
        await Execute(() => service.SaveDraftAsync(AdminId(), request, ct));

    [HttpPost("publish")]
    public async Task<IActionResult> Publish(PublishAIRequest request, CancellationToken ct) =>
        await Execute(() => service.PublishAsync(AdminId(), request.ExpectedVersion, ct));

    [HttpPost("disable")]
    public async Task<IActionResult> Disable(CancellationToken ct)
    {
        await service.DisableAsync(AdminId(), ct);
        return Accepted(ApiResponse.Ok("تم إيقاف الرد الآلي، وسيتم تحويل المحادثات النشطة للدعم."));
    }

    [HttpPost("enable")]
    public async Task<IActionResult> Enable(CancellationToken ct) =>
        await Execute(() => service.EnableAsync(AdminId(), ct));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct, [FromQuery] string period = "last-24h") =>
        Ok(ApiResponse<LiveSupportAIStatsDto>.Ok(await service.GetStatsAsync(period, ct)));

    private Guid AdminId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static async Task<IActionResult> Execute(Func<Task<LiveSupportAIPolicyDto>> action)
    {
        try { return new OkObjectResult(ApiResponse<LiveSupportAIPolicyDto>.Ok(await action())); }
        catch (LiveSupportAIAdminException ex)
        {
            var status = ex.Code == "VERSION_CONFLICT" ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity;
            return new ObjectResult(ApiResponse<object>.Fail(ex.Message, [ex.Code])) { StatusCode = status };
        }
    }
}

public sealed record PublishAIRequest(long ExpectedVersion);
