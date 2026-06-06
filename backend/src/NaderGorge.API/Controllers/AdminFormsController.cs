using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using System;
using System.Threading.Tasks;

using NaderGorge.API.Extensions;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/forms")]
[Authorize]
[HasPermission("users.manage")]
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

    [HttpPost("cover/upload")]
    public async Task<IActionResult> UploadCoverImage([FromBody] UploadCoverImageRequest dto)
    {
        try
        {
            var base64Data = dto.Base64Image.Contains(",") ? dto.Base64Image.Split(',')[1] : dto.Base64Image;
            var bytes = Convert.FromBase64String(base64Data);

            var uploadsDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "uploads", "form-covers");
            if (!System.IO.Directory.Exists(uploadsDir))
                System.IO.Directory.CreateDirectory(uploadsDir);

            var uniqueFileName = $"{Guid.NewGuid()}_{System.IO.Path.GetFileName(dto.FileName)}";
            var filePath = System.IO.Path.Combine(uploadsDir, uniqueFileName);

            await System.IO.File.WriteAllBytesAsync(filePath, bytes);

            var relativeUrl = $"/uploads/form-covers/{uniqueFileName}";
            return Ok(new { Success = true, Data = relativeUrl });
        }
        catch (FormatException)
        {
            return BadRequest(new { Success = false, Message = "Invalid image data format" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Success = false, Message = $"Could not save the image: {ex.Message}" });
        }
    }
}

public record UploadCoverImageRequest(string Base64Image, string FileName);
