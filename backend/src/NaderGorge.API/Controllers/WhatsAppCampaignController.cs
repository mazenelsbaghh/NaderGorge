using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/live-support/whatsapp")]
[HasPermission(WhatsAppCampaignPermissions.Manage)]
public sealed class WhatsAppCampaignController(IWhatsAppCampaignService campaigns) : ControllerBase
{
    [HttpGet("campaigns/bootstrap")]
    public Task<IActionResult> Bootstrap([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) => ExecuteAsync(
        async () => await campaigns.GetBootstrapAsync(page, pageSize, ct));

    [HttpGet("campaigns")]
    public Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) => ExecuteAsync(
        async () => await campaigns.ListAsync(page, pageSize, ct));

    [HttpPost("campaigns/audience/preview")]
    public Task<IActionResult> Preview(WhatsAppCampaignPreviewRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => await campaigns.PreviewAsync(request, ct));

    [HttpPost("campaigns/drafts")]
    public Task<IActionResult> CreateDraft(
        CreateWhatsAppCampaignDraftRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct) => ExecuteAsync(async () => await campaigns.CreateDraftAsync(
            User.RequireUserId(), idempotencyKey ?? string.Empty, request, ct));

    [HttpPost("campaigns/{campaignId:guid}/launch")]
    public Task<IActionResult> Launch(
        Guid campaignId,
        LaunchWhatsAppCampaignRequest request,
        CancellationToken ct) => ExecuteAsync(async () => await campaigns.LaunchAsync(
            User.RequireUserId(), campaignId, request, ct));

    [HttpPost("campaigns/{campaignId:guid}/pause")]
    public Task<IActionResult> Pause(
        Guid campaignId,
        ChangeWhatsAppCampaignStateRequest request,
        CancellationToken ct) => ExecuteAsync(async () => await campaigns.PauseAsync(
            User.RequireUserId(), campaignId, request, ct));

    [HttpPost("campaigns/{campaignId:guid}/resume")]
    public Task<IActionResult> Resume(
        Guid campaignId,
        ChangeWhatsAppCampaignStateRequest request,
        CancellationToken ct) => ExecuteAsync(async () => await campaigns.ResumeAsync(
            User.RequireUserId(), campaignId, request, ct));

    [HttpPost("campaigns/{campaignId:guid}/cancel")]
    public Task<IActionResult> Cancel(
        Guid campaignId,
        ChangeWhatsAppCampaignStateRequest request,
        CancellationToken ct) => ExecuteAsync(async () => await campaigns.CancelAsync(
            User.RequireUserId(), campaignId, request, ct));

    [HttpGet("preferences")]
    public Task<IActionResult> Preferences(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) => ExecuteAsync(async () =>
            await campaigns.ListPreferencesAsync(search, page, pageSize, ct));

    [HttpPost("preferences")]
    public Task<IActionResult> RecordPreference(
        RecordWhatsAppContactPreferenceRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct) => ExecuteAsync(async () => await campaigns.RecordPreferenceAsync(
            User.RequireUserId(), idempotencyKey ?? string.Empty, request, ct));

    [HttpPost("preferences/contacts/search")]
    public Task<IActionResult> ContactCandidates(
        SearchWhatsAppContactCandidatesRequest request,
        CancellationToken ct) => ExecuteAsync(async () =>
            await campaigns.SearchContactCandidatesAsync(
                request.Search, request.Page, request.PageSize, ct));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return Ok(ApiResponse<T>.Ok(await operation()));
        }
        catch (WhatsAppCampaignException exception)
        {
            return StatusCode(exception.StatusCode,
                ApiResponse<object>.Fail(exception.Message, [exception.Code]));
        }
    }
}
