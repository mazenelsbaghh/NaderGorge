using MediatR;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Configuration;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Application.Features.Webhooks.Commands;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/v1/internal/callbacks")]
public class InternalController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILiveSupportService _liveSupportService;

    public InternalController(IMediator mediator, ILiveSupportService liveSupportService)
    {
        _mediator = mediator;
        _liveSupportService = liveSupportService;
    }

    [InternalTokenAuthorize("AI_CALLBACK_SECRET", "API_CALLBACK_SECRET")]
    [HttpPost("ai-analysis-completed")]
    public async Task<IActionResult> AiAnalysisCompleted([FromBody] AiAnalysisCompletedWebhookRequest request)
    {
        var cmd = new AiAnalysisCompletedCommand(request.VideoId, request.SubtitleUrl, request.Chapters, request.JobId);
        var result = await _mediator.Send(cmd);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [InternalTokenAuthorize("AI_CALLBACK_SECRET", "API_CALLBACK_SECRET")]
    [HttpPost("ai-progress")]
    public async Task<IActionResult> AiProgress([FromBody] AiProgressWebhookRequest request)
    {
        var cmd = new AiProgressCommand(request.JobId, request.Progress, request.Status, request.Message);
        var result = await _mediator.Send(cmd);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [InternalTokenAuthorize("API_CALLBACK_SECRET")]
    [HttpPost("mindmaps-completed")]
    public async Task<IActionResult> MindmapsCompleted([FromBody] MindmapsCompletedWebhookRequest request)
    {
        var cmd = new MindmapsCompletedCommand(request.VideoId, request.Mindmaps);
        var result = await _mediator.Send(cmd);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [InternalTokenAuthorize("API_CALLBACK_SECRET")]
    [HttpPost("single-mindmap-completed")]
    public async Task<IActionResult> SingleMindmapCompleted([FromBody] SingleMindmapCompletedWebhookRequest request)
    {
        var cmd = new SingleMindmapCompletedCommand(request.ChapterId, request.ImageUrl);
        var result = await _mediator.Send(cmd);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [InternalTokenAuthorize("AI_CALLBACK_SECRET", "API_CALLBACK_SECRET")]
    [HttpPost("essay-graded")]
    public async Task<IActionResult> EssayGraded([FromBody] EssayGradedWebhookRequest request)
    {
        var cmd = new WebhookEssayGradedCommand(request.EssaySubmissionId, request.AiScore, request.AiFeedback);
        var result = await _mediator.Send(cmd);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [InternalTokenAuthorize("AI_CALLBACK_SECRET", "API_CALLBACK_SECRET")]
    [HttpPost("live-support-ai/turns/{turnId:guid}/claim")]
    public async Task<IActionResult> ClaimAITurn([FromRoute] Guid turnId, CancellationToken ct)
    {
        var context = await _liveSupportService.ClaimAITurnAsync(turnId, ct);
        if (context is null) return NotFound(new { error = "Turn not found" });
        return Ok(context);
    }

    [InternalTokenAuthorize("AI_CALLBACK_SECRET", "API_CALLBACK_SECRET")]
    [HttpPost("live-support-ai/turns/{turnId:guid}/complete")]
    public async Task<IActionResult> CompleteAITurn([FromRoute] Guid turnId, [FromBody] LiveSupportAITurnCompleteRequest request, CancellationToken ct)
    {
        await _liveSupportService.CompleteAITurnAsync(turnId, request, ct);
        return Ok();
    }

    [InternalTokenAuthorize("AI_CALLBACK_SECRET", "API_CALLBACK_SECRET")]
    [HttpPost("live-support-ai/turns/{turnId:guid}/fail")]
    public async Task<IActionResult> FailAITurn([FromRoute] Guid turnId, [FromBody] LiveSupportAITurnFailRequest request, CancellationToken ct)
    {
        await _liveSupportService.FailAITurnAsync(turnId, request, ct);
        return Ok();
    }
}

public class AiAnalysisCompletedWebhookRequest
{
    public Guid VideoId { get; set; }
    public string SubtitleUrl { get; set; } = string.Empty;
    public List<ChapterDto> Chapters { get; set; } = new List<ChapterDto>();
    public string? JobId { get; set; }
}

public class AiProgressWebhookRequest
{
    public string JobId { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class MindmapsCompletedWebhookRequest
{
    public Guid VideoId { get; set; }
    public List<MindmapDto> Mindmaps { get; set; } = new List<MindmapDto>();
}

public class SingleMindmapCompletedWebhookRequest
{
    public Guid ChapterId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

public class EssayGradedWebhookRequest
{
    public Guid EssaySubmissionId { get; set; }
    public decimal AiScore { get; set; }
    public string? AiFeedback { get; set; }
}
