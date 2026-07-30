using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Gamification.Queries;

public record GetGamificationStatusQuery(Guid StudentId) : IRequest<ApiResponse<GamificationStatusDto>>;

public record GamificationStatusDto(int TotalPoints, string CurrentLevel, List<string> EarnedBadges);

public class GetGamificationStatusQueryHandler : IRequestHandler<GetGamificationStatusQuery, ApiResponse<GamificationStatusDto>>
{
    private readonly IAppDbContext _dbContext;

    public GetGamificationStatusQueryHandler(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<GamificationStatusDto>> Handle(GetGamificationStatusQuery request, CancellationToken cancellationToken)
    {
        var gamification = await _dbContext.StudentGamifications
            .FirstOrDefaultAsync(g => g.StudentId == request.StudentId, cancellationToken);

        if (gamification == null)
        {
            // First time fetching, returns 0s
            return ApiResponse<GamificationStatusDto>.Ok(new GamificationStatusDto(0, "Novice", new List<string>()));
        }

        var badges = await _dbContext.StudentBadges
            .Where(b => b.StudentId == request.StudentId)
            .Select(b => b.BadgeName)
            .ToListAsync(cancellationToken);

        var dto = new GamificationStatusDto(
            gamification.TotalPoints,
            gamification.LevelName,
            badges
        );

        return ApiResponse<GamificationStatusDto>.Ok(dto);
    }
}
