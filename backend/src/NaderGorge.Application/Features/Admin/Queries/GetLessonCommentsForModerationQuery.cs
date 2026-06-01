using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record ModerationLessonCommentDto(
    Guid Id,
    Guid LessonId,
    string LessonTitle,
    Guid StudentId,
    string StudentName,
    string Body,
    string Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByName
);

public record GetLessonCommentsForModerationQuery(Guid LessonId, string? Status = null)
    : IRequest<ApiResponse<List<ModerationLessonCommentDto>>>;

public class GetLessonCommentsForModerationQueryHandler
    : IRequestHandler<GetLessonCommentsForModerationQuery, ApiResponse<List<ModerationLessonCommentDto>>>
{
    private readonly IAppDbContext _context;

    public GetLessonCommentsForModerationQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<ModerationLessonCommentDto>>> Handle(GetLessonCommentsForModerationQuery request, CancellationToken cancellationToken)
    {
        var lesson = await _context.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, cancellationToken);

        if (lesson == null)
            return ApiResponse<List<ModerationLessonCommentDto>>.Fail("Lesson not found", new List<string> { "NOT_FOUND" });

        LessonCommentStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<LessonCommentStatus>(request.Status.Trim(), ignoreCase: true, out var status))
            {
                return ApiResponse<List<ModerationLessonCommentDto>>.Fail("Invalid status filter", new List<string> { "INVALID_STATUS" });
            }

            parsedStatus = status;
        }

        var query = _context.LessonComments
            .AsNoTracking()
            .Include(c => c.AuthorUser)
            .Include(c => c.ReviewedByUser)
            .Where(c => c.LessonId == request.LessonId);

        if (parsedStatus.HasValue)
        {
            query = query.Where(c => c.Status == parsedStatus.Value);
        }

        var comments = await query
            .OrderBy(c => c.Status == NaderGorge.Domain.Enums.LessonCommentStatus.Pending ? 0 : 1)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new ModerationLessonCommentDto(
                c.Id,
                c.LessonId,
                lesson.Title,
                c.AuthorUserId,
                c.AuthorUser.FullName,
                c.Body,
                c.Status.ToString(),
                c.CreatedAt,
                c.ReviewedAt,
                c.ReviewedByUser != null ? c.ReviewedByUser.FullName : null
            ))
            .ToListAsync(cancellationToken);

        return ApiResponse<List<ModerationLessonCommentDto>>.Ok(comments);
    }
}
