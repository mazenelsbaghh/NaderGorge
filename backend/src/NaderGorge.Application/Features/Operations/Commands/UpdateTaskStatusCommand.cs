using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
) : IRequest<ApiResponse<bool>>
{
    public string? OperationId { get; init; }
}

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
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<bool>.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await UpdateOnceAsync(request, null, null, ct);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.TaskId, request.Status, request.UserId }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
            return prior.Scope == "operations.task.status.update" && prior.ActorUserId == request.UserId
                && prior.RequestHash == requestHash && prior.ResultEntityId == request.TaskId
                ? ApiResponse<bool>.Ok(true, $"Task status updated to {request.Status}.")
                : ApiResponse<bool>.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);

        var updated = await UpdateOnceAsync(request, operationId, requestHash, ct);
        await transaction.CommitAsync(ct);
        return updated;
    }

    private async Task<ApiResponse<bool>> UpdateOnceAsync(UpdateTaskStatusCommand request,
        string? operationId, string? requestHash, CancellationToken ct)
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
                throw new ForbiddenException("You are not authorized to update this task.");
            }

            // 1. Cannot transition out of Completed or Review
            if (task.Status == TaskStatus.Completed || task.Status == TaskStatus.Review)
            {
                throw new ForbiddenException("Only managers can change the status of tasks in Review or Completed status.");
            }

            // 2. Cannot transition TO Completed directly (must go through Review for approval)
            if (request.Status == TaskStatus.Completed)
            {
                throw new ForbiddenException("Task completion requires manager approval. Please transition to Review.");
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

        if (operationId is not null)
            _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
            {
                OperationId = operationId, Scope = "operations.task.status.update",
                ActorUserId = request.UserId, RequestHash = requestHash!, ResultEntityId = request.TaskId,
                SafeResultJson = JsonSerializer.Serialize(new { updated = true })
            });

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, $"Task status updated to {request.Status}.");
    }
}
