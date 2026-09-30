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
            AdminAICreateSocialPlanInput socialPlan when capabilityKey == "admin.tools.social-plan.create" =>
                PreviewSocialPlanCreateAsync(capabilityKey, actorId, socialPlan, ct),
            AdminAICreateMediaPipelineInput pipeline when capabilityKey == "admin.tools.media-pipeline.create" =>
                PreviewMediaPipelineCreateAsync(capabilityKey, actorId, pipeline, ct),
            AdminAICreateTaskInput create when capabilityKey == "admin.operations.task.create" =>
                PreviewTaskCreateAsync(capabilityKey, actorId, create, ct),
            AdminAIAddTaskCommentInput comment when capabilityKey == "admin.operations.task-comment.create" =>
                PreviewTaskCommentAsync(capabilityKey, actorId, comment, ct),
            AdminAIUpdateTaskStatusInput status when capabilityKey == "admin.operations.task.status.update" =>
                PreviewTaskStatusAsync(capabilityKey, actorId, status, ct),
            AdminAIResolveTaskApprovalInput approval when capabilityKey == "admin.operations.task.approval.resolve" =>
                PreviewTaskApprovalAsync(capabilityKey, actorId, approval, ct),
            _ => throw new NotSupportedException("Admin AI action preview capability is unavailable.")
        };

    private async Task<AdminAIActionPreview> PreviewSocialPlanCreateAsync(
        string capabilityKey, Guid actorId, AdminAICreateSocialPlanInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 250
            || input.Description?.Length > 2000 || input.Script?.Length > 10000
            || !Enum.IsDefined(input.Platform) || input.ScheduledDate == default)
            throw new ArgumentException("Social plan input is invalid.", nameof(input));
        if (input.Status is not (SocialPlanStatus.Draft or SocialPlanStatus.Scripting))
            throw new AdminAIActionPreviewUnavailableException(
                "Scheduling or publishing requires a high-risk action.");
        if (!await db.Users.AsNoTracking().AnyAsync(item => item.Id == actorId, ct))
            throw new AdminAIActionPreviewUnavailableException("The actor is unavailable.");

        var linkedPipelineId = input.MediaProductionPipelineId.GetValueOrDefault();
        var pipeline = await db.MediaProductionPipelines.AsNoTracking()
            .Where(item => item.Id == linkedPipelineId)
            .Select(item => new { item.Id, item.Title, item.Stage })
            .SingleOrDefaultAsync(ct);
        if (linkedPipelineId != Guid.Empty && pipeline is null)
            throw new AdminAIActionPreviewUnavailableException("The linked media pipeline is unavailable.");

        return new AdminAIActionPreview(
            "social-plan", linkedPipelineId == Guid.Empty ? "social-plan:new"
                : $"media-pipeline:{linkedPipelineId:D}",
            new { linkedPipelineTitle = pipeline?.Title, linkedPipelineStage = pipeline?.Stage },
            new { input.Title, input.Description, input.Platform, input.Status, input.ScheduledDate,
                input.MediaProductionPipelineId, scriptProvided = !string.IsNullOrWhiteSpace(input.Script) },
            new { draftPlanWillBeCreated = true, affected = 1 },
            new { valid = true },
            Fingerprint(capabilityKey, new { actorId, pipeline?.Id, pipeline?.Title, pipeline?.Stage }));
    }

    private async Task<AdminAIActionPreview> PreviewMediaPipelineCreateAsync(
        string capabilityKey, Guid actorId, AdminAICreateMediaPipelineInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 250
            || input.Description?.Length > 2000 || input.AssetFolderUrl?.Length > 2000)
            throw new ArgumentException("Media pipeline input is invalid.", nameof(input));
        if (!await db.Users.AsNoTracking().AnyAsync(item => item.Id == actorId, ct))
            throw new AdminAIActionPreviewUnavailableException("The actor is unavailable.");

        var assignedAgentId = input.AssignedAgentId.GetValueOrDefault();
        var agent = await db.Users.AsNoTracking()
            .Where(item => item.Id == assignedAgentId)
            .Select(item => new
            {
                item.Id, item.FullName,
                IsStudent = item.UserRoles.Any(role => role.Role.Type == RoleType.Student)
            })
            .SingleOrDefaultAsync(ct);
        if (assignedAgentId != Guid.Empty && agent is null)
            throw new AdminAIActionPreviewUnavailableException("The assigned agent is unavailable.");
        if (agent?.IsStudent == true)
            throw new AdminAIActionPreviewUnavailableException("Media work cannot be assigned to students.");

        return new AdminAIActionPreview(
            "media-pipeline", assignedAgentId == Guid.Empty ? "media-pipeline:new"
                : $"agent:{assignedAgentId:D}",
            new { agentName = agent?.FullName, agentIsStudent = agent?.IsStudent },
            new { input.Title, input.Description, input.AssignedAgentId,
                assetFolderProvided = !string.IsNullOrWhiteSpace(input.AssetFolderUrl) },
            new { pipelineWillBeCreated = true, affected = 1 },
            new { valid = true },
            Fingerprint(capabilityKey, new { actorId, agent?.Id, agent?.FullName, agent?.IsStudent }));
    }

    private async Task<AdminAIActionPreview> PreviewTaskCreateAsync(
        string capabilityKey, Guid actorId, AdminAICreateTaskInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 255
            || input.Description?.Length > 4000 || input.AssigneeId == Guid.Empty
            || !Enum.IsDefined(input.Priority))
            throw new ArgumentException("Task creation input is invalid.", nameof(input));

        var assignee = await db.Users.AsNoTracking()
            .Where(item => item.Id == input.AssigneeId)
            .Select(item => new
            {
                item.Id, item.FullName,
                IsStudent = item.UserRoles.Any(role => role.Role.Type == RoleType.Student)
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The assignee is unavailable.");
        if (assignee.IsStudent)
            throw new AdminAIActionPreviewUnavailableException("Tasks cannot be assigned to students.");
        if (!await db.Users.AsNoTracking().AnyAsync(item => item.Id == actorId, ct))
            throw new AdminAIActionPreviewUnavailableException("The actor is unavailable.");

        var supervisorRole = await db.Roles.AsNoTracking()
            .Where(item => item.Type == RoleType.Supervisor)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(ct);
        var supervisorIds = supervisorRole.HasValue
            ? await db.UserRoles.AsNoTracking()
                .Where(item => item.RoleId == supervisorRole.Value)
                .OrderBy(item => item.UserId)
                .Select(item => item.UserId)
                .ToListAsync(ct)
            : [];

        return new AdminAIActionPreview(
            "task", $"assignee:{assignee.Id:D}",
            new { assignee.FullName, assignee.IsStudent, supervisorCount = supervisorIds.Count },
            new { input.Title, input.Description, input.AssigneeId, input.Priority, input.DueDate },
            new { taskWillBeCreated = true, workroomWillBeCreated = true, affected = 1 },
            new { valid = true },
            Fingerprint(capabilityKey, new { actorId, assignee.Id, assignee.FullName,
                assignee.IsStudent, supervisorIds }));
    }

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
    AdminAIOperationsPreviewSource operations,
    AdminAIAssessmentPreviewSource assessment,
    AdminAITeacherFinancialReviewPreviewSource teacherFinance) : IAdminAIActionPreviewSource
{
    public Task<AdminAIActionPreview> PreviewAsync<TInput>(
        string capabilityKey, Guid actorId, TInput input, CancellationToken ct) where TInput : class =>
        capabilityKey switch
        {
            "admin.finance.teacher-event.review" =>
                teacherFinance.PreviewAsync(capabilityKey, actorId, input, ct),
            "admin.tools.media-pipeline.create" or "admin.tools.social-plan.create"
                or "admin.operations.task.create"
                or "admin.operations.task-comment.create" or "admin.operations.task.status.update"
                or "admin.operations.task.approval.resolve" =>
                operations.PreviewAsync(capabilityKey, actorId, input, ct),
            "admin.assessment.lesson-comment.approve" or "admin.assessment.community-post.approve"
                or "admin.assessment.community-comment.approve" =>
                assessment.PreviewAsync(capabilityKey, actorId, input, ct),
            _ => identityContent.PreviewAsync(capabilityKey, actorId, input, ct)
        };
}
