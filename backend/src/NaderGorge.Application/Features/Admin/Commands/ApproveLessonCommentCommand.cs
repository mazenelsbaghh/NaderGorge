using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record ModerateLessonCommentResponse(
    Guid Id,
    string Status,
    DateTime? ReviewedAt,
    Guid? ReviewedByUserId
);

public record ApproveLessonCommentCommand(Guid CommentId, Guid ReviewerUserId)
    : IRequest<ApiResponse<ModerateLessonCommentResponse>>
{
    public string? OperationId { get; init; }
}

public class ApproveLessonCommentCommandHandler
    : IRequestHandler<ApproveLessonCommentCommand, ApiResponse<ModerateLessonCommentResponse>>
{
    private readonly IAppDbContext _context;

    public ApproveLessonCommentCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ModerateLessonCommentResponse>> Handle(ApproveLessonCommentCommand request, CancellationToken cancellationToken)
    {
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<ModerateLessonCommentResponse>.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await ApproveOnceAsync(request, cancellationToken);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.CommentId, request.ReviewerUserId }))));
        await using var transaction = await _context.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var prior = await _context.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, cancellationToken);
        if (prior is not null)
        {
            if (prior.Scope != "lesson-comment.approve" || prior.ActorUserId != request.ReviewerUserId
                || prior.RequestHash != requestHash || prior.ResultEntityId != request.CommentId)
                return ApiResponse<ModerateLessonCommentResponse>.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);
            var result = JsonSerializer.Deserialize<ModerateLessonCommentResponse>(prior.SafeResultJson
                ?? throw new InvalidOperationException("Lesson comment approval receipt has no result."));
            if (result?.Id != request.CommentId)
                throw new InvalidOperationException("Lesson comment approval receipt result does not match its target.");
            return ApiResponse<ModerateLessonCommentResponse>.Ok(result);
        }

        var approved = await ApproveOnceAsync(request, cancellationToken);
        if (!approved.Success)
            return approved;
        _context.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
        {
            OperationId = operationId, Scope = "lesson-comment.approve",
            ActorUserId = request.ReviewerUserId, RequestHash = requestHash,
            ResultEntityId = request.CommentId, SafeResultJson = JsonSerializer.Serialize(approved.Data)
        });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return approved;
    }

    private async Task<ApiResponse<ModerateLessonCommentResponse>> ApproveOnceAsync(
        ApproveLessonCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await _context.LessonComments
            .Include(c => c.ParentComment)
            .FirstOrDefaultAsync(c => c.Id == request.CommentId, cancellationToken);

        if (comment == null)
            return ApiResponse<ModerateLessonCommentResponse>.Fail("Comment not found", new List<string> { "NOT_FOUND" });

        if (comment.Status != LessonCommentStatus.Pending)
            return ApiResponse<ModerateLessonCommentResponse>.Fail("Comment is already resolved", new List<string> { "ALREADY_RESOLVED" });

        if (comment.ParentComment != null && comment.ParentComment.Status != LessonCommentStatus.Approved)
            return ApiResponse<ModerateLessonCommentResponse>.Fail("انشر التعليق الأصلي أولًا قبل نشر الرد.", new List<string> { "PARENT_NOT_APPROVED" });

        comment.Status = LessonCommentStatus.Approved;
        comment.ReviewedAt = DateTime.UtcNow;
        comment.ReviewedByUserId = request.ReviewerUserId;
        comment.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            Action = "ApproveLessonComment",
            EntityType = nameof(LessonComment),
            EntityId = comment.Id,
            PerformedByUserId = request.ReviewerUserId,
            OldValues = $"Status={LessonCommentStatus.Pending}",
            NewValues = $"Status={LessonCommentStatus.Approved};ReviewedAt={comment.ReviewedAt:O}",
        });

        var lessonEvent = new OutboxEvent
        {
            Type = "LessonCommentApproved",
            TargetGroup = $"Lesson_{comment.LessonId}",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                commentId = comment.Id,
                lessonId = comment.LessonId,
                authorUserId = comment.AuthorUserId,
                body = comment.Body,
                status = comment.Status.ToString()
            })
        };
        _context.OutboxEvents.Add(lessonEvent);

        var authorEvent = new OutboxEvent
        {
            Type = "LessonCommentApproved",
            TargetUserId = comment.AuthorUserId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                commentId = comment.Id,
                lessonId = comment.LessonId,
                authorUserId = comment.AuthorUserId,
                body = comment.Body,
                status = comment.Status.ToString()
            })
        };
        _context.OutboxEvents.Add(authorEvent);

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<ModerateLessonCommentResponse>.Ok(
            new ModerateLessonCommentResponse(comment.Id, comment.Status.ToString(), comment.ReviewedAt, comment.ReviewedByUserId)
        );
    }
}
