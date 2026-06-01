using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record LessonCommentDto(
    Guid Id,
    Guid LessonId,
    string AuthorName,
    string Body,
    string Status,
    DateTime CreatedAt,
    bool IsOwnComment,
    string? AuthorAvatarSlug
);

public record GetLessonCommentsQuery(Guid LessonId, Guid UserId) : IRequest<ApiResponse<List<LessonCommentDto>>>;

public class GetLessonCommentsQueryHandler : IRequestHandler<GetLessonCommentsQuery, ApiResponse<List<LessonCommentDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public GetLessonCommentsQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<List<LessonCommentDto>>> Handle(GetLessonCommentsQuery request, CancellationToken ct)
    {
        var hasAccess = await _access.HasAccessToLessonAsync(request.UserId, request.LessonId, ct);
        if (!hasAccess)
            return ApiResponse<List<LessonCommentDto>>.Fail("You do not have access to this lesson.", new List<string> { "FORBIDDEN" });

        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == request.LessonId, ct);
        if (!lessonExists)
            return ApiResponse<List<LessonCommentDto>>.Fail("Lesson not found", new List<string> { "NOT_FOUND" });

        var comments = await _db.LessonComments
            .AsNoTracking()
            .Include(c => c.AuthorUser)
            .Where(c => c.LessonId == request.LessonId && c.Status == LessonCommentStatus.Approved)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new LessonCommentDto(
                c.Id,
                c.LessonId,
                c.AuthorUser.FullName,
                c.Body,
                c.Status.ToString(),
                c.CreatedAt,
                c.AuthorUserId == request.UserId,
                c.AuthorUser.StudentProfile != null ? c.AuthorUser.StudentProfile.AvatarSlug : null
            ))
            .ToListAsync(ct);

        return ApiResponse<List<LessonCommentDto>>.Ok(comments);
    }
}
