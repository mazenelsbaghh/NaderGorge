using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Community.Commands;

public record ToggleCommunityPostLikeResponse(
    Guid PostId,
    bool IsLikedByCurrentUser,
    int LikeCount
);

public record ToggleCommunityPostLikeCommand(Guid PostId, Guid UserId)
    : IRequest<ApiResponse<ToggleCommunityPostLikeResponse>>;

public class ToggleCommunityPostLikeCommandHandler : IRequestHandler<ToggleCommunityPostLikeCommand, ApiResponse<ToggleCommunityPostLikeResponse>>
{
    private readonly IAppDbContext _db;

    public ToggleCommunityPostLikeCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ToggleCommunityPostLikeResponse>> Handle(ToggleCommunityPostLikeCommand request, CancellationToken ct)
    {
        var post = await _db.CommunityPosts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, ct);

        if (post == null || post.Status != CommunityPostStatus.Approved)
            return ApiResponse<ToggleCommunityPostLikeResponse>.Fail("Post not found", new List<string> { "NOT_FOUND" });

        var existingLike = await _db.CommunityPostLikes
            .FirstOrDefaultAsync(l => l.PostId == request.PostId && l.UserId == request.UserId, ct);

        var isLiked = false;
        if (existingLike == null)
        {
            _db.CommunityPostLikes.Add(new CommunityPostLike
            {
                PostId = request.PostId,
                UserId = request.UserId,
            });
            isLiked = true;
        }
        else
        {
            _db.CommunityPostLikes.Remove(existingLike);
        }

        await _db.SaveChangesAsync(ct);

        var likeCount = await _db.CommunityPostLikes.CountAsync(l => l.PostId == request.PostId, ct);

        return ApiResponse<ToggleCommunityPostLikeResponse>.Ok(
            new ToggleCommunityPostLikeResponse(request.PostId, isLiked, likeCount)
        );
    }
}
