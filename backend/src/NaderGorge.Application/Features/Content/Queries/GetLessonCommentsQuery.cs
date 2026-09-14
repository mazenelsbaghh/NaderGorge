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
    string? AuthorAvatarSlug,
    Guid? ParentCommentId = null,
    int ReplyCount = 0
);

public record GetLessonCommentsQuery(Guid LessonId, Guid UserId, int Offset = 0, int Limit = 50, Guid? ParentCommentId = null) : IRequest<ApiResponse<List<LessonCommentDto>>>;

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

        if (request.ParentCommentId.HasValue && !await _db.LessonComments.AnyAsync(
            c => c.Id == request.ParentCommentId && c.LessonId == request.LessonId
                && c.ParentCommentId == null && c.Status == LessonCommentStatus.Approved, ct))
            return ApiResponse<List<LessonCommentDto>>.Fail("التعليق غير متاح.", new List<string> { "NOT_FOUND" });

        var query = _db.LessonComments
            .AsNoTracking()
            .Where(c => c.LessonId == request.LessonId && c.ParentCommentId == request.ParentCommentId
                && (c.Status == LessonCommentStatus.Approved || (request.ParentCommentId != null
                    && c.AuthorUserId == request.UserId && c.Status == LessonCommentStatus.Pending)));
        var ordered = request.ParentCommentId.HasValue
            ? query.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            : query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id);
        var comments = await ordered
            .Skip(Math.Max(0, request.Offset))
            .Take(Math.Clamp(request.Limit, 1, 100))
            .Select(c => new LessonCommentDto(
                c.Id,
                c.LessonId,
                c.AuthorUser.FullName,
                c.Body,
                c.Status.ToString(),
                c.CreatedAt,
                c.AuthorUserId == request.UserId,
                c.AuthorUser.StudentProfile != null ? c.AuthorUser.StudentProfile.AvatarSlug : null,
                c.ParentCommentId,
                c.Replies.Count(r => r.Status == LessonCommentStatus.Approved
                    || (r.AuthorUserId == request.UserId && r.Status == LessonCommentStatus.Pending))
            ))
            .ToListAsync(ct);

        return ApiResponse<List<LessonCommentDto>>.Ok(comments);
    }
}
