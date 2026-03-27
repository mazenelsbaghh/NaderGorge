using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Gamification.Commands;

public class AcademicTaskCompletedEventHandler : INotificationHandler<AcademicTaskCompletedEvent>
{
    private readonly IAppDbContext _dbContext;

    public AcademicTaskCompletedEventHandler(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(AcademicTaskCompletedEvent notification, CancellationToken cancellationToken)
    {
        var gamification = await _dbContext.StudentGamifications
            .FirstOrDefaultAsync(g => g.StudentId == notification.StudentId, cancellationToken);

        if (gamification == null)
        {
            gamification = new StudentGamification
            {
                StudentId = notification.StudentId,
                TotalPoints = 0,
                LevelName = "Novice",
                CurrentStreakCount = 1,
                LongestStreakCount = 1
            };
            _dbContext.StudentGamifications.Add(gamification);
        }

        // Add points
        gamification.TotalPoints += notification.BasePoints;

        // Logging the action
        var actionLog = new GamificationActionLog
        {
            StudentId = notification.StudentId,
            EventType = notification.TaskType,
            PointsAwarded = notification.BasePoints
        };
        _dbContext.GamificationActionLogs.Add(actionLog);

        // Simple badge logic MVP
        if (gamification.TotalPoints > 500)
        {
            var hasScholarBadge = await _dbContext.StudentBadges
                .AnyAsync(b => b.StudentId == notification.StudentId && b.BadgeName == "Scholar", cancellationToken);

            if (!hasScholarBadge)
            {
                _dbContext.StudentBadges.Add(new StudentBadge
                {
                    StudentId = notification.StudentId,
                    BadgeName = "Scholar"
                });
            }
        }
        
        // Level up
        if (gamification.TotalPoints > 2000 && gamification.LevelName != "Master")
            gamification.LevelName = "Master";
        else if (gamification.TotalPoints > 1000 && gamification.LevelName != "Expert")
            gamification.LevelName = "Expert";
        else if (gamification.TotalPoints > 500 && gamification.LevelName != "Apprentice")
            gamification.LevelName = "Apprentice";

        gamification.LastTaskCompletedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
