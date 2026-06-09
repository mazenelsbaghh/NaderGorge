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
) : IRequest<ApiResponse<Guid>>;

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
            throw new UnauthorizedAccessException("You are not authorized to comment on this task.");
        }

        var comment = new TaskComment
        {
            TaskId = request.TaskId,
            UserId = request.UserId,
            Content = request.Content,
            AttachmentUrl = request.AttachmentUrl
        };

        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(comment.Id, "Comment posted successfully.");
    }
}
