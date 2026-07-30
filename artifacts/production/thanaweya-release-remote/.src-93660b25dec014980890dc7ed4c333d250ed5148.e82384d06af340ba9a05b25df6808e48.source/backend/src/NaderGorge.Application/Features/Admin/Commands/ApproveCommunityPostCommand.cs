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
    : IRequest<ApiResponse<ModerateCommunityPostResponse>>;

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
