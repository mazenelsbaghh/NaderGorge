using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Community.Commands;

public record CreateCommunityPostCommentResponse(
    Guid Id,
    Guid PostId,
    Guid? ParentCommentId,
    DateTime CreatedAt,
    string Status,
    string Message
);

public record CreateCommunityPostCommentCommand(Guid PostId, Guid UserId, string Body, Guid? ParentCommentId = null)
    : IRequest<ApiResponse<CreateCommunityPostCommentResponse>>;

public class CreateCommunityPostCommentCommandHandler : IRequestHandler<CreateCommunityPostCommentCommand, ApiResponse<CreateCommunityPostCommentResponse>>
{
    private const int MaxCommentLength = 2000;

    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService? _academicScope;

    public CreateCommunityPostCommentCommandHandler(IAppDbContext db, IAcademicScopeService? academicScope = null)
    {
        _db = db;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<CreateCommunityPostCommentResponse>> Handle(CreateCommunityPostCommentCommand request, CancellationToken ct)
    {
        var post = await _db.CommunityPosts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, ct);

        if (post == null || post.Status != CommunityPostStatus.Approved)
            return ApiResponse<CreateCommunityPostCommentResponse>.Fail("Post not found", new List<string> { "NOT_FOUND" });

        if (_academicScope != null && !await _academicScope.IsOwnerEligibleForStudentAsync(
                StudentFacingScopeOwnerType.CommunityPost,
                request.PostId,
                request.UserId,
                ct))
        {
            return ApiResponse<CreateCommunityPostCommentResponse>.Fail(
                "This post is not available for your academic scope.",
                new List<string> { "ACADEMIC_SCOPE_DENIED" });
        }

        var trimmedBody = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBody))
            return ApiResponse<CreateCommunityPostCommentResponse>.Fail("Comment body is required.", new List<string> { "VALIDATION_EMPTY_BODY" });

        if (trimmedBody.Length > MaxCommentLength)
            return ApiResponse<CreateCommunityPostCommentResponse>.Fail($"Comment body must be {MaxCommentLength} characters or fewer.", new List<string> { "VALIDATION_BODY_TOO_LONG" });

        if (request.ParentCommentId.HasValue)
        {
            var parentExists = await _db.CommunityPostComments
                .AnyAsync(c => c.Id == request.ParentCommentId.Value && c.PostId == request.PostId, ct);
            if (!parentExists)
                return ApiResponse<CreateCommunityPostCommentResponse>.Fail("Parent comment not found.", new List<string> { "PARENT_COMMENT_NOT_FOUND" });
        }

        var userRoles = await _db.UserRoles
            .Where(ur => ur.UserId == request.UserId)
            .Select(ur => ur.Role.Type)
            .ToListAsync(ct);

        var isTeacherOrAdmin = userRoles.Any(r => r == RoleType.Teacher || r == RoleType.Admin);

        var comment = new CommunityPostComment
        {
            PostId = request.PostId,
            ParentCommentId = request.ParentCommentId,
            AuthorUserId = request.UserId,
            Body = trimmedBody,
            Status = isTeacherOrAdmin ? CommunityCommentStatus.Approved : CommunityCommentStatus.Pending,
        };

        _db.CommunityPostComments.Add(comment);

        var createdEvent = new OutboxEvent
        {
            Type = "CommunityCommentCreated",
            TargetUserId = request.UserId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                commentId = comment.Id,
                postId = comment.PostId,
                authorId = comment.AuthorUserId,
                status = comment.Status.ToString()
            })
        };
        _db.OutboxEvents.Add(createdEvent);

        if (isTeacherOrAdmin)
        {
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
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<CreateCommunityPostCommentResponse>.Ok(
            new CreateCommunityPostCommentResponse(
                comment.Id,
                comment.PostId,
                comment.ParentCommentId,
                comment.CreatedAt,
                comment.Status.ToString(),
                isTeacherOrAdmin ? "تم إضافة التعليق بنجاح." : "تم استلام تعليقك وسيظهر بعد المراجعة.")
        );
    }
}
