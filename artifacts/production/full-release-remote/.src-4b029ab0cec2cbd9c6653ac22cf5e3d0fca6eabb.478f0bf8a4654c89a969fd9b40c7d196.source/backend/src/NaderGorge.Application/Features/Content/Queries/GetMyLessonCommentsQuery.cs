using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetMyLessonCommentsQuery(Guid LessonId, Guid UserId) : IRequest<ApiResponse<List<LessonCommentDto>>>;

public class GetMyLessonCommentsQueryHandler : IRequestHandler<GetMyLessonCommentsQuery, ApiResponse<List<LessonCommentDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public GetMyLessonCommentsQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<List<LessonCommentDto>>> Handle(GetMyLessonCommentsQuery request, CancellationToken ct)
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
            .Where(c => c.LessonId == request.LessonId &&
                        c.AuthorUserId == request.UserId &&
                        c.Status != LessonCommentStatus.Rejected)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new LessonCommentDto(
                c.Id,
                c.LessonId,
                c.AuthorUser.FullName,
                c.Body,
                c.Status.ToString(),
                c.CreatedAt,
                true,
                c.AuthorUser.StudentProfile != null ? c.AuthorUser.StudentProfile.AvatarSlug : null
            ))
            .ToListAsync(ct);

        return ApiResponse<List<LessonCommentDto>>.Ok(comments);
    }
}
