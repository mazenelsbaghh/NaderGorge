using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record RejectLessonCommentCommand(Guid CommentId, Guid ReviewerUserId)
    : IRequest<ApiResponse<ModerateLessonCommentResponse>>;

public class RejectLessonCommentCommandHandler
    : IRequestHandler<RejectLessonCommentCommand, ApiResponse<ModerateLessonCommentResponse>>
{
    private readonly IAppDbContext _context;

    public RejectLessonCommentCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ModerateLessonCommentResponse>> Handle(RejectLessonCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await _context.LessonComments
            .FirstOrDefaultAsync(c => c.Id == request.CommentId, cancellationToken);

        if (comment == null)
            return ApiResponse<ModerateLessonCommentResponse>.Fail("Comment not found", new List<string> { "NOT_FOUND" });

        if (comment.Status != LessonCommentStatus.Pending)
            return ApiResponse<ModerateLessonCommentResponse>.Fail("Comment is already resolved", new List<string> { "ALREADY_RESOLVED" });

        comment.Status = LessonCommentStatus.Rejected;
        comment.ReviewedAt = DateTime.UtcNow;
        comment.ReviewedByUserId = request.ReviewerUserId;
        comment.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            Action = "RejectLessonComment",
            EntityType = nameof(LessonComment),
            EntityId = comment.Id,
            PerformedByUserId = request.ReviewerUserId,
            OldValues = $"Status={LessonCommentStatus.Pending}",
            NewValues = $"Status={LessonCommentStatus.Rejected};ReviewedAt={comment.ReviewedAt:O}",
        });

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<ModerateLessonCommentResponse>.Ok(
            new ModerateLessonCommentResponse(comment.Id, comment.Status.ToString(), comment.ReviewedAt, comment.ReviewedByUserId)
        );
    }
}
