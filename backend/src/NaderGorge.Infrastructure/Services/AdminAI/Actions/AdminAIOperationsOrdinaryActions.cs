using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Operations.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAICreateTaskInput(string Title, string Description, Guid AssigneeId, TaskPriority Priority, DateTime? DueDate);

public sealed class AdminAICreateTaskAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAICreateTaskInput, ApiResponse<Guid>>(mediator, preview)
{
    public override string Key => "admin.operations.task.create";
    protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAICreateTaskInput input, Guid actorId, string operationId) =>
        new CreateTaskCommand(input.Title, input.Description, input.AssigneeId, input.Priority, input.DueDate, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { taskId = response.Data }, 1, ["operations-tasks", "internal-chat"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["operations-tasks", "internal-chat"]);
}

public sealed record AdminAIUpdateTaskStatusInput(Guid TaskId, NaderGorge.Domain.Enums.TaskStatus Status);
public sealed class AdminAIUpdateTaskStatusAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIUpdateTaskStatusInput, ApiResponse<bool>>(mediator, preview)
{
    public override string Key => "admin.operations.task.status.update";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIUpdateTaskStatusInput input, Guid actorId, string operationId) =>
        new UpdateTaskStatusCommand(input.TaskId, input.Status, actorId);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { updated = response.Data }, 1, ["operations-tasks"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["operations-tasks"]);
}

public sealed record AdminAIAddTaskCommentInput(Guid TaskId, string Content, string? AttachmentUrl);
public sealed class AdminAIAddTaskCommentAction(IMediator mediator, IAdminAIActionPreviewSource preview)
    : AdminAIMediatRActionCapability<AdminAIAddTaskCommentInput, ApiResponse<Guid>>(mediator, preview)
{
    public override string Key => "admin.operations.task-comment.create";
    protected override IRequest<ApiResponse<Guid>> CreateCommand(AdminAIAddTaskCommentInput input, Guid actorId, string operationId) =>
        new AddTaskCommentCommand(input.TaskId, actorId, input.Content, input.AttachmentUrl);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<Guid> response) => response.Success
        ? AdminAIActionOutcomeFactory.Success(new { commentId = response.Data }, 1, ["operations-tasks", "task-comments"])
        : AdminAIActionOutcomeFactory.Rejected(new { response.Message, response.Errors }, ["operations-tasks", "task-comments"]);
}
