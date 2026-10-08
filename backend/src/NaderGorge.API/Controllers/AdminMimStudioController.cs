using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.MimStudio;
using NaderGorge.Infrastructure.Services.MimStudio;

namespace NaderGorge.API.Controllers;

[ApiController, Authorize(Roles = "Admin"), HasPermission("content.manage")]
[Route("api/admin/mim-studio"), RequestSizeLimit(2_000_000)]
public sealed class AdminMimStudioController(LessonMimStudioService studio, HiggsfieldMcpConnectionService connection, MimSceneVideoService videos, MimEpisodeVideoService episodes) : ControllerBase
{
    [HttpGet("lessons/{lessonId:guid}")]
    public Task<IActionResult> Read(Guid lessonId, CancellationToken ct) => Run(() => studio.ReadAsync(lessonId, ct));

    [HttpGet("lessons/{lessonId:guid}/sources")]
    public Task<IActionResult> Sources(Guid lessonId, CancellationToken ct) => Run(() => studio.SourcesAsync(lessonId, ct));

    [HttpPut("lessons/{lessonId:guid}")]
    public Task<IActionResult> Save(Guid lessonId, SaveMimStudio request, CancellationToken ct) =>
        Run(() => studio.SaveAsync(User.RequireUserId(), lessonId, request, ct));

    [HttpPost("lessons/{lessonId:guid}/scenes/next")]
    public Task<IActionResult> Generate(Guid lessonId, GenerateMimScene request, CancellationToken ct) =>
        Run(() => studio.GenerateAsync(User.RequireUserId(), lessonId, request, ct));

    [HttpGet("lessons/{lessonId:guid}/episode-video")]
    public Task<IActionResult> Episode(Guid lessonId, CancellationToken ct) => Run(() => episodes.ReadAsync(User.RequireUserId(), lessonId, ct));

    [HttpPost("lessons/{lessonId:guid}/episode-video")]
    public Task<IActionResult> Assemble(Guid lessonId, MimEpisodeApproval request, CancellationToken ct) =>
        Run(() => episodes.AssembleAsync(User.RequireUserId(), lessonId, request, ct));

    [HttpGet("lessons/{lessonId:guid}/episode-video/file")]
    public async Task<IActionResult> EpisodeFile(Guid lessonId, CancellationToken ct)
    {
        try
        {
            var response = await episodes.FileAsync(User.RequireUserId(), lessonId, Request.Headers.Range.ToString(), ct);
            Response.RegisterForDispose(response);
            Response.StatusCode = (int)response.StatusCode;
            Response.Headers.CacheControl = "private, no-store";
            if (response.Content.Headers.ContentRange is { } range) Response.Headers.ContentRange = range.ToString();
            if (response.Content.Headers.ContentLength is { } length) Response.ContentLength = length;
            return File(await response.Content.ReadAsStreamAsync(ct), "video/mp4");
        }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("الحصة غير موجودة.")); }
        catch (MimStudioConflictException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
        catch (MimStudioGenerationException ex) { return StatusCode(503, ApiResponse.Fail(ex.Message)); }
    }

    [HttpGet("lessons/{lessonId:guid}/scenes/{scene:int}/video")]
    public Task<IActionResult> Video(Guid lessonId, int scene, CancellationToken ct) => Run(() => videos.ReadAsync(User.RequireUserId(), lessonId, scene, ct));

    [HttpPost("lessons/{lessonId:guid}/scenes/{scene:int}/video/quote")]
    public Task<IActionResult> Quote(Guid lessonId, int scene, MimVideoQuoteRequest request, CancellationToken ct) =>
        Run(() => videos.QuoteAsync(User.RequireUserId(), new(lessonId, scene, request.Model), ct));

    [HttpGet("video-models")]
    public IActionResult Models() => Ok(ApiResponse<MimVideoModel[]>.Ok(MimVideoModels.Choices));

    [HttpPost("lessons/{lessonId:guid}/scenes/{scene:int}/video")]
    public Task<IActionResult> Submit(Guid lessonId, int scene, MimVideoApproval request, CancellationToken ct) =>
        Run(() => videos.SubmitAsync(User.RequireUserId(), lessonId, scene, request, ct));

    [HttpPost("lessons/{lessonId:guid}/scenes/{scene:int}/video/review")]
    public Task<IActionResult> Review(Guid lessonId, int scene, MimVideoReview request, CancellationToken ct) =>
        Run(() => videos.ReviewAsync(User.RequireUserId(), new(lessonId, scene, request), ct));

    [HttpGet("connection")]
    public Task<IActionResult> Connection(CancellationToken ct) => Run(() => connection.StatusAsync(User.RequireUserId(), ct));

    [HttpPost("connection/start")]
    public Task<IActionResult> Connect(CancellationToken ct) => Run(() => connection.StartAsync(User.RequireUserId(), ct));

    [HttpPost("connection/complete")]
    public Task<IActionResult> Complete(HiggsfieldOAuthCallback request, CancellationToken ct) =>
        Run(() => connection.CompleteAsync(User.RequireUserId(), request, ct));

    [HttpDelete("connection")]
    public Task<IActionResult> Disconnect(CancellationToken ct) => Run(() => connection.DisconnectAsync(User.RequireUserId(), ct));

    [HttpGet("connection/tools")]
    public Task<IActionResult> Tools(CancellationToken ct) => Run(() => connection.DiscoverAsync(User.RequireUserId(), ct));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(ApiResponse<T>.Ok(await action())); }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("الحصة غير موجودة.")); }
        catch (MimStudioConflictException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
        catch (DbUpdateConcurrencyException) { return Conflict(ApiResponse.Fail("البيانات اتغيّرت أثناء الطلب. حدّث الصفحة.")); }
        catch (DbUpdateException) { return Conflict(ApiResponse.Fail("تعذر الحفظ بسبب تعديل متزامن أو بيانات مرتبطة.")); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (MimStudioGenerationException ex) { return StatusCode(503, ApiResponse.Fail(ex.Message)); }
        // This is an actionable provider rejection, not a gateway failure. Preserve its message through the edge.
        catch (HiggsfieldMcpException ex) { return UnprocessableEntity(ApiResponse.Fail(ex.Message)); }
    }
}
