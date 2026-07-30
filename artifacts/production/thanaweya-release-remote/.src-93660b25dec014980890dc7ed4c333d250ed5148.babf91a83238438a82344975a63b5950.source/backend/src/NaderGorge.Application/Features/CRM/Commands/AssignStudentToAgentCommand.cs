using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.CRM.Commands;

public record AssignStudentToAgentCommand(
    Guid StudentId,
    Guid? AssignedAgentId,
    CrmPriority Priority,
    string? Notes,
    Guid PerformedByUserId = default) : IRequest<ApiResponse>;

public class AssignStudentToAgentCommandHandler : IRequestHandler<AssignStudentToAgentCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public AssignStudentToAgentCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(AssignStudentToAgentCommand request, CancellationToken ct)
    {
        // 1. Verify Student exists and actually has the Student role
        var studentUser = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.StudentId, ct);

        if (studentUser == null)
        {
            return ApiResponse.Fail("Student user not found.");
        }

        var isStudent = studentUser.UserRoles.Any(ur => ur.Role.Type == RoleType.Student);
        if (!isStudent)
        {
            return ApiResponse.Fail("The selected user is not a student.");
        }

        // 2. If AssignedAgentId is provided, verify they are NOT a student (must be staff/admin/teacher)
        if (request.AssignedAgentId.HasValue && request.AssignedAgentId.Value != Guid.Empty)
        {
            var agentUser = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == request.AssignedAgentId.Value, ct);

            if (agentUser == null)
            {
                return ApiResponse.Fail("Assigned agent user not found.");
            }

            var isAgentStudent = agentUser.UserRoles.Any(ur => ur.Role.Type == RoleType.Student);
            if (isAgentStudent)
            {
                return ApiResponse.Fail("Cannot assign a student user as an agent.");
            }
        }

        // 3. Find or create CrmStudentStatus
        var crmStatus = await _db.CrmStudentStatuses
            .FirstOrDefaultAsync(s => s.StudentId == request.StudentId, ct);

        var oldAgentId = crmStatus?.AssignedAgentId;
        var oldPriority = crmStatus?.Priority;

        if (crmStatus == null)
        {
            crmStatus = new CrmStudentStatus
            {
                StudentId = request.StudentId,
                Status = request.AssignedAgentId.HasValue ? CrmStatus.Assigned : CrmStatus.Unassigned,
                AssignedAgentId = request.AssignedAgentId,
                Priority = request.Priority,
                Notes = request.Notes
            };
            _db.CrmStudentStatuses.Add(crmStatus);
        }
        else
        {
            crmStatus.AssignedAgentId = request.AssignedAgentId;
            crmStatus.Priority = request.Priority;
            crmStatus.Notes = request.Notes;

            // Update status state appropriately
            if (crmStatus.Status == CrmStatus.Unassigned && request.AssignedAgentId.HasValue)
            {
                crmStatus.Status = CrmStatus.Assigned;
            }
            else if (!request.AssignedAgentId.HasValue)
            {
                crmStatus.Status = CrmStatus.Unassigned;
            }
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "AssignStudentToAgent",
            EntityType = nameof(CrmStudentStatus),
            EntityId = crmStatus.StudentId,
            PerformedByUserId = request.PerformedByUserId != Guid.Empty ? request.PerformedByUserId : null,
            OldValues = $"AssignedAgentId: {oldAgentId}, Priority: {oldPriority}",
            NewValues = $"AssignedAgentId: {crmStatus.AssignedAgentId}, Priority: {crmStatus.Priority}, Status: {crmStatus.Status}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
