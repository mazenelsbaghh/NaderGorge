using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

/// <summary>Read-only previews for reviewed operations actions.</summary>
public sealed class AdminAIOperationsPreviewSource(IAppDbContext db) : IAdminAIActionPreviewSource
{
    public async Task<AdminAIActionPreview> PreviewAsync<TInput>(
        string capabilityKey, Guid actorId, TInput input, CancellationToken ct) where TInput : class
    {
        if (capabilityKey != "admin.operations.task-comment.create" || input is not AdminAIAddTaskCommentInput comment)
            throw new NotSupportedException("Admin AI action preview capability is unavailable.");
        if (comment.TaskId == Guid.Empty || string.IsNullOrWhiteSpace(comment.Content)
            || comment.Content.Length > 4000 || comment.AttachmentUrl?.Length > 2048)
            throw new ArgumentException("Task comment input is invalid.", nameof(input));

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
            Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                new { capabilityKey, state }))));
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
            "admin.operations.task-comment.create" => operations.PreviewAsync(capabilityKey, actorId, input, ct),
            _ => identityContent.PreviewAsync(capabilityKey, actorId, input, ct)
        };
}
