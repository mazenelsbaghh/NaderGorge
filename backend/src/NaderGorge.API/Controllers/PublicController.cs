using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Public.Queries;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlatformStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPlatformStatsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("settings")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicSettings(
        [FromServices] NaderGorge.Application.Common.ICachedPlatformSettingsReader settingsReader,
        CancellationToken ct)
    {
        var settings = await settingsReader.GetAsync(ct);
        return Ok(new
        {
            PlatformName = settings.PlatformName,
            SupportPhoneNumber = settings.SupportPhoneNumber,
            SupportWhatsAppUrl = settings.SupportWhatsAppUrl,
            YouTubeChannelUrl = settings.YouTubeChannelUrl,
            TelegramChannelUrl = settings.TelegramChannelUrl,
            MaintenanceMode = settings.MaintenanceMode,
            MaintenanceMessage = settings.MaintenanceMessage,
            EnableWatermark = settings.EnableWatermark,
            WatermarkOpacity = settings.WatermarkOpacity
        });
    }
}
