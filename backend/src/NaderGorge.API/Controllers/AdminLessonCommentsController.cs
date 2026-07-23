using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.API.Extensions;
using NaderGorge.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
[HasPermission("comments.manage")]
public class AdminLessonCommentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;

    public AdminLessonCommentsController(IMediator mediator, IAppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    private Guid GetUserId() => User.RequireUserId();

    [HttpGet("comments")]
    public async Task<IActionResult> GetAllLessonComments([FromQuery] Guid? teacherId, [FromQuery] string? status, CancellationToken ct)
    {
        NaderGorge.Domain.Enums.LessonCommentStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<NaderGorge.Domain.Enums.LessonCommentStatus>(status, true, out var parsed)) return BadRequest();
            parsedStatus = parsed;
        }
        var query = _db.LessonComments.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) query = query.Where(comment => comment.Lesson.ContentSection.Term.Package.TeacherId == teacherId.Value);
        if (parsedStatus.HasValue) query = query.Where(comment => comment.Status == parsedStatus.Value);
        var comments = await query.OrderByDescending(comment => comment.CreatedAt).Select(comment => new ModerationLessonCommentDto(comment.Id, comment.LessonId, comment.Lesson.Title, comment.Lesson.ContentSection.Term.Package.Teacher.User.FullName, comment.Lesson.ContentSection.Term.Package.Name, comment.Lesson.ContentSection.Term.Title, comment.Lesson.ContentSection.Title, comment.AuthorUserId, comment.AuthorUser.FullName, comment.Body, comment.Status.ToString(), comment.CreatedAt, comment.ReviewedAt, comment.ReviewedByUser != null ? comment.ReviewedByUser.FullName : null)).ToListAsync(ct);
        return Ok(NaderGorge.Application.Common.ApiResponse<List<ModerationLessonCommentDto>>.Ok(comments));
    }

    [HttpGet("lessons/{lessonId:guid}/comments")]
    public async Task<IActionResult> GetLessonCommentsForModeration(Guid lessonId, [FromQuery] string? status = null)
    {
        var response = await _mediator.Send(new GetLessonCommentsForModerationQuery(lessonId, status));

        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            if (response.Errors?.Contains("INVALID_STATUS") == true)
                return BadRequest(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("comments/{commentId:guid}/approve")]
    public async Task<IActionResult> ApproveLessonComment(Guid commentId)
    {
        var response = await _mediator.Send(new ApproveLessonCommentCommand(commentId, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("comments/{commentId:guid}/reject")]
    public async Task<IActionResult> RejectLessonComment(Guid commentId)
    {
        var response = await _mediator.Send(new RejectLessonCommentCommand(commentId, GetUserId()));

        if (!response.Success)
        {
            if (response.Errors?.Contains("NOT_FOUND") == true)
                return NotFound(response);

            return BadRequest(response);
        }

        return Ok(response);
    }
}
