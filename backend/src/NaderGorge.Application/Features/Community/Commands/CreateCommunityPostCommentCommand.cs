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
    DateTime CreatedAt,
    string Status,
    string Message
);

public record CreateCommunityPostCommentCommand(Guid PostId, Guid UserId, string Body)
    : IRequest<ApiResponse<CreateCommunityPostCommentResponse>>;

public class CreateCommunityPostCommentCommandHandler : IRequestHandler<CreateCommunityPostCommentCommand, ApiResponse<CreateCommunityPostCommentResponse>>
{
    private const int MaxCommentLength = 2000;

    private readonly IAppDbContext _db;

    public CreateCommunityPostCommentCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<CreateCommunityPostCommentResponse>> Handle(CreateCommunityPostCommentCommand request, CancellationToken ct)
    {
        var post = await _db.CommunityPosts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, ct);

        if (post == null || post.Status != CommunityPostStatus.Approved)
            return ApiResponse<CreateCommunityPostCommentResponse>.Fail("Post not found", new List<string> { "NOT_FOUND" });

        var trimmedBody = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBody))
            return ApiResponse<CreateCommunityPostCommentResponse>.Fail("Comment body is required.", new List<string> { "VALIDATION_EMPTY_BODY" });

        if (trimmedBody.Length > MaxCommentLength)
            return ApiResponse<CreateCommunityPostCommentResponse>.Fail($"Comment body must be {MaxCommentLength} characters or fewer.", new List<string> { "VALIDATION_BODY_TOO_LONG" });

        var userRoles = await _db.UserRoles
            .Where(ur => ur.UserId == request.UserId)
            .Select(ur => ur.Role.Type)
            .ToListAsync(ct);

        var isTeacherOrAdmin = userRoles.Any(r => r == RoleType.Teacher || r == RoleType.Admin);

        var comment = new CommunityPostComment
        {
            PostId = request.PostId,
            AuthorUserId = request.UserId,
            Body = trimmedBody,
            Status = isTeacherOrAdmin ? CommunityCommentStatus.Approved : CommunityCommentStatus.Pending,
        };

        _db.CommunityPostComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<CreateCommunityPostCommentResponse>.Ok(
            new CreateCommunityPostCommentResponse(
                comment.Id,
                comment.PostId,
                comment.CreatedAt,
                comment.Status.ToString(),
                isTeacherOrAdmin ? "تم إضافة التعليق بنجاح." : "تم استلام تعليقك وسيظهر بعد المراجعة.")
        );
    }
}
