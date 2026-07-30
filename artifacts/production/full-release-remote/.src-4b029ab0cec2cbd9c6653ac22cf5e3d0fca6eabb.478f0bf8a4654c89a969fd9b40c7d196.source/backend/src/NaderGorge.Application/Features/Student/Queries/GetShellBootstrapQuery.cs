using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities.Notifications;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetShellBootstrapQuery(Guid UserId) : IRequest<ApiResponse<ShellBootstrapDto>>;

public record ShellBootstrapDto(
    int UnreadNotificationsCount,
    decimal CurrentBalance,
    StudentGamificationDto Gamification,
    StudentThemePreferencesDto ThemePreferences,
    string? AvatarSlug,
    string? ParentTrackingCode,
    bool HasSeenTrackingCodePopup
);

public record StudentGamificationDto(
    int TotalPoints,
    int CurrentStreakCount,
    int LongestStreakCount,
    string LevelName
);

public class GetShellBootstrapQueryHandler : IRequestHandler<GetShellBootstrapQuery, ApiResponse<ShellBootstrapDto>>
{
    private readonly IAppDbContext _db;

    public GetShellBootstrapQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ShellBootstrapDto>> Handle(GetShellBootstrapQuery request, CancellationToken ct)
    {
        var userId = request.UserId;

        // 1. Unread notification count
        var unreadCount = await _db.NotificationEvents
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.ChannelType == NotificationChannelType.InApp && n.ReadAt == null, ct);

        // 2. Balance
        var balance = await _db.StudentBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId, ct);
        decimal currentBalance = balance?.CurrentBalance ?? 0m;

        // 3. Gamification
        var gamification = await _db.StudentGamifications
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.StudentId == userId, ct);

        var gamificationDto = new StudentGamificationDto(
            gamification?.TotalPoints ?? 0,
            gamification?.CurrentStreakCount ?? 0,
            gamification?.LongestStreakCount ?? 0,
            gamification?.LevelName ?? "Novice"
        );

        // 4. Student profile & Theme
        var profile = await _db.StudentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var themePreferencesDto = StudentThemeCatalog.BuildPreferences(
            profile?.LightThemePaletteId,
            profile?.DarkThemePaletteId,
            profile?.CurrentMode
        );

        var dto = new ShellBootstrapDto(
            unreadCount,
            currentBalance,
            gamificationDto,
            themePreferencesDto,
            profile?.AvatarSlug,
            profile?.ParentTrackingCode,
            profile?.HasSeenTrackingCodePopup ?? false
        );

        return ApiResponse<ShellBootstrapDto>.Ok(dto);
    }
}
