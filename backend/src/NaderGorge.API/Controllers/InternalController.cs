using MediatR;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Configuration;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Application.Features.Webhooks.Commands;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/v1/internal/callbacks")]
public class InternalController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [InternalTokenAuthorize("AI_CALLBACK_SECRET", "API_CALLBACK_SECRET")]
    [HttpPost("ai-analysis-completed")]
    public async Task<IActionResult> AiAnalysisCompleted([FromBody] AiAnalysisCompletedWebhookRequest request)
    {
        var cmd = new AiAnalysisCompletedCommand(request.VideoId, request.SubtitleUrl, request.Chapters);
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
}

public class AiAnalysisCompletedWebhookRequest
{
    public Guid VideoId { get; set; }
    public string SubtitleUrl { get; set; } = string.Empty;
    public List<ChapterDto> Chapters { get; set; } = new List<ChapterDto>();
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
