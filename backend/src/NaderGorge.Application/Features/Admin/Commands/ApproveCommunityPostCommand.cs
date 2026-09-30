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

public record ModerateCommunityPostResponse(
    Guid Id,
    string Status,
    DateTime? ReviewedAt,
    Guid? ReviewedByUserId
);

public record ApproveCommunityPostCommand(Guid PostId, Guid ReviewerUserId)
    : IRequest<ApiResponse<ModerateCommunityPostResponse>>
{
    public string? OperationId { get; init; }
}

public class ApproveCommunityPostCommandHandler : IRequestHandler<ApproveCommunityPostCommand, ApiResponse<ModerateCommunityPostResponse>>
{
    private readonly IAppDbContext _context;
    private readonly IAcademicScopeService? _academicScope;

    public ApproveCommunityPostCommandHandler(IAppDbContext context, IAcademicScopeService? academicScope = null)
    {
        _context = context;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<ModerateCommunityPostResponse>> Handle(ApproveCommunityPostCommand request, CancellationToken cancellationToken)
    {
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<ModerateCommunityPostResponse>.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await ApproveOnceAsync(request, cancellationToken);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.PostId, request.ReviewerUserId }))));
        await using var transaction = await _context.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var prior = await _context.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, cancellationToken);
        if (prior is not null)
        {
            if (prior.Scope != "community-post.approve" || prior.ActorUserId != request.ReviewerUserId
                || prior.RequestHash != requestHash || prior.ResultEntityId != request.PostId)
                return ApiResponse<ModerateCommunityPostResponse>.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);
            var result = JsonSerializer.Deserialize<ModerateCommunityPostResponse>(prior.SafeResultJson
                ?? throw new InvalidOperationException("Community post approval receipt has no result."));
            if (result?.Id != request.PostId)
                throw new InvalidOperationException("Community post approval receipt result does not match its target.");
            return ApiResponse<ModerateCommunityPostResponse>.Ok(result);
        }

        var approved = await ApproveOnceAsync(request, cancellationToken);
        if (!approved.Success)
            return approved;
        _context.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
        {
            OperationId = operationId, Scope = "community-post.approve",
            ActorUserId = request.ReviewerUserId, RequestHash = requestHash,
            ResultEntityId = request.PostId, SafeResultJson = JsonSerializer.Serialize(approved.Data)
        });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return approved;
    }

    private async Task<ApiResponse<ModerateCommunityPostResponse>> ApproveOnceAsync(
        ApproveCommunityPostCommand request, CancellationToken cancellationToken)
    {
        var post = await _context.CommunityPosts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, cancellationToken);

        if (post == null)
            return ApiResponse<ModerateCommunityPostResponse>.Fail("Post not found", new List<string> { "NOT_FOUND" });

        if (post.Status != CommunityPostStatus.Pending)
            return ApiResponse<ModerateCommunityPostResponse>.Fail("Post is already resolved", new List<string> { "ALREADY_RESOLVED" });

        if (_academicScope != null && !post.TeacherId.HasValue)
        {
            var scopeResult = await _academicScope.ValidateTargetHasScopeAsync(
                StudentFacingScopeOwnerType.CommunityPost,
                post.Id,
                cancellationToken);
            if (!scopeResult.IsEligible)
            {
                return ApiResponse<ModerateCommunityPostResponse>.Fail(
                    scopeResult.Message ?? "منشور المجتمع يجب أن يكون مربوطا بنطاق أكاديمي صالح قبل النشر.",
                    new List<string> { scopeResult.ErrorCode ?? "ACADEMIC_SCOPE_TARGET_UNSCOPED" });
            }
        }

        post.Status = CommunityPostStatus.Approved;
        post.ReviewedAt = DateTime.UtcNow;
        post.ReviewedByUserId = request.ReviewerUserId;
        post.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            Action = "ApproveCommunityPost",
            EntityType = nameof(CommunityPost),
            EntityId = post.Id,
            PerformedByUserId = request.ReviewerUserId,
            OldValues = $"Status={CommunityPostStatus.Pending}",
            NewValues = $"Status={CommunityPostStatus.Approved};ReviewedAt={post.ReviewedAt:O}",
        });

        var outboxEvent = new OutboxEvent
        {
            Type = "CommunityPostApproved",
            TargetGroup = "Public",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                postId = post.Id,
                authorId = post.AuthorUserId,
                body = post.Body
            })
        };
        _context.OutboxEvents.Add(outboxEvent);

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<ModerateCommunityPostResponse>.Ok(
            new ModerateCommunityPostResponse(post.Id, post.Status.ToString(), post.ReviewedAt, post.ReviewedByUserId)
        );
    }
}
