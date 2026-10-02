using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Assessments;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/whatsapp/admin/exams/{examId:guid}/parent-messages")]
[Authorize(Roles = "Admin")]
[HasPermission("settings.manage")]
public sealed class ExamParentMessagesController(IExamParentMessageRetryService service) : ControllerBase
{
    [HttpGet("/api/whatsapp/admin/exams/parent-messages")]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        try { return Ok(await service.ListAsync(User.RequireUserId(), search, page, ct)); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet]
    public async Task<IActionResult> Summary(Guid examId, CancellationToken ct)
    {
        try { return Ok(await service.SummaryAsync(User.RequireUserId(), examId, ct)); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("retry")]
    public async Task<IActionResult> Retry(Guid examId, [FromBody] ExamParentMessageRetryRequest request, CancellationToken ct)
    {
        try { return Ok(await service.QueueAsync(User.RequireUserId(), examId, request, ct)); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
