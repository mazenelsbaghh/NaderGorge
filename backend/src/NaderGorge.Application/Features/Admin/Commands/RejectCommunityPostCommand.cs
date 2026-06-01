using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record RejectCommunityPostCommand(Guid PostId, Guid ReviewerUserId)
    : IRequest<ApiResponse<ModerateCommunityPostResponse>>;

public class RejectCommunityPostCommandHandler : IRequestHandler<RejectCommunityPostCommand, ApiResponse<ModerateCommunityPostResponse>>
{
    private readonly IAppDbContext _context;

    public RejectCommunityPostCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ModerateCommunityPostResponse>> Handle(RejectCommunityPostCommand request, CancellationToken cancellationToken)
    {
        var post = await _context.CommunityPosts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, cancellationToken);

        if (post == null)
            return ApiResponse<ModerateCommunityPostResponse>.Fail("Post not found", new List<string> { "NOT_FOUND" });

        if (post.Status != CommunityPostStatus.Pending)
            return ApiResponse<ModerateCommunityPostResponse>.Fail("Post is already resolved", new List<string> { "ALREADY_RESOLVED" });

        post.Status = CommunityPostStatus.Rejected;
        post.ReviewedAt = DateTime.UtcNow;
        post.ReviewedByUserId = request.ReviewerUserId;
        post.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            Action = "RejectCommunityPost",
            EntityType = nameof(CommunityPost),
            EntityId = post.Id,
            PerformedByUserId = request.ReviewerUserId,
            OldValues = $"Status={CommunityPostStatus.Pending}",
            NewValues = $"Status={CommunityPostStatus.Rejected};ReviewedAt={post.ReviewedAt:O}",
        });

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<ModerateCommunityPostResponse>.Ok(
            new ModerateCommunityPostResponse(post.Id, post.Status.ToString(), post.ReviewedAt, post.ReviewedByUserId)
        );
    }
}
