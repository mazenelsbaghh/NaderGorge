using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using System;
using System.Threading.Tasks;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/forms")]
[Authorize(Roles = "Admin")]
public class AdminFormsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminFormsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> ListForms()
    {
        var result = await _mediator.Send(new ListFormsQuery());
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetFormDetails(Guid id)
    {
        var result = await _mediator.Send(new GetFormDetailsQuery(id));
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateForm([FromBody] CreateFormCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateForm(Guid id, [FromBody] UpdateFormCommand command)
    {
        if (id != command.Id) return BadRequest("Form ID mismatch");
        var result = await _mediator.Send(command);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteForm(Guid id)
    {
        var result = await _mediator.Send(new DeleteFormCommand(id));
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("{id:guid}/submissions")]
    public async Task<IActionResult> GetFormSubmissions(Guid id)
    {
        var result = await _mediator.Send(new ListSubmissionsQuery(id));
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("submissions/{submissionId:guid}/status")]
    public async Task<IActionResult> UpdateSubmissionStatus(Guid submissionId, [FromBody] UpdateSubmissionStatusCommand command)
    {
        if (submissionId != command.SubmissionId) return BadRequest("Submission ID mismatch");
        var result = await _mediator.Send(command);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
