using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using System.Security.Claims;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    // --- Users ---
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _mediator.Send(new ListUsersQuery(page, pageSize, search)));

    [HttpPut("users/{id:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest dto)
    {
        var result = await _mediator.Send(new UpdateUserStatusCommand(id, dto.Status, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("users/{id:guid}/devices")]
    public async Task<IActionResult> GetUserDevices(Guid id)
        => Ok(await _mediator.Send(new GetUserDevicesQuery(id)));

    [HttpDelete("devices/{id:guid}")]
    public async Task<IActionResult> RemoveDevice(Guid id)
    {
        var result = await _mediator.Send(new RemoveDeviceCommand(id, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // --- Codes ---
    [HttpPost("codes/bulk-generate")]
    public async Task<IActionResult> BulkGenerateCodes([FromBody] BulkGenerateCodesCommand command)
    {
        var result = await _mediator.Send(command with { AdminId = GetUserId() });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("codes/groups")]
    public async Task<IActionResult> ListCodeGroups()
        => Ok(await _mediator.Send(new ListCodeGroupsQuery()));

    [HttpGet("codes/groups/{id:guid}/details")]
    public async Task<IActionResult> GetCodeGroupDetails(Guid id)
    {
        var result = await _mediator.Send(new GetCodeGroupCodesQuery(id));
        return result.Success ? Ok(result) : NotFound(result);
    }

    // --- Content ---
    [HttpPost("packages")]
    public async Task<IActionResult> CreatePackage(CreatePackageCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreatePackage), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPost("sections")]
    public async Task<IActionResult> CreateSection(CreateSectionCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateSection), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPost("lessons")]
    public async Task<IActionResult> CreateLesson(CreateLessonCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateLesson), new { id = result.Data }, result) : BadRequest(result);
    }

    [HttpPost("videos")]
    public async Task<IActionResult> CreateVideo(CreateVideoCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateVideo), new { id = result.Data }, result) : BadRequest(result);
    }

    // --- Questions ---
    [HttpGet("questions")]
    public async Task<IActionResult> ListQuestions([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _mediator.Send(new ListQuestionsQuery(page, pageSize, search)));

    [HttpPost("questions")]
    public async Task<IActionResult> CreateQuestion(CreateQuestionCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? CreatedAtAction(nameof(CreateQuestion), new { id = result.Data }, result) : BadRequest(result);
    }

    // --- Overrides ---
    [HttpPost("overrides/reset-watch")]
    public async Task<IActionResult> ResetWatchLimit([FromBody] ResetWatchRequest dto)
    {
        var result = await _mediator.Send(new ResetWatchLimitCommand(dto.LessonVideoId, dto.StudentId, GetUserId()));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record UpdateUserStatusRequest(string Status);
public record ResetWatchRequest(Guid LessonVideoId, Guid StudentId);
