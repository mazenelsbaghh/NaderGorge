using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
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
        var taskExists = await _db.TaskItems.AnyAsync(t => t.Id == request.TaskId, ct);
        if (!taskExists)
        {
            throw new KeyNotFoundException("Task not found.");
        }

        var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, ct);
        if (!userExists)
        {
            throw new KeyNotFoundException("User not found.");
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
