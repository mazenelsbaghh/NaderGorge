using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LearningCenter;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/learning-center")]
[Authorize(Roles = "Admin,Teacher")]
public sealed class LearningCenterController(IAppDbContext db) : ControllerBase
{
    private Guid ActorId => User.RequireUserId();

    [HttpGet("options")]
    public Task<IActionResult> Options(CancellationToken ct) => Read(() => new LearningQuestionBank(db).OptionsAsync(ActorId, ct));

    [HttpGet("overview")]
    public Task<IActionResult> Overview([FromQuery] LearningFilter filter, CancellationToken ct) =>
        Read(() => new LearningOverview(db).ReadAsync(ActorId, filter, ct));

    [HttpGet("questions")]
    public Task<IActionResult> Questions([FromQuery] LearningQuestionFilter filter, CancellationToken ct) =>
        Read(() => new LearningQuestionBank(db).ListAsync(ActorId, filter, ct));

    [HttpPost("questions/import")]
    public Task<IActionResult> ImportQuestions([FromBody] List<SaveLearningQuestion> request, CancellationToken ct) =>
        Read(() => new LearningQuestionBank(db).ImportAsync(ActorId, request, ct));

    [HttpPost("questions")]
    public Task<IActionResult> CreateQuestion([FromBody] SaveLearningQuestion request, CancellationToken ct) =>
        Read(() => new LearningQuestionBank(db).SaveAsync(ActorId, new(null, request), ct));

    [HttpPut("questions/{id:guid}")]
    public Task<IActionResult> VersionQuestion(Guid id, [FromBody] SaveLearningQuestion request, CancellationToken ct) =>
        Read(() => new LearningQuestionBank(db).SaveAsync(ActorId, new(id, request), ct));

    [HttpPut("questions/{id:guid}/classification")]
    public Task<IActionResult> Classify(Guid id, [FromBody] ClassifyLearningQuestion request, CancellationToken ct) =>
        Write(() => new LearningQuestionBank(db).ClassifyAsync(ActorId, new(id, request), ct));

    [HttpGet("follow-ups")]
    public Task<IActionResult> FollowUps([FromQuery] LearningFilter filter, CancellationToken ct) =>
        Read(() => new LearningFollowUps(db).ReadAsync(ActorId, filter, ct));

    [HttpGet("follow-ups/history")]
    public Task<IActionResult> History([FromQuery] FollowUpTarget target, CancellationToken ct) =>
        Read(() => new LearningFollowUps(db).HistoryAsync(ActorId, target, ct));

    [HttpPost("follow-ups")]
    public Task<IActionResult> SaveFollowUp([FromBody] SaveLearningFollowUp request, CancellationToken ct) =>
        Write(() => new LearningFollowUps(db).SaveAsync(ActorId, request, ct));

    [HttpPost("forms")]
    public Task<IActionResult> GenerateForms([FromBody] GenerateLearningForms request, CancellationToken ct) =>
        Read(() => new LearningFormGenerator(db).GenerateAsync(ActorId, request, ct));

    private async Task<IActionResult> Read<T>(Func<Task<T>> query)
    {
        try { return Ok(ApiResponse<T>.Ok(await query())); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    private async Task<IActionResult> Write(Func<Task> command)
    {
        try { await command(); return Ok(ApiResponse.Ok()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }
}
