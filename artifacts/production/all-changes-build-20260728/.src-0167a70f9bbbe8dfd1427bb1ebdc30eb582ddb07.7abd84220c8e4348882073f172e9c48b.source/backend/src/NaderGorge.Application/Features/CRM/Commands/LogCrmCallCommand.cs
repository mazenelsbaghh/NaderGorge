using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.CRM.Commands;

public record LogCrmCallCommand(
    Guid StudentId,
    Guid AgentId,
    CallOutcome Outcome,
    string? Notes,
    DateTime? NextFollowUpDate) : IRequest<ApiResponse>;

public class LogCrmCallCommandHandler : IRequestHandler<LogCrmCallCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public LogCrmCallCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(LogCrmCallCommand request, CancellationToken ct)
    {
        // 1. Add Call Log Entry
        var callLog = new CrmCallLog
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            AgentId = request.AgentId,
            CallDate = DateTime.UtcNow,
            Outcome = request.Outcome,
            Notes = request.Notes,
            NextFollowUpDate = request.NextFollowUpDate
        };
        _db.CrmCallLogs.Add(callLog);

        // 2. Find or create CrmStudentStatus
        var crmStatus = await _db.CrmStudentStatuses
            .FirstOrDefaultAsync(s => s.StudentId == request.StudentId, ct);

        var nextStatus = request.Outcome switch
        {
            CallOutcome.Closed => CrmStatus.Closed,
            _ => CrmStatus.InProgress
        };

        if (crmStatus == null)
        {
            crmStatus = new CrmStudentStatus
            {
                StudentId = request.StudentId,
                Status = nextStatus,
                AssignedAgentId = request.AgentId,
                LastCalledAt = DateTime.UtcNow,
                NextFollowUpDate = request.NextFollowUpDate
            };
            _db.CrmStudentStatuses.Add(crmStatus);
        }
        else
        {
            crmStatus.Status = nextStatus;
            crmStatus.LastCalledAt = DateTime.UtcNow;
            crmStatus.NextFollowUpDate = request.NextFollowUpDate;
            
            // If the student status was unassigned/assigned, associate it to this agent
            if (crmStatus.AssignedAgentId == null)
            {
                crmStatus.AssignedAgentId = request.AgentId;
            }
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "LogCrmCall",
            EntityType = nameof(CrmCallLog),
            EntityId = callLog.Id,
            PerformedByUserId = request.AgentId,
            NewValues = $"StudentId: {request.StudentId}, AgentId: {request.AgentId}, Outcome: {request.Outcome}, NextFollowUpDate: {request.NextFollowUpDate}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
