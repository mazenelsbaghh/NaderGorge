using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.VideoLearning;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/video-learning")]
[Authorize]
[RequestSizeLimit(1_000_000)]
public sealed class VideoLearningController(VideoLearningService service) : ControllerBase
{
    private Guid Actor => User.RequireUserId();
    [HttpGet("{videoId:guid}")]
    public Task<IActionResult> Read(Guid videoId, CancellationToken ct) => Run(() => service.SnapshotAsync(Actor, videoId, false, ct));
    [HttpGet("{videoId:guid}/author"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Author(Guid videoId, CancellationToken ct) => Run(() => service.SnapshotAsync(Actor, videoId, true, ct));
    [HttpPut("{videoId:guid}/author"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Save(Guid videoId, SaveLearningDocument request, CancellationToken ct) => Run(() => service.SaveAsync(Actor, videoId, request, ct));
    [HttpPost("{videoId:guid}/entries")]
    public Task<IActionResult> Record(Guid videoId, LearningEntryRequest request, CancellationToken ct) => Run(() => service.RecordAsync(Actor, videoId, request, ct));
    [HttpGet("{videoId:guid}/entries/{entryId:guid}/replies")]
    public Task<IActionResult> Replies(Guid videoId, Guid entryId, CancellationToken ct) => Run(() => service.RepliesAsync(Actor, videoId, entryId, ct));
    [HttpDelete("{videoId:guid}/entries/{entryId:guid}")]
    public Task<IActionResult> Delete(Guid videoId, Guid entryId, CancellationToken ct) => Run(async () => { await service.DeleteAsync(Actor, videoId, entryId, ct); return true; });
    [HttpPost("{videoId:guid}/ai")]
    public Task<IActionResult> Ai(Guid videoId, LearningAiRequest request, CancellationToken ct) => Run(() => service.AiAsync(Actor, videoId, request, ct));
    [HttpGet("{videoId:guid}/report"), Authorize(Roles = "Admin,Teacher")]
    public Task<IActionResult> Report(Guid videoId, CancellationToken ct) => Run(() => service.ReportAsync(Actor, videoId, ct));
    [HttpGet("review")]
    public Task<IActionResult> Review(CancellationToken ct) => Run(() => service.ReviewAsync(Actor, ct));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(ApiResponse<T>.Ok(await action())); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("الفيديو أو التفاعل غير موجود.")); }
        catch (LearningConflictException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
        catch (Npgsql.PostgresException ex) when (ex.SqlState is "40001" or "40P01") { return Conflict(ApiResponse.Fail("طلب متزامن. حاول مرة أخرى.")); }
        catch (DbUpdateException) { return Conflict(ApiResponse.Fail("اتحفظ تعديل متزامن. حدّث الصفحة وحاول تاني.")); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("AI_", StringComparison.Ordinal))
        { return StatusCode(503, ApiResponse.Fail("المساعد غير متاح حاليًا. حاول لاحقًا أو اسأل المدرس.")); }
        catch (System.Text.Json.JsonException) { return StatusCode(503, ApiResponse.Fail("رد المساعد غير صالح. حاول مرة أخرى.")); }
        catch (HttpRequestException) { return StatusCode(503, ApiResponse.Fail("تعذر الاتصال بالمساعد. حاول لاحقًا.")); }
        catch (TaskCanceledException) { return StatusCode(503, ApiResponse.Fail("المساعد استغرق وقتًا أطول. حاول لاحقًا.")); }
    }
}
