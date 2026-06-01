using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record ModerationCommunityCommentDto(
    Guid Id,
    Guid PostId,
    Guid StudentId,
    string StudentName,
    string Body,
    string Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByName,
    string? RejectionReason
);

public record GetCommunityCommentsForModerationQuery()
    : IRequest<ApiResponse<List<ModerationCommunityCommentDto>>>;

public class GetCommunityCommentsForModerationQueryHandler
    : IRequestHandler<GetCommunityCommentsForModerationQuery, ApiResponse<List<ModerationCommunityCommentDto>>>
{
    private readonly IAppDbContext _db;

    public GetCommunityCommentsForModerationQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<ModerationCommunityCommentDto>>> Handle(
        GetCommunityCommentsForModerationQuery request,
        CancellationToken ct)
    {
        var comments = await _db.CommunityPostComments
            .AsNoTracking()
            .Include(c => c.AuthorUser)
            .Include(c => c.ReviewedByUser)
            .Where(c => c.Status == CommunityCommentStatus.Pending)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ModerationCommunityCommentDto(
                c.Id,
                c.PostId,
                c.AuthorUserId,
                c.AuthorUser.FullName,
                c.Body,
                c.Status.ToString(),
                c.CreatedAt,
                c.ReviewedAt,
                c.ReviewedByUser != null ? c.ReviewedByUser.FullName : null,
                c.RejectionReason
            ))
            .ToListAsync(ct);

        return ApiResponse<List<ModerationCommunityCommentDto>>.Ok(comments);
    }
}
