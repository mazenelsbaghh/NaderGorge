using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Community.Queries;

public record MyCommunityPostDto(
    Guid Id,
    string Body,
    string Status,
    DateTime CreatedAt,
    bool IsPoll
);

public record GetMyCommunityPostsQuery(Guid UserId) : IRequest<ApiResponse<List<MyCommunityPostDto>>>;

public class GetMyCommunityPostsQueryHandler : IRequestHandler<GetMyCommunityPostsQuery, ApiResponse<List<MyCommunityPostDto>>>
{
    private readonly IAppDbContext _db;

    public GetMyCommunityPostsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<MyCommunityPostDto>>> Handle(GetMyCommunityPostsQuery request, CancellationToken ct)
    {
        var posts = await _db.CommunityPosts
            .AsNoTracking()
            .Where(p => p.AuthorUserId == request.UserId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new MyCommunityPostDto(
                p.Id,
                p.Body,
                p.Status.ToString(),
                p.CreatedAt,
                p.IsPoll
            ))
            .ToListAsync(ct);

        return ApiResponse<List<MyCommunityPostDto>>.Ok(posts);
    }
}
