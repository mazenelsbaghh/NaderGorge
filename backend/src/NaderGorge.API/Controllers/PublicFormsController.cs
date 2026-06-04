using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.Application.Features.Public.Commands;
using NaderGorge.Application.Features.Public.Queries;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/public/forms")]
[AllowAnonymous]
public class PublicFormsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicFormsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetPublicForm(string slug)
    {
        var result = await _mediator.Send(new GetPublicFormQuery(slug));
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPost("{slug}/submit")]
    [EnableRateLimiting("public-forms")]
    public async Task<IActionResult> SubmitPublicForm(string slug, [FromBody] Dictionary<string, string>? answers)
    {
        if (answers is null)
            return BadRequest(new { success = false, message = "Form submission is required." });

        if (answers.Count > 50 || answers.Any(a => a.Key.Length > 128 || a.Value is null || a.Value.Length > 4_000))
            return BadRequest(new { success = false, message = "Form submission is too large." });

        var result = await _mediator.Send(new SubmitPublicFormCommand(slug, answers));
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
