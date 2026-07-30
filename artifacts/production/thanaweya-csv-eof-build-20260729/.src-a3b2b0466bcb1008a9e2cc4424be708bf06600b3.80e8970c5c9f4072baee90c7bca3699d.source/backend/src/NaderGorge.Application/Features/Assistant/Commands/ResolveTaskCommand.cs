using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Assistant;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assistant.Commands;

public record ResolveTaskCommand(Guid TaskId, Guid ResolvedByUserId, string ResolutionNotes) : IRequest<ApiResponse<Guid>>;

public class ResolveTaskCommandHandler : IRequestHandler<ResolveTaskCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _dbContext;

    public ResolveTaskCommandHandler(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<Guid>> Handle(ResolveTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _dbContext.AssistantTasks
            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

        if (task == null)
            return ApiResponse<Guid>.Fail("Task not found.");

        if (task.Status != AssistantTaskStatus.Open && task.Status != AssistantTaskStatus.InReview)
            return ApiResponse<Guid>.Fail("Task is already resolved or cancelled.");

        task.Status = AssistantTaskStatus.Done;
        task.AssignedAssistantId = request.ResolvedByUserId; // Usually assigned then completed, but works
        task.CompletedAt = DateTime.UtcNow;

        _dbContext.AssistantTasks.Update(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<Guid>.Ok(task.Id, "Task resolved successfully.");
    }
}
