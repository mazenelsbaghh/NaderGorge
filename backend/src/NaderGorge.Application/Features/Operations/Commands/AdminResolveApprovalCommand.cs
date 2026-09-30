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

public record AdminResolveApprovalCommand(
    Guid TaskId,
    Guid UserId,
    bool Approve,
    string? RejectionReason = null
) : IRequest<ApiResponse<bool>>
{
    public string? OperationId { get; init; }
}

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
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<bool>.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await ResolveOnceAsync(request, null, null, ct);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.TaskId, request.UserId, request.Approve, request.RejectionReason }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
            return prior.Scope == "operations.task.approval.resolve" && prior.ActorUserId == request.UserId
                && prior.RequestHash == requestHash && prior.ResultEntityId == request.TaskId
                ? ApiResponse<bool>.Ok(true, request.Approve ? "Task completion approved." : "Task completion rejected.")
                : ApiResponse<bool>.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);

        var resolved = await ResolveOnceAsync(request, operationId, requestHash, ct);
        await transaction.CommitAsync(ct);
        return resolved;
    }

    private async Task<ApiResponse<bool>> ResolveOnceAsync(AdminResolveApprovalCommand request,
        string? operationId, string? requestHash, CancellationToken ct)
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
            throw new ForbiddenException("Only managers (Admin or Supervisor) can approve or reject tasks.");
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

        if (operationId is not null)
            _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
            {
                OperationId = operationId, Scope = "operations.task.approval.resolve",
                ActorUserId = request.UserId, RequestHash = requestHash!, ResultEntityId = request.TaskId,
                SafeResultJson = JsonSerializer.Serialize(new { resolved = true })
            });

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, request.Approve ? "Task completion approved." : "Task completion rejected.");
    }
}
