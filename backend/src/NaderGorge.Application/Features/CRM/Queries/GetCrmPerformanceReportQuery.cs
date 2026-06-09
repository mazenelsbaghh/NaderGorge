using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.CRM.Queries;

public record GetCrmPerformanceReportQuery(Guid RequesterUserId) : IRequest<ApiResponse<CrmPerformanceReportDto>>;

public record CrmPerformanceReportDto(
    int TotalCalls,
    Dictionary<string, int> OutcomeBreakdown,
    List<AgentPerformanceDto> AgentPerformance);

public record AgentPerformanceDto(
    Guid AgentId,
    string AgentName,
    int CallsMade,
    int CompletedCalls,
    int NoAnswerCalls);

public class GetCrmPerformanceReportQueryHandler : IRequestHandler<GetCrmPerformanceReportQuery, ApiResponse<CrmPerformanceReportDto>>
{
    private readonly IAppDbContext _db;

    public GetCrmPerformanceReportQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<CrmPerformanceReportDto>> Handle(GetCrmPerformanceReportQuery request, CancellationToken ct)
    {
        // 1. Verify manager role (Admin or Supervisor)
        var requesterRoles = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == request.RequesterUserId)
            .Select(ur => ur.Role.Type)
            .ToListAsync(ct);

        var isManager = requesterRoles.Any(r => r == RoleType.Admin || r == RoleType.Supervisor);
        if (!isManager)
        {
            return ApiResponse<CrmPerformanceReportDto>.Fail("Access denied. Only managers can view performance reports.");
        }

        // 2. Fetch total calls
        var totalCalls = await _db.CrmCallLogs.CountAsync(ct);

        // 3. Fetch outcome breakdown
        var outcomeList = await _db.CrmCallLogs
            .GroupBy(l => l.Outcome)
            .Select(g => new { Outcome = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var outcomeBreakdown = new Dictionary<string, int>();
        foreach (var item in outcomeList)
        {
            outcomeBreakdown[item.Outcome.ToString()] = item.Count;
        }

        // Ensure all outcomes exist in the dictionary with at least 0
        foreach (CallOutcome outcome in Enum.GetValues(typeof(CallOutcome)))
        {
            if (!outcomeBreakdown.ContainsKey(outcome.ToString()))
            {
                outcomeBreakdown[outcome.ToString()] = 0;
            }
        }

        // 4. Fetch agent logs breakdown
        var agentStats = await _db.CrmCallLogs
            .Include(l => l.Agent)
            .GroupBy(l => new { l.AgentId, l.Agent.FullName })
            .Select(g => new AgentPerformanceDto(
                g.Key.AgentId,
                g.Key.FullName,
                g.Count(),
                g.Count(l => l.Outcome == CallOutcome.Completed),
                g.Count(l => l.Outcome == CallOutcome.NoAnswer)
            ))
            .ToListAsync(ct);

        var report = new CrmPerformanceReportDto(totalCalls, outcomeBreakdown, agentStats);
        return ApiResponse<CrmPerformanceReportDto>.Ok(report);
    }
}
