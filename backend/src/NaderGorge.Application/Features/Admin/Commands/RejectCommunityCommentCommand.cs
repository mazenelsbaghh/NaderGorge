using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record RejectCommunityCommentCommand(Guid CommentId, Guid ReviewerUserId, string Reason)
    : IRequest<ApiResponse<ModerateCommunityCommentResponse>>;

public class RejectCommunityCommentCommandHandler
    : IRequestHandler<RejectCommunityCommentCommand, ApiResponse<ModerateCommunityCommentResponse>>
{
    private readonly IAppDbContext _db;

    public RejectCommunityCommentCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ModerateCommunityCommentResponse>> Handle(RejectCommunityCommentCommand request, CancellationToken ct)
    {
        var comment = await _db.CommunityPostComments.FindAsync(new object[] { request.CommentId }, ct);
        if (comment == null)
        {
            return ApiResponse<ModerateCommunityCommentResponse>.Fail("Comment not found", new List<string> { "NOT_FOUND" });
        }

        var trimmedReason = request.Reason.Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            return ApiResponse<ModerateCommunityCommentResponse>.Fail("Rejection reason is required.", new List<string> { "VALIDATION_REASON_REQUIRED" });
        }

        comment.Status = CommunityCommentStatus.Rejected;
        comment.RejectionReason = trimmedReason;
        comment.ReviewedAt = DateTime.UtcNow;
        comment.ReviewedByUserId = request.ReviewerUserId;

        var authorEvent = new OutboxEvent
        {
            Type = "CommunityCommentRejected",
            TargetUserId = comment.AuthorUserId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                commentId = comment.Id,
                postId = comment.PostId,
                authorUserId = comment.AuthorUserId,
                status = comment.Status.ToString(),
                reason = comment.RejectionReason
            })
        };
        _db.OutboxEvents.Add(authorEvent);

        await _db.SaveChangesAsync(ct);

        return ApiResponse<ModerateCommunityCommentResponse>.Ok(
            new ModerateCommunityCommentResponse(comment.Id, comment.Status.ToString(), comment.RejectionReason));
    }
}
