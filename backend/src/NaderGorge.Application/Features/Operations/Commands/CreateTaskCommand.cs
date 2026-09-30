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

public record CreateTaskCommand(
    string Title,
    string Description,
    Guid AssigneeId,
    TaskPriority Priority,
    DateTime? DueDate,
    Guid CreatedById
) : IRequest<ApiResponse<Guid>>
{
    public string? OperationId { get; init; }
}

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.AssigneeId).NotEmpty();
        RuleFor(x => x.CreatedById).NotEmpty();
    }
}

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateTaskCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateTaskCommand request, CancellationToken ct)
    {
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<Guid>.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await CreateOnceAsync(request, null, null, ct);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.Title, request.Description, request.AssigneeId,
                request.Priority, request.DueDate, request.CreatedById }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
            return prior.Scope == "operations.task.create" && prior.ActorUserId == request.CreatedById
                && prior.RequestHash == requestHash
                ? ApiResponse<Guid>.Ok(prior.ResultEntityId)
                : ApiResponse<Guid>.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);

        var created = await CreateOnceAsync(request, operationId, requestHash, ct);
        await transaction.CommitAsync(ct);
        return created;
    }

    private async Task<ApiResponse<Guid>> CreateOnceAsync(CreateTaskCommand request,
        string? operationId, string? requestHash, CancellationToken ct)
    {
        // 1. Verify assignee exists
        var assignee = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.AssigneeId, ct);

        if (assignee == null)
        {
            throw new KeyNotFoundException("Assignee user not found.");
        }

        // 2. Block student assignment
        var isStudent = assignee.UserRoles.Any(ur => ur.Role.Type == RoleType.Student);
        if (isStudent)
        {
            throw new InvalidOperationException("Cannot assign tasks to student users.");
        }

        // 3. Verify creator exists
        var creatorExists = await _db.Users.AnyAsync(u => u.Id == request.CreatedById, ct);
        if (!creatorExists)
        {
            throw new KeyNotFoundException("Creator user not found.");
        }

        // 4. Create the task
        var task = new TaskItem
        {
            Title = request.Title,
            Description = request.Description,
            AssigneeId = request.AssigneeId,
            CreatedById = request.CreatedById,
            Priority = request.Priority,
            DueDate = request.DueDate is { } dueDate ? CairoTime.ToUtc(dueDate) : null,
            Status = TaskStatus.New
        };

        _db.TaskItems.Add(task);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "CreateTask",
            EntityType = nameof(TaskItem),
            EntityId = task.Id,
            PerformedByUserId = request.CreatedById,
            NewValues = $"Title: {task.Title}, AssigneeId: {task.AssigneeId}, Priority: {task.Priority}, DueDate: {task.DueDate}",
            CreatedAt = DateTime.UtcNow
        });

        // 5. Automatically create a Workroom Chat for the task
        var supervisorRole = await _db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Supervisor, ct);
        var supervisorUserIds = supervisorRole != null
            ? await _db.UserRoles
                .Where(ur => ur.RoleId == supervisorRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync(ct)
            : new List<Guid>();

        var participantUserIds = new List<Guid> { request.CreatedById, request.AssigneeId }
            .Concat(supervisorUserIds)
            .Distinct()
            .ToList();

        var chatRoom = new ChatRoom
        {
            Name = $"Workroom: {task.Title}",
            Type = ChatRoomType.Workroom,
            TaskItemId = task.Id,
            CreatedByUserId = request.CreatedById,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var userId in participantUserIds)
        {
            chatRoom.ChatParticipants.Add(new ChatParticipant
            {
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            });
        }

        _db.ChatRooms.Add(chatRoom);
        if (operationId is not null)
            _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
            {
                OperationId = operationId, Scope = "operations.task.create",
                ActorUserId = request.CreatedById, RequestHash = requestHash!, ResultEntityId = task.Id
            });
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(task.Id);
    }
}
