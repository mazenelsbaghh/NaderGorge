using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Application.Features.Operations.Commands;

public record AdminResolveApprovalCommand(
    Guid TaskId,
    Guid UserId,
    bool Approve,
    string? RejectionReason = null
) : IRequest<ApiResponse<bool>>;

public class AdminResolveApprovalCommandValidator : AbstractValidator<AdminResolveApprovalCommand>
{
    public AdminResolveApprovalCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RejectionReason).MaximumLength(1000);
    }
}

public class AdminResolveApprovalCommandHandler : IRequestHandler<AdminResolveApprovalCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;

    public AdminResolveApprovalCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<bool>> Handle(AdminResolveApprovalCommand request, CancellationToken ct)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == request.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException("Task not found.");
        }

        if (task.Status != TaskStatus.Review)
        {
            throw new InvalidOperationException("Only tasks in Review status can be approved or rejected.");
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        var isManager = user.UserRoles.Any(ur => ur.Role.Type == RoleType.Admin || ur.Role.Type == RoleType.Supervisor);
        if (!isManager)
        {
            throw new UnauthorizedAccessException("Only managers (Admin or Supervisor) can approve or reject tasks.");
        }

        if (request.Approve)
        {
            task.Status = TaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.ApprovedById = request.UserId;

            if (task.MediaPipelineId.HasValue)
            {
                var pipeline = await _db.MediaProductionPipelines.FirstOrDefaultAsync(mp => mp.Id == task.MediaPipelineId.Value, ct);
                if (pipeline != null)
                {
                    pipeline.Stage = MediaStage.Approved;
                }
            }
        }
        else
        {
            task.Status = TaskStatus.InProgress;
            task.CompletedAt = null;
            task.ApprovedById = null;

            if (task.MediaPipelineId.HasValue)
            {
                var pipeline = await _db.MediaProductionPipelines.FirstOrDefaultAsync(mp => mp.Id == task.MediaPipelineId.Value, ct);
                if (pipeline != null)
                {
                    pipeline.Stage = MediaStage.Editing;
                }
            }

            // Log a rejection comment
            var rejectionCommentText = $"Task completion rejected by {user.FullName}.";
            if (!string.IsNullOrWhiteSpace(request.RejectionReason))
            {
                rejectionCommentText += $" Reason: {request.RejectionReason}";
            }

            var rejectionComment = new TaskComment
            {
                TaskId = task.Id,
                UserId = request.UserId,
                Content = rejectionCommentText
            };
            _db.TaskComments.Add(rejectionComment);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Action = request.Approve ? "ApproveTask" : "RejectTask",
            EntityType = nameof(TaskItem),
            EntityId = task.Id,
            PerformedByUserId = request.UserId,
            OldValues = $"Status: {TaskStatus.Review}",
            NewValues = request.Approve 
                ? $"Status: {task.Status}, CompletedAt: {task.CompletedAt}, ApprovedById: {task.ApprovedById}"
                : $"Status: {task.Status}, RejectionReason: {request.RejectionReason}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, request.Approve ? "Task completion approved." : "Task completion rejected.");
    }
}
