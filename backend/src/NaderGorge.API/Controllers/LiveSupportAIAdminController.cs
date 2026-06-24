using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/live-support/admin/ai")]
[Authorize(Roles = "Admin")]
public sealed class LiveSupportAIAdminController(ILiveSupportAIAdminService service, ILiveSupportAIKnowledgeService knowledge) : ControllerBase
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
    public async Task<IActionResult> Disable(ChangeAIStateRequest request, CancellationToken ct)
    {
        try
        {
            await service.DisableAsync(AdminId(), request.ExpectedVersion, ct);
            return Accepted(ApiResponse.Ok("تم إيقاف الرد الآلي، وسيتم تحويل المحادثات النشطة للدعم."));
        }
        catch (LiveSupportAIAdminException exception)
        {
            return Conflict(ApiResponse<object>.Fail(exception.Message, [exception.Code]));
        }
    }

    [HttpPost("enable")]
    public async Task<IActionResult> Enable(ChangeAIStateRequest request, CancellationToken ct) =>
        await Execute(() => service.EnableAsync(AdminId(), request.ExpectedVersion, ct));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct, [FromQuery] string period = "last-24h") =>
        Ok(ApiResponse<LiveSupportAIStatsDto>.Ok(await service.GetStatsAsync(period, ct)));

    [HttpGet("active-conversations")]
    public async Task<IActionResult> GetActiveConversations(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<LiveSupportAdminConversationDto>>.Ok(await service.GetActiveConversationsAsync(ct)));

    [HttpGet("knowledge")]
    public async Task<IActionResult> Knowledge(CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<LiveSupportAIKnowledgeRevisionDto>>.Ok(await knowledge.ListAsync(ct)));

    [HttpPost("knowledge/revisions")]
    public async Task<IActionResult> SaveKnowledge(SaveLiveSupportAIKnowledgeRequest request, CancellationToken ct)
    {
        try { return StatusCode(StatusCodes.Status201Created, ApiResponse<LiveSupportAIKnowledgeRevisionDto>.Ok(await knowledge.SaveRevisionAsync(AdminId(), request, ct))); }
        catch (InvalidOperationException exception) { return UnprocessableEntity(ApiResponse<object>.Fail("تعذر حفظ مصدر المعرفة.", [exception.Message])); }
    }

    [HttpPut("knowledge/links")]
    public async Task<IActionResult> LinkKnowledge(LinkLiveSupportAIKnowledgeRequest request, CancellationToken ct)
    {
        try { await knowledge.LinkPublishedRevisionsAsync(AdminId(), request, ct); return NoContent(); }
        catch (InvalidOperationException exception) { return UnprocessableEntity(ApiResponse<object>.Fail("تعذر ربط مصادر المعرفة.", [exception.Message])); }
    }

    [HttpPost("preview")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("live-support-ai-admin-preview")]
    [RequestSizeLimit(16 * 1_024)]
    public async Task<IActionResult> Preview(LiveSupportAIPreviewRequestDto request, CancellationToken ct)
    {
        try { return Ok(ApiResponse<LiveSupportAIPreviewResultDto>.Ok(await service.PreviewAsync(request, ct))); }
        catch (LiveSupportAIAdminException exception) { return UnprocessableEntity(ApiResponse<object>.Fail(exception.Message, [exception.Code])); }
    }

    [HttpGet("evidence")]
    public async Task<IActionResult> Evidence([FromQuery] string period = "last-24h", [FromQuery] string? cursor = null, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        try { return Ok(ApiResponse<LiveSupportAIEvidencePageDto>.Ok(await service.GetEvidenceAsync(period, cursor, pageSize, ct))); }
        catch (LiveSupportAIAdminException exception) { return UnprocessableEntity(ApiResponse<object>.Fail(exception.Message, [exception.Code])); }
    }

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
public sealed record ChangeAIStateRequest(long ExpectedVersion);
