using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public class AdjustGamificationPointsCommand : IRequest<ApiResponse>
{
    public Guid UserId { get; set; }
    public decimal Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid AdminId { get; set; }

    public AdjustGamificationPointsCommand(Guid userId, decimal points, string reason, Guid adminId)
    {
        UserId = userId;
        Points = points;
        Reason = reason;
        AdminId = adminId;
    }
}

public class AdjustGamificationPointsCommandHandler : IRequestHandler<AdjustGamificationPointsCommand, ApiResponse>
{
    private readonly IAppDbContext _context;

    public AdjustGamificationPointsCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse> Handle(AdjustGamificationPointsCommand request, CancellationToken cancellationToken)
    {
        var gamification = await _context.StudentGamifications
            .FirstOrDefaultAsync(g => g.StudentId == request.UserId, cancellationToken);

        if (gamification == null) return ApiResponse.Fail("Gamification profile not found.");

        int oldPoints = gamification.TotalPoints;
        // Gamification total points is currently an int. If your dto uses decimal, convert it.
        gamification.TotalPoints += Convert.ToInt32(request.Points);

        var audit = new AuditLog
        {
            EntityType = "User",
            EntityId = request.UserId,
            Action = "ADJUST_POINTS",
            PerformedByUserId = request.AdminId,
            OldValues = JsonSerializer.Serialize(new { totalPoints = oldPoints }),
            NewValues = JsonSerializer.Serialize(new { totalPoints = gamification.TotalPoints, change = request.Points, reason = request.Reason })
        };
        _context.AuditLogs.Add(audit);

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Ok("Gamification points adjusted.");
    }
}
