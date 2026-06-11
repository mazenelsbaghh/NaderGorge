using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Content.Commands;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;
    private readonly IConfiguration _config;

    public ContentController(IMediator mediator, IAppDbContext db, IAccessCheckService access, IConfiguration config)
    {
        _mediator = mediator;
        _db = db;
        _access = access;
        _config = config;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("packages")]
    public async Task<IActionResult> GetPackages()
    {
        var response = await _mediator.Send(new GetPackagesQuery(GetUserId()));
        if (!response.Success) return BadRequest(response); // Shouldn't happen for packages

        return Ok(response);
    }

    [HttpGet("packages/{packageId:guid}/terms")]
    public async Task<IActionResult> GetTerms(Guid packageId)
    {
        var response = await _mediator.Send(new GetTermsQuery(packageId));
        return Ok(response);
    }

    [HttpGet("packages/{packageId:guid}/code-page")]
    public async Task<IActionResult> GetPackageCodePage(Guid packageId)
    {
        var response = await _mediator.Send(new GetPackageCodePageQuery(packageId));
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpGet("terms/{termId:guid}/sections")]
    public async Task<IActionResult> GetSections(Guid termId)
    {
        var response = await _mediator.Send(new GetSectionsQuery(termId));
        return Ok(response);
    }

    [HttpGet("sections/{sectionId:guid}/lessons")]
    public async Task<IActionResult> GetLessons(Guid sectionId)
    {
        var response = await _mediator.Send(new GetLessonsQuery(sectionId, GetUserId()));

        if (!response.Success && (response.Errors?.Contains("Section not found") == true || response.Message?.Contains("Section not found") == true))
            return NotFound(response);

        return Ok(response);
    }

    [HttpGet("lessons/{lessonId:guid}")]
    public async Task<IActionResult> GetLessonDetail(Guid lessonId)
    {
        var response = await _mediator.Send(new GetLessonDetailQuery(lessonId, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors?.Contains("You do not have access") == true || response.Message?.Contains("You do not have access") == true)
                return StatusCode(403, response);

            return NotFound(response);
        }

        return Ok(response);
    }

    [HttpGet("lessons/{lessonId:guid}/comments")]
    public async Task<IActionResult> GetLessonComments(Guid lessonId, [FromQuery] int offset = 0, [FromQuery] int limit = 50)
    {
        var response = await _mediator.Send(new GetLessonCommentsQuery(lessonId, GetUserId(), offset, limit));

        if (!response.Success)
        {
            if (response.Errors?.Contains("FORBIDDEN") == true)
                return StatusCode(403, response);

            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("lessons/{lessonId:guid}/resources")]
    public async Task<IActionResult> GetLessonResources(Guid lessonId)
    {
        var response = await _mediator.Send(new GetLessonResourcesQuery(lessonId, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors?.Contains("FORBIDDEN") == true)
                return StatusCode(403, response);

            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("lessons/{lessonId:guid}/comments/mine")]
    public async Task<IActionResult> GetMyLessonComments(Guid lessonId)
    {
        var response = await _mediator.Send(new GetMyLessonCommentsQuery(lessonId, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors?.Contains("FORBIDDEN") == true)
                return StatusCode(403, response);

            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("lessons/{lessonId:guid}/comments")]
    public async Task<IActionResult> CreateLessonComment(Guid lessonId, [FromBody] CreateLessonCommentRequest request)
    {
        var response = await _mediator.Send(new CreateLessonCommentCommand(lessonId, GetUserId(), request.Body));

        if (!response.Success)
        {
            if (response.Errors?.Contains("FORBIDDEN") == true)
                return StatusCode(403, response);

            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("resources/{resourceId:guid}/sign-download")]
    [EnableRateLimiting("sign-download")]
    public async Task<IActionResult> SignDownload(Guid resourceId, CancellationToken ct)
    {
        var userId = GetUserId();
        var resource = await _db.LessonResources.FirstOrDefaultAsync(r => r.Id == resourceId, ct);
        if (resource == null)
            return NotFound(new { Success = false, Message = "Resource not found" });

        var hasAccess = await _access.HasAccessToLessonAsync(userId, resource.LessonId, ct);
        if (!hasAccess)
            return StatusCode(403, new { Success = false, Message = "You do not have access to this resource." });

        var secret = _config["JwtSettings:Secret"];
        if (string.IsNullOrEmpty(secret))
            return StatusCode(500, new { Success = false, Message = "Server configuration error" });

        var expiresUnixSeconds = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var payload = $"{userId}:{expiresUnixSeconds}";
        
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{resourceId}:{payload}"));
        var signature = Convert.ToHexString(hashBytes);

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}:{signature}"));
        var downloadUrl = $"/api/public/resources/{resourceId}/download?token={Uri.EscapeDataString(token)}";

        return Ok(new { Success = true, DownloadUrl = downloadUrl });
    }
}

public record CreateLessonCommentRequest(string Body);
