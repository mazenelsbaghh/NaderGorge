using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    : IRequest<ApiResponse<ModerateCommunityCommentResponse>>
{
    public string? OperationId { get; init; }
}

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
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<ModerateCommunityCommentResponse>.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await ApproveOnceAsync(request, ct);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.CommentId, request.ReviewerUserId }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
        {
            if (prior.Scope != "community-comment.approve" || prior.ActorUserId != request.ReviewerUserId
                || prior.RequestHash != requestHash || prior.ResultEntityId != request.CommentId)
                return ApiResponse<ModerateCommunityCommentResponse>.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);
            var result = JsonSerializer.Deserialize<ModerateCommunityCommentResponse>(prior.SafeResultJson
                ?? throw new InvalidOperationException("Community comment approval receipt has no result."));
            if (result?.CommentId != request.CommentId)
                throw new InvalidOperationException("Community comment approval receipt result does not match its target.");
            return ApiResponse<ModerateCommunityCommentResponse>.Ok(result);
        }

        var approved = await ApproveOnceAsync(request, ct);
        if (!approved.Success)
            return approved;
        _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
        {
            OperationId = operationId, Scope = "community-comment.approve",
            ActorUserId = request.ReviewerUserId, RequestHash = requestHash,
            ResultEntityId = request.CommentId, SafeResultJson = JsonSerializer.Serialize(approved.Data)
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return approved;
    }

    private async Task<ApiResponse<ModerateCommunityCommentResponse>> ApproveOnceAsync(
        ApproveCommunityCommentCommand request, CancellationToken ct)
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
