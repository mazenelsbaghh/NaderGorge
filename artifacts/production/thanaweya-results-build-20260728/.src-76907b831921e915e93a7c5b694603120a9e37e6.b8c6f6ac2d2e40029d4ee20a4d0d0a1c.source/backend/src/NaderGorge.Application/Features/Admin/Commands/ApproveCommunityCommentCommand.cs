using MediatR;
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
        var comment = await _db.CommunityPostComments.FindAsync(new object[] { request.CommentId }, ct);
        if (comment == null)
        {
            return ApiResponse<ModerateCommunityCommentResponse>.Fail("Comment not found", new List<string> { "NOT_FOUND" });
        }

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
