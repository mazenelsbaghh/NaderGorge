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

namespace NaderGorge.Application.Features.Operations.Commands;

public record AddTaskCommentCommand(
    Guid TaskId,
    Guid UserId,
    string Content,
    string? AttachmentUrl = null
) : IRequest<ApiResponse<Guid>>
{
    public string? OperationId { get; init; }
}

public class AddTaskCommentCommandValidator : AbstractValidator<AddTaskCommentCommand>
{
    public AddTaskCommentCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.AttachmentUrl).MaximumLength(2048);
    }
}

public class AddTaskCommentCommandHandler : IRequestHandler<AddTaskCommentCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public AddTaskCommentCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<Guid>> Handle(AddTaskCommentCommand request, CancellationToken ct)
    {
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<Guid>.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await AddOnceAsync(request, null, null, ct);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.TaskId, request.UserId, request.Content, request.AttachmentUrl }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
            return prior.Scope == "operations.task-comment.create" && prior.ActorUserId == request.UserId
                && prior.RequestHash == requestHash
                ? ApiResponse<Guid>.Ok(prior.ResultEntityId, "Comment posted successfully.")
                : ApiResponse<Guid>.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);

        var created = await AddOnceAsync(request, operationId, requestHash, ct);
        await transaction.CommitAsync(ct);
        return created;
    }

    private async Task<ApiResponse<Guid>> AddOnceAsync(AddTaskCommentCommand request,
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

        if (!isManager && task.AssigneeId != request.UserId && task.CreatedById != request.UserId)
        {
            throw new ForbiddenException("You are not authorized to comment on this task.");
        }

        var comment = new TaskComment
        {
            TaskId = request.TaskId,
            UserId = request.UserId,
            Content = request.Content,
            AttachmentUrl = request.AttachmentUrl
        };

        _db.TaskComments.Add(comment);
        if (operationId is not null)
            _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
            {
                OperationId = operationId, Scope = "operations.task-comment.create",
                ActorUserId = request.UserId, RequestHash = requestHash!, ResultEntityId = comment.Id
            });
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(comment.Id, "Comment posted successfully.");
    }
}
