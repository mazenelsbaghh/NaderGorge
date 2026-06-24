using MediatR;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Configuration;
using NaderGorge.Application.Features.Internal.Commands;
using NaderGorge.Application.Features.Webhooks.Commands;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/v1/internal/callbacks")]
public class InternalController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILiveSupportService _liveSupportService;
    private readonly ILiveSupportAITurnOrchestrator _liveSupportAITurnOrchestrator;

    public InternalController(IMediator mediator, ILiveSupportService liveSupportService, ILiveSupportAITurnOrchestrator liveSupportAITurnOrchestrator)
    {
        _mediator = mediator;
        _liveSupportService = liveSupportService;
        _liveSupportAITurnOrchestrator = liveSupportAITurnOrchestrator;
    }

    [InternalTokenAuthorize("AI_CALLBACK_SECRET")]
    [HttpGet("live-support-ai/readiness")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("live-support-ai-callback")]
    public IActionResult LiveSupportAIReadiness() => Ok(new { status = "ready" });

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

    [InternalTokenAuthorize("AI_CALLBACK_SECRET")]
    [HttpPost("live-support-ai/turns/{turnId:guid}/claim")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("live-support-ai-callback")]
    [RequestSizeLimit(1_024)]
    public async Task<IActionResult> ClaimAITurn([FromRoute] Guid turnId, CancellationToken ct)
    {
        var context = await _liveSupportAITurnOrchestrator.ClaimAsync(turnId, ct);
        if (context is null) return NotFound(new { code = "TURN_NOT_FOUND" });
        return Ok(context);
    }

    [InternalTokenAuthorize("AI_CALLBACK_SECRET")]
    [HttpPost("live-support-ai/turns/{turnId:guid}/complete")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("live-support-ai-callback")]
    [RequestSizeLimit(256 * 1_024)]
    public async Task<IActionResult> CompleteAITurn([FromRoute] Guid turnId, [FromBody] LiveSupportAIWorkerCompletionDto request, CancellationToken ct)
    {
        try
        {
            var outcome = await _liveSupportAITurnOrchestrator.CompleteAsync(turnId, request, ct);
            return outcome switch
            {
                "TURN_NOT_FOUND" => NotFound(new { code = outcome }),
                "IDEMPOTENCY_CONFLICT" => Conflict(new { code = outcome }),
                _ => Ok(new { outcome })
            };
        }
        catch (InvalidOperationException exception) when (exception.Message is "DECISION_SCHEMA_INVALID" or "DECISION_HASH_INVALID" or "ACTION_REQUIRES_LINKED_STUDENT" or "ACTION_NOT_ALLOWED" or "AI_DATA_PROTECTOR_UNAVAILABLE")
        {
            return UnprocessableEntity(new { code = exception.Message });
        }
    }

    [InternalTokenAuthorize("AI_CALLBACK_SECRET")]
    [HttpPost("live-support-ai/turns/{turnId:guid}/fail")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("live-support-ai-callback")]
    [RequestSizeLimit(16 * 1_024)]
    public async Task<IActionResult> FailAITurn([FromRoute] Guid turnId, [FromBody] LiveSupportAIWorkerFailureDto request, CancellationToken ct)
    {
        var outcome = await _liveSupportAITurnOrchestrator.FailAsync(turnId, request, ct);
        return outcome switch
        {
            "TURN_NOT_FOUND" => NotFound(new { code = outcome }),
            "INVALID_FAILURE" => UnprocessableEntity(new { code = outcome }),
            _ => Ok(new { outcome })
        };
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
