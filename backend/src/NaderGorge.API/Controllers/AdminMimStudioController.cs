using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.MimStudio;
using NaderGorge.Infrastructure.Services.MimStudio;

namespace NaderGorge.API.Controllers;

[ApiController, Authorize(Roles = "Admin"), HasPermission("content.manage")]
[Route("api/admin/mim-studio"), RequestSizeLimit(250_000)]
public sealed class AdminMimStudioController(LessonMimStudioService studio, HiggsfieldMcpConnectionService connection) : ControllerBase
{
    [HttpGet("lessons/{lessonId:guid}")]
    public Task<IActionResult> Read(Guid lessonId, CancellationToken ct) => Run(() => studio.ReadAsync(lessonId, ct));

    [HttpGet("lessons/{lessonId:guid}/sources")]
    public Task<IActionResult> Sources(Guid lessonId, CancellationToken ct) => Run(() => studio.SourcesAsync(lessonId, ct));

    [HttpPut("lessons/{lessonId:guid}")]
    public Task<IActionResult> Save(Guid lessonId, SaveMimStudio request, CancellationToken ct) =>
        Run(() => studio.SaveAsync(User.RequireUserId(), lessonId, request, ct));

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
        catch (HiggsfieldMcpException ex) { return StatusCode(502, ApiResponse.Fail(ex.Message)); }
    }
}
