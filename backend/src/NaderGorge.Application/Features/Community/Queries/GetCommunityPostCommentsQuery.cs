using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Community.Queries;

public record CommunityPostCommentDto(
    Guid Id,
    Guid PostId,
    string AuthorName,
    string Body,
    DateTime CreatedAt,
    bool IsOwnComment,
    string? AuthorAvatarSlug,
    bool IsPinned
);

public record GetCommunityPostCommentsQuery(Guid PostId, Guid UserId)
    : IRequest<ApiResponse<List<CommunityPostCommentDto>>>;

public class GetCommunityPostCommentsQueryHandler : IRequestHandler<GetCommunityPostCommentsQuery, ApiResponse<List<CommunityPostCommentDto>>>
{
    private readonly IAppDbContext _db;

    public GetCommunityPostCommentsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<CommunityPostCommentDto>>> Handle(GetCommunityPostCommentsQuery request, CancellationToken ct)
    {
        var postExists = await _db.CommunityPosts
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.PostId && p.Status == CommunityPostStatus.Approved, ct);

        if (!postExists)
            return ApiResponse<List<CommunityPostCommentDto>>.Fail("Post not found", new List<string> { "NOT_FOUND" });

        var comments = await _db.CommunityPostComments
            .AsNoTracking()
            .Where(c => c.PostId == request.PostId && c.Status == CommunityCommentStatus.Approved)
            .Select(c => new
            {
                Comment = c,
                IsPinned = c.AuthorUser.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher || ur.Role.Type == RoleType.Admin)
            })
            .OrderByDescending(x => x.IsPinned)
            .ThenBy(x => x.Comment.CreatedAt)
            .Select(x => new CommunityPostCommentDto(
                x.Comment.Id,
                x.Comment.PostId,
                x.Comment.AuthorUser.FullName,
                x.Comment.Body,
                x.Comment.CreatedAt,
                x.Comment.AuthorUserId == request.UserId,
                x.Comment.AuthorUser.StudentProfile != null ? x.Comment.AuthorUser.StudentProfile.AvatarSlug : null,
                x.IsPinned
            ))
            .ToListAsync(ct);

        return ApiResponse<List<CommunityPostCommentDto>>.Ok(comments);
    }
}
