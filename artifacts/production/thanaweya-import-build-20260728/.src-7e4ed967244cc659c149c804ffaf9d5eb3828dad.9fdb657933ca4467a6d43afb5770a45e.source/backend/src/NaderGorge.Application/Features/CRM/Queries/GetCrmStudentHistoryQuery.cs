using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.CRM.Queries;

public record GetCrmStudentHistoryQuery(Guid StudentId, Guid RequesterUserId) : IRequest<ApiResponse<List<CrmCallLogDto>>>;

public record CrmCallLogDto(
    Guid Id,
    Guid StudentId,
    string AgentName,
    DateTime CallDate,
    string Outcome,
    string? Notes,
    DateTime? NextFollowUpDate);

public class GetCrmStudentHistoryQueryHandler : IRequestHandler<GetCrmStudentHistoryQuery, ApiResponse<List<CrmCallLogDto>>>
{
    private readonly IAppDbContext _db;

    public GetCrmStudentHistoryQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<CrmCallLogDto>>> Handle(GetCrmStudentHistoryQuery request, CancellationToken ct)
    {
        // 1. Verify that the requester is authorized
        var requesterRoles = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == request.RequesterUserId)
            .Select(ur => ur.Role.Type)
            .ToListAsync(ct);

        var isManager = requesterRoles.Any(r => r == RoleType.Admin || r == RoleType.Supervisor);

        if (!isManager)
        {
            var assignedAgent = await _db.CrmStudentStatuses
                .Where(s => s.StudentId == request.StudentId)
                .Select(s => s.AssignedAgentId)
                .FirstOrDefaultAsync(ct);

            if (assignedAgent != request.RequesterUserId)
            {
                return ApiResponse<List<CrmCallLogDto>>.Fail("You are not authorized to view this student's call log history.");
            }
        }

        // 2. Query logs
        var logs = await _db.CrmCallLogs
            .Include(l => l.Agent)
            .Where(l => l.StudentId == request.StudentId)
            .OrderByDescending(l => l.CallDate)
            .Select(l => new CrmCallLogDto(
                l.Id,
                l.StudentId,
                l.Agent.FullName,
                l.CallDate,
                l.Outcome.ToString(),
                l.Notes,
                l.NextFollowUpDate
            ))
            .ToListAsync(ct);

        return ApiResponse<List<CrmCallLogDto>>.Ok(logs);
    }
}
