using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Application.Features.Operations.Commands;

public record UpdateTaskStatusCommand(
    Guid TaskId,
    TaskStatus Status,
    Guid UserId
) : IRequest<ApiResponse<bool>>;

public class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;

    public UpdateTaskStatusCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<bool>> Handle(UpdateTaskStatusCommand request, CancellationToken ct)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == request.TaskId, ct);
        if (task == null)
        {
            throw new KeyNotFoundException("Task not found.");
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

        // Enforce restrictions for non-managers
        if (!isManager)
        {
            if (task.AssigneeId != request.UserId)
            {
                throw new UnauthorizedAccessException("You are not authorized to update this task.");
            }

            // 1. Cannot transition out of Completed or Review
            if (task.Status == TaskStatus.Completed || task.Status == TaskStatus.Review)
            {
                throw new UnauthorizedAccessException("Only managers can change the status of tasks in Review or Completed status.");
            }

            // 2. Cannot transition TO Completed directly (must go through Review for approval)
            if (request.Status == TaskStatus.Completed)
            {
                throw new UnauthorizedAccessException("Task completion requires manager approval. Please transition to Review.");
            }
        }

        var oldStatus = task.Status;
        task.Status = request.Status;
        if (request.Status == TaskStatus.Completed)
        {
            task.CompletedAt = DateTime.UtcNow;
            if (isManager && task.ApprovedById == null)
            {
                task.ApprovedById = request.UserId;
            }
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "UpdateTaskStatus",
            EntityType = nameof(TaskItem),
            EntityId = task.Id,
            PerformedByUserId = request.UserId,
            OldValues = $"Status: {oldStatus}",
            NewValues = $"Status: {task.Status}, CompletedAt: {task.CompletedAt}, ApprovedById: {task.ApprovedById}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, $"Task status updated to {request.Status}.");
    }
}
