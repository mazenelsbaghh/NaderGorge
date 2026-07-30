using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record ModerationCommunityPostDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string Body,
    string Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByName,
    int CommentCount,
    int LikeCount
);

public record GetCommunityPostsForModerationQuery(string? Status = null)
    : IRequest<ApiResponse<List<ModerationCommunityPostDto>>>;

public class GetCommunityPostsForModerationQueryHandler : IRequestHandler<GetCommunityPostsForModerationQuery, ApiResponse<List<ModerationCommunityPostDto>>>
{
    private readonly IAppDbContext _context;

    public GetCommunityPostsForModerationQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<ModerationCommunityPostDto>>> Handle(GetCommunityPostsForModerationQuery request, CancellationToken cancellationToken)
    {
        CommunityPostStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<CommunityPostStatus>(request.Status.Trim(), ignoreCase: true, out var status))
            {
                return ApiResponse<List<ModerationCommunityPostDto>>.Fail("Invalid status filter", new List<string> { "INVALID_STATUS" });
            }

            parsedStatus = status;
        }

        var query = _context.CommunityPosts
            .AsNoTracking()
            .Include(p => p.AuthorUser)
            .Include(p => p.ReviewedByUser)
            .AsQueryable();

        if (parsedStatus.HasValue)
        {
            query = query.Where(p => p.Status == parsedStatus.Value);
        }

        var basePosts = await query
            .OrderBy(p => p.Status == NaderGorge.Domain.Enums.CommunityPostStatus.Pending ? 0 : 1)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                StudentId = p.AuthorUserId,
                StudentName = p.AuthorUser.FullName,
                p.Body,
                Status = p.Status.ToString(),
                p.CreatedAt,
                p.ReviewedAt,
                ReviewedByName = p.ReviewedByUser != null ? p.ReviewedByUser.FullName : null
            })
            .ToListAsync(cancellationToken);

        var postIds = basePosts.Select(post => post.Id).ToList();
        var commentCounts = postIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _context.CommunityPostComments
                .AsNoTracking()
                .Where(comment => postIds.Contains(comment.PostId))
                .GroupBy(comment => comment.PostId)
                .Select(group => new { PostId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.PostId, item => item.Count, cancellationToken);

        var likeCounts = postIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _context.CommunityPostLikes
                .AsNoTracking()
                .Where(like => postIds.Contains(like.PostId))
                .GroupBy(like => like.PostId)
                .Select(group => new { PostId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.PostId, item => item.Count, cancellationToken);

        var posts = basePosts
            .Select(post => new ModerationCommunityPostDto(
                post.Id,
                post.StudentId,
                post.StudentName,
                post.Body,
                post.Status,
                post.CreatedAt,
                post.ReviewedAt,
                post.ReviewedByName,
                commentCounts.GetValueOrDefault(post.Id),
                likeCounts.GetValueOrDefault(post.Id)))
            .ToList();

        return ApiResponse<List<ModerationCommunityPostDto>>.Ok(posts);
    }
}
