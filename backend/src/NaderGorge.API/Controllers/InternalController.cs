using MediatR;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Application.Features.Webhooks.Commands;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/v1/internal/callbacks")]
public class InternalController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;

    public InternalController(IMediator mediator, IConfiguration config)
    {
        _mediator = mediator;
        _config = config;
    }

    [HttpPost("ai-analysis-completed")]
    public async Task<IActionResult> AiAnalysisCompleted([FromBody] AiAnalysisCompletedWebhookRequest request)
    {
        // Simple shared secret token check
        var token = Request.Headers["X-Internal-Token"].FirstOrDefault();
        var expectedSecret = _config["AI_CALLBACK_SECRET"] ?? "secretxyz";
        
        if (token != expectedSecret)
            return Unauthorized("Invalid webhook token");

        var cmd = new AiAnalysisCompletedCommand(request.VideoId, request.SubtitleUrl, request.Chapters);
        var result = await _mediator.Send(cmd);
        
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPost("mindmaps-completed")]
    public async Task<IActionResult> MindmapsCompleted([FromBody] MindmapsCompletedWebhookRequest request)
    {
        var token = Request.Headers["X-Internal-Token"].FirstOrDefault();
        var expectedSecret = _config["API_CALLBACK_SECRET"] ?? "secretxyz";
        
        if (token != expectedSecret)
            return Unauthorized("Invalid webhook token");

        var cmd = new MindmapsCompletedCommand(request.VideoId, request.Mindmaps);
        var result = await _mediator.Send(cmd);
        
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("single-mindmap-completed")]
    public async Task<IActionResult> SingleMindmapCompleted([FromBody] SingleMindmapCompletedWebhookRequest request)
    {
        var token = Request.Headers["X-Internal-Token"].FirstOrDefault();
        var expectedSecret = _config["API_CALLBACK_SECRET"] ?? "secretxyz";

        if (token != expectedSecret)
            return Unauthorized("Invalid webhook token");

        var cmd = new SingleMindmapCompletedCommand(request.ChapterId, request.ImageUrl);
        var result = await _mediator.Send(cmd);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("essay-graded")]
    public async Task<IActionResult> EssayGraded([FromBody] EssayGradedWebhookRequest request)
    {
        var token = Request.Headers["X-Internal-Token"].FirstOrDefault();
        var expectedSecret = _config["AI_CALLBACK_SECRET"] ?? "secretxyz";
        
        if (token != expectedSecret)
            return Unauthorized("Invalid webhook token");

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
