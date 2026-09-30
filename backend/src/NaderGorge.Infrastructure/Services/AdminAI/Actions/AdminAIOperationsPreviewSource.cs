using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

/// <summary>Read-only previews for reviewed operations actions.</summary>
public sealed class AdminAIOperationsPreviewSource(IAppDbContext db) : IAdminAIActionPreviewSource
{
    public Task<AdminAIActionPreview> PreviewAsync<TInput>(
        string capabilityKey, Guid actorId, TInput input, CancellationToken ct) where TInput : class =>
        input switch
        {
            AdminAIAddTaskCommentInput comment when capabilityKey == "admin.operations.task-comment.create" =>
                PreviewTaskCommentAsync(capabilityKey, actorId, comment, ct),
            AdminAIUpdateTaskStatusInput status when capabilityKey == "admin.operations.task.status.update" =>
                PreviewTaskStatusAsync(capabilityKey, actorId, status, ct),
            AdminAIResolveTaskApprovalInput approval when capabilityKey == "admin.operations.task.approval.resolve" =>
                PreviewTaskApprovalAsync(capabilityKey, actorId, approval, ct),
            _ => throw new NotSupportedException("Admin AI action preview capability is unavailable.")
        };

    private async Task<AdminAIActionPreview> PreviewTaskCommentAsync(
        string capabilityKey, Guid actorId, AdminAIAddTaskCommentInput comment, CancellationToken ct)
    {
        if (comment.TaskId == Guid.Empty || string.IsNullOrWhiteSpace(comment.Content)
            || comment.Content.Length > 4000 || comment.AttachmentUrl?.Length > 2048)
            throw new ArgumentException("Task comment input is invalid.", nameof(comment));

        var task = await db.TaskItems.AsNoTracking()
            .Where(item => item.Id == comment.TaskId)
            .Select(item => new { item.Id, item.Title, item.Status, item.AssigneeId, item.CreatedById })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The task is unavailable.");
        var actor = await db.Users.AsNoTracking()
            .Where(item => item.Id == actorId)
            .Select(item => new
            {
                item.Id,
                IsManager = item.UserRoles.Any(role => role.Role.Type == RoleType.Admin
                    || role.Role.Type == RoleType.Supervisor)
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The actor is unavailable.");
        if (!actor.IsManager && task.AssigneeId != actorId && task.CreatedById != actorId)
            throw new AdminAIActionPreviewUnavailableException("The actor cannot comment on this task.");

        var state = new { task.Id, task.Title, task.Status, task.AssigneeId, task.CreatedById, actor.IsManager };
        return new AdminAIActionPreview(
            "task", $"task:{task.Id:D}",
            new { task.Title, task.Status },
            new { comment.Content, comment.AttachmentUrl },
            new { commentWillBeAdded = true, affected = 1 },
            new { valid = true },
            Fingerprint(capabilityKey, state));
    }

    private async Task<AdminAIActionPreview> PreviewTaskStatusAsync(
        string capabilityKey, Guid actorId, AdminAIUpdateTaskStatusInput input, CancellationToken ct)
    {
        if (input.TaskId == Guid.Empty || !Enum.IsDefined(input.Status))
            throw new ArgumentException("Task status input is invalid.", nameof(input));
        var task = await db.TaskItems.AsNoTracking()
            .Where(item => item.Id == input.TaskId)
            .Select(item => new
            {
                item.Id, item.Title, item.Status, item.AssigneeId, item.CreatedById,
                item.CompletedAt, item.ApprovedById
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The task is unavailable.");
        var actor = await db.Users.AsNoTracking()
            .Where(item => item.Id == actorId)
            .Select(item => new
            {
                item.Id,
                IsManager = item.UserRoles.Any(role => role.Role.Type == RoleType.Admin
                    || role.Role.Type == RoleType.Supervisor)
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The actor is unavailable.");
        if (!actor.IsManager && (task.AssigneeId != actorId
            || task.Status is TaskStatus.Completed or TaskStatus.Review
            || input.Status == TaskStatus.Completed))
            throw new AdminAIActionPreviewUnavailableException("The actor cannot change this task status.");

        var state = new
        {
            task.Id, task.Title, task.Status, task.AssigneeId, task.CreatedById,
            task.CompletedAt, task.ApprovedById, actor.IsManager
        };
        return new AdminAIActionPreview(
            "task", $"task:{task.Id:D}",
            new { task.Title, task.Status, task.CompletedAt, task.ApprovedById },
            new { input.Status },
            new { taskStatusWillBeUpdated = true, affected = 1 },
            new { valid = true },
            Fingerprint(capabilityKey, state));
    }

    private static string Fingerprint(string capabilityKey, object state) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { capabilityKey, state })));

    private async Task<AdminAIActionPreview> PreviewTaskApprovalAsync(
        string capabilityKey, Guid actorId, AdminAIResolveTaskApprovalInput input, CancellationToken ct)
    {
        if (input.TaskId == Guid.Empty || input.RejectionReason?.Length > 1000)
            throw new ArgumentException("Task approval input is invalid.", nameof(input));
        var task = await db.TaskItems.AsNoTracking()
            .Where(item => item.Id == input.TaskId)
            .Select(item => new
            {
                item.Id, item.Title, item.Status, item.AssigneeId, item.CreatedById,
                item.CompletedAt, item.ApprovedById, item.MediaPipelineId
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The task is unavailable.");
        if (task.Status != TaskStatus.Review)
            throw new AdminAIActionPreviewUnavailableException("The task is no longer in review.");
        var actor = await db.Users.AsNoTracking()
            .Where(item => item.Id == actorId)
            .Select(item => new
            {
                item.Id, item.FullName,
                IsManager = item.UserRoles.Any(role => role.Role.Type == RoleType.Admin
                    || role.Role.Type == RoleType.Supervisor)
            })
            .SingleOrDefaultAsync(ct);
        if (actor is null || !actor.IsManager)
            throw new AdminAIActionPreviewUnavailableException("The actor cannot resolve task approval.");
        var pipelineStage = task.MediaPipelineId.HasValue
            ? await db.MediaProductionPipelines.AsNoTracking()
                .Where(item => item.Id == task.MediaPipelineId.Value)
                .Select(item => (MediaStage?)item.Stage)
                .SingleOrDefaultAsync(ct)
            : null;
        var state = new
        {
            task.Id, task.Title, task.Status, task.AssigneeId, task.CreatedById,
            task.CompletedAt, task.ApprovedById, task.MediaPipelineId, pipelineStage,
            actor.FullName, actor.IsManager
        };
        return new AdminAIActionPreview(
            "task", $"task:{task.Id:D}",
            new { task.Title, task.Status, pipelineStage },
            new { input.Approve, input.RejectionReason },
            new
            {
                taskStatusAfter = input.Approve ? TaskStatus.Completed : TaskStatus.InProgress,
                pipelineStageAfter = pipelineStage is null ? (MediaStage?)null
                    : input.Approve ? MediaStage.Approved : MediaStage.Editing,
                rejectionCommentWillBeAdded = !input.Approve,
                affected = 1
            },
            new { valid = true },
            Fingerprint(capabilityKey, state));
    }
}

/// <summary>Routes supported ordinary actions to their authoritative preview source.</summary>
public sealed class AdminAIOrdinaryPreviewSource(
    AdminAIIdentityContentPreviewSource identityContent,
    AdminAIOperationsPreviewSource operations) : IAdminAIActionPreviewSource
{
    public Task<AdminAIActionPreview> PreviewAsync<TInput>(
        string capabilityKey, Guid actorId, TInput input, CancellationToken ct) where TInput : class =>
        capabilityKey switch
        {
            "admin.operations.task-comment.create" or "admin.operations.task.status.update"
                or "admin.operations.task.approval.resolve" =>
                operations.PreviewAsync(capabilityKey, actorId, input, ct),
            _ => identityContent.PreviewAsync(capabilityKey, actorId, input, ct)
        };
}
