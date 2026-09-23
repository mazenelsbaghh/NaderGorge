using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Welcome;

public sealed record StudentWelcomeStatsDto(int TotalStudents, int Completed, int Pending, int CompletedToday, decimal CompletionPercent);
public sealed record GetStudentWelcomeStatsQuery : IRequest<ApiResponse<StudentWelcomeStatsDto>>;

public sealed class GetStudentWelcomeStatsQueryHandler(IAppDbContext db, TimeProvider? clock = null)
    : IRequestHandler<GetStudentWelcomeStatsQuery, ApiResponse<StudentWelcomeStatsDto>>
{
    public async Task<ApiResponse<StudentWelcomeStatsDto>> Handle(GetStudentWelcomeStatsQuery request, CancellationToken ct)
    {
        var today = CairoTime.ToLocal((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime).Date;
        var (start, end) = CairoTime.GetDayRangeUtc(today);
        var counts = await db.StudentProfiles.AsNoTracking().Where(p => !p.User.IsDeleted)
            .GroupBy(p => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Completed = group.Count(p => p.FirstWelcomeCompletedAt != null),
                Today = group.Count(p => p.FirstWelcomeCompletedAt >= start && p.FirstWelcomeCompletedAt < end)
            }).SingleOrDefaultAsync(ct);
        var total = counts?.Total ?? 0;
        var completed = counts?.Completed ?? 0;
        return ApiResponse<StudentWelcomeStatsDto>.Ok(new(total, completed, total - completed,
            counts?.Today ?? 0, total == 0 ? 0 : Math.Round(completed * 100m / total, 1)));
    }
}
