using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.CRM.Queries;

public record GetCrmStudentsQuery(
    Guid RequesterUserId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    CrmStatus? Status = null,
    Guid? AgentId = null,
    CrmPriority? Priority = null,
    bool OnlyOverdue = false) : IRequest<ApiResponse<CrmStudentListResponse>>;

public record CrmStudentDto(
    Guid StudentId,
    string StudentName,
    string StudentPhone,
    string CrmStatus,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    string Priority,
    DateTime? NextFollowUpDate,
    DateTime? LastCalledAt,
    string? Notes);

public record CrmStudentListResponse(
    List<CrmStudentDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetCrmStudentsQueryHandler : IRequestHandler<GetCrmStudentsQuery, ApiResponse<CrmStudentListResponse>>
{
    private readonly IAppDbContext _db;

    public GetCrmStudentsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<CrmStudentListResponse>> Handle(GetCrmStudentsQuery request, CancellationToken ct)
    {
        // 1. Determine if Requester is Admin or Supervisor
        var requesterRoles = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == request.RequesterUserId)
            .Select(ur => ur.Role.Type)
            .ToListAsync(ct);

        var isManager = requesterRoles.Any(r => r == RoleType.Admin || r == RoleType.Supervisor);

        // 2. Build Query
        // We want to list all users who are Students. We can join on StudentProfiles.
        var query = _db.Users
            .Include(u => u.UserRoles)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Type == RoleType.Student));

        // Join/Include CrmStudentStatus
        // We'll project the results, but let's apply filtering first.
        // We need to access CrmStudentStatuses, so we filter based on it.
        var statusQuery = _db.CrmStudentStatuses
            .Include(s => s.Student)
            .Include(s => s.AssignedAgent)
            .AsQueryable();

        // Apply Search (Name or Phone on Student User)
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            statusQuery = statusQuery.Where(s => s.Student.FullName.ToLower().Contains(searchLower) || s.Student.PhoneNumber.Contains(request.Search));
        }

        // Apply Status Filter
        if (request.Status.HasValue)
        {
            statusQuery = statusQuery.Where(s => s.Status == request.Status.Value);
        }

        // Apply Priority Filter
        if (request.Priority.HasValue)
        {
            statusQuery = statusQuery.Where(s => s.Priority == request.Priority.Value);
        }

        // Apply Agent Row-level Filtering
        if (!isManager)
        {
            // Non-managers can ONLY see records assigned to them
            statusQuery = statusQuery.Where(s => s.AssignedAgentId == request.RequesterUserId);
        }
        else if (request.AgentId.HasValue)
        {
            // Managers can filter by any AgentId
            statusQuery = statusQuery.Where(s => s.AssignedAgentId == request.AgentId.Value);
        }

        // Apply Overdue Filter (NextFollowUpDate in the past)
        if (request.OnlyOverdue)
        {
            var now = DateTime.UtcNow;
            statusQuery = statusQuery.Where(s => s.NextFollowUpDate.HasValue && s.NextFollowUpDate.Value < now && s.Status != CrmStatus.Closed);
        }

        // We also want to support listing students who have NO CrmStudentStatus record yet (Unassigned status).
        // But only if we are not filtering by Agent, Priority, or Overdue (since those imply a status record exists),
        // and only if the requester is a manager (since unassigned students aren't assigned to assistants).
        // Wait, if a manager views the list and searches or filters by Unassigned, they should see students who don't have a CrmStudentStatus row.
        // Let's handle this cleanly by checking if the query is for Unassigned or if no CrmStatus filter is set.
        // To do this, we can project from the Student User table directly, left-joining CrmStudentStatuses!
        // This is much more robust because it ensures all student accounts show up in the CRM directory!
        
        var mainQuery = _db.Users
            .Include(u => u.UserRoles)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Type == RoleType.Student))
            .GroupJoin(
                _db.CrmStudentStatuses.Include(s => s.AssignedAgent),
                u => u.Id,
                s => s.StudentId,
                (u, statuses) => new { Student = u, Statuses = statuses }
            )
            .SelectMany(
                x => x.Statuses.DefaultIfEmpty(),
                (x, status) => new { x.Student, Status = status }
            );

        // Apply Search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            mainQuery = mainQuery.Where(x => x.Student.FullName.ToLower().Contains(searchLower) || x.Student.PhoneNumber.Contains(request.Search));
        }

        // Apply Status Filter
        if (request.Status.HasValue)
        {
            if (request.Status.Value == CrmStatus.Unassigned)
            {
                mainQuery = mainQuery.Where(x => x.Status == null || x.Status.Status == CrmStatus.Unassigned);
            }
            else
            {
                mainQuery = mainQuery.Where(x => x.Status != null && x.Status.Status == request.Status.Value);
            }
        }

        // Apply Priority Filter
        if (request.Priority.HasValue)
        {
            mainQuery = mainQuery.Where(x => x.Status != null && x.Status.Priority == request.Priority.Value);
        }

        // Apply Agent Row-level Security / Filter
        if (!isManager)
        {
            mainQuery = mainQuery.Where(x => x.Status != null && x.Status.AssignedAgentId == request.RequesterUserId);
        }
        else if (request.AgentId.HasValue)
        {
            mainQuery = mainQuery.Where(x => x.Status != null && x.Status.AssignedAgentId == request.AgentId.Value);
        }

        // Apply Overdue Filter
        if (request.OnlyOverdue)
        {
            var now = DateTime.UtcNow;
            mainQuery = mainQuery.Where(x => x.Status != null && x.Status.NextFollowUpDate.HasValue && x.Status.NextFollowUpDate.Value < now && x.Status.Status != CrmStatus.Closed);
        }

        // Calculate count and pagination
        var totalCount = await mainQuery.CountAsync(ct);

        var items = await mainQuery
            .OrderBy(x => x.Status != null ? x.Status.Priority : CrmPriority.Medium)
            .ThenBy(x => x.Student.FullName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new CrmStudentDto(
                x.Student.Id,
                x.Student.FullName,
                x.Student.PhoneNumber,
                x.Status != null ? x.Status.Status.ToString() : CrmStatus.Unassigned.ToString(),
                x.Status != null ? x.Status.AssignedAgentId : null,
                x.Status != null && x.Status.AssignedAgent != null ? x.Status.AssignedAgent.FullName : null,
                x.Status != null ? x.Status.Priority.ToString() : CrmPriority.Medium.ToString(),
                x.Status != null ? x.Status.NextFollowUpDate : null,
                x.Status != null ? x.Status.LastCalledAt : null,
                x.Status != null ? x.Status.Notes : null
            ))
            .ToListAsync(ct);

        var response = new CrmStudentListResponse(items, totalCount, request.Page, request.PageSize);
        return ApiResponse<CrmStudentListResponse>.Ok(response);
    }
}
