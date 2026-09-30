using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Admin.Commands;

public record ModerateCommunityCommentResponse(
    Guid CommentId,
    string Status,
    string? RejectionReason
);

public record ApproveCommunityCommentCommand(Guid CommentId, Guid ReviewerUserId)
    : IRequest<ApiResponse<ModerateCommunityCommentResponse>>;

public class ApproveCommunityCommentCommandHandler
    : IRequestHandler<ApproveCommunityCommentCommand, ApiResponse<ModerateCommunityCommentResponse>>
{
    private readonly IAppDbContext _db;

    public ApproveCommunityCommentCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ModerateCommunityCommentResponse>> Handle(ApproveCommunityCommentCommand request, CancellationToken ct)
    {
        var comment = await _db.CommunityPostComments
            .Include(item => item.Post)
            .Include(item => item.ParentComment)
            .FirstOrDefaultAsync(item => item.Id == request.CommentId, ct);
        if (comment == null)
        {
            return ApiResponse<ModerateCommunityCommentResponse>.Fail("Comment not found", new List<string> { "NOT_FOUND" });
        }

        if (comment.Status != CommunityCommentStatus.Pending)
            return ApiResponse<ModerateCommunityCommentResponse>.Fail("Comment is already resolved", ["ALREADY_RESOLVED"]);
        if (comment.Post.Status != CommunityPostStatus.Approved)
            return ApiResponse<ModerateCommunityCommentResponse>.Fail("Post must be approved first", ["POST_NOT_APPROVED"]);
        if (comment.ParentComment != null && (comment.ParentComment.PostId != comment.PostId
            || comment.ParentComment.Status != CommunityCommentStatus.Approved))
            return ApiResponse<ModerateCommunityCommentResponse>.Fail("Parent comment must be approved first", ["PARENT_NOT_APPROVED"]);

        comment.Status = CommunityCommentStatus.Approved;
        comment.RejectionReason = null;
        comment.ReviewedAt = DateTime.UtcNow;
        comment.ReviewedByUserId = request.ReviewerUserId;

        var approvedEvent = new OutboxEvent
        {
            Type = "CommunityCommentApproved",
            TargetGroup = "Public",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                commentId = comment.Id,
                postId = comment.PostId,
                authorId = comment.AuthorUserId,
                body = comment.Body
            })
        };
        _db.OutboxEvents.Add(approvedEvent);

        await _db.SaveChangesAsync(ct);

        return ApiResponse<ModerateCommunityCommentResponse>.Ok(
            new ModerateCommunityCommentResponse(comment.Id, comment.Status.ToString(), comment.RejectionReason));
    }
}
