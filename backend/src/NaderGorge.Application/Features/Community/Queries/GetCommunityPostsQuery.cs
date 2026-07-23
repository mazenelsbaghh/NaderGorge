using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Community.Queries;

public record CommunityPostPollOptionDto(Guid Id, string Text, int VoteCount);

public record CommunityPostFeedDto(
    Guid Id,
    string AuthorName,
    string Body,
    DateTime CreatedAt,
    int LikeCount,
    int CommentCount,
    bool IsLikedByCurrentUser,
    bool IsPoll,
    Guid? UserVoteOptionId,
    List<CommunityPostPollOptionDto> PollOptions,
    string? AuthorAvatarSlug
);

public record GetCommunityPostsQuery(Guid UserId, Guid? TeacherId = null) : IRequest<ApiResponse<List<CommunityPostFeedDto>>>;

public class GetCommunityPostsQueryHandler : IRequestHandler<GetCommunityPostsQuery, ApiResponse<List<CommunityPostFeedDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService _academicScope;

    public GetCommunityPostsQueryHandler(IAppDbContext db, IAcademicScopeService academicScope)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<List<CommunityPostFeedDto>>> Handle(GetCommunityPostsQuery request, CancellationToken ct)
    {
        var postIds = await _db.CommunityPosts
            .AsNoTracking()
            .Where(p =>
                p.Status == CommunityPostStatus.Approved &&
                (request.TeacherId.HasValue ? p.TeacherId == request.TeacherId.Value : p.TeacherId == null))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var eligiblePostIds = new List<Guid>();
        foreach (var postId in postIds)
        {
            if (await _academicScope.IsOwnerEligibleForStudentAsync(
                    StudentFacingScopeOwnerType.CommunityPost,
                    postId,
                    request.UserId,
                    ct))
            {
                eligiblePostIds.Add(postId);
            }
        }

        var posts = await _db.CommunityPosts
            .AsNoTracking()
            .Where(p =>
                eligiblePostIds.Contains(p.Id))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new CommunityPostFeedDto(
                p.Id,
                p.AuthorUser.FullName,
                p.Body,
                p.CreatedAt,
                _db.CommunityPostLikes.Count(l => l.PostId == p.Id),
                _db.CommunityPostComments.Count(c => c.PostId == p.Id),
                _db.CommunityPostLikes.Any(l => l.PostId == p.Id && l.UserId == request.UserId),
                p.IsPoll,
                p.IsPoll ? _db.CommunityPostPollVotes.Where(v => v.PostId == p.Id && v.UserId == request.UserId).Select(v => (Guid?)v.PollOptionId).FirstOrDefault() : null,
                p.IsPoll ? p.PollOptions.Select(o => new CommunityPostPollOptionDto(
                    o.Id,
                    o.Text,
                    _db.CommunityPostPollVotes.Count(v => v.PollOptionId == o.Id)
                )).ToList() : new List<CommunityPostPollOptionDto>(),
                p.AuthorUser.StudentProfile != null ? p.AuthorUser.StudentProfile.AvatarSlug : null
            ))
            .ToListAsync(ct);

        return ApiResponse<List<CommunityPostFeedDto>>.Ok(posts);
    }
}
