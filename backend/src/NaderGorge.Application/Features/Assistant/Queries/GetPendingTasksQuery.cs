using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Assistant;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assistant.Queries;

public record GetPendingTasksQuery(AssistantTaskType? TypeFilter) : IRequest<ApiResponse<List<AssistantTaskDto>>>;

public record AssistantTaskDto(
    Guid Id, 
    AssistantTaskType TaskType, 
    Guid? StudentId, 
    string StudentName, 
    Guid? ReferenceEntityId, 
    string Status, 
    DateTime CreatedAt);

public class GetPendingTasksQueryHandler : IRequestHandler<GetPendingTasksQuery, ApiResponse<List<AssistantTaskDto>>>
{
    private readonly IAppDbContext _dbContext;

    public GetPendingTasksQueryHandler(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<List<AssistantTaskDto>>> Handle(GetPendingTasksQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AssistantTasks
            .Include(t => t.Student)
            .Where(t => t.Status == AssistantTaskStatus.Open || t.Status == AssistantTaskStatus.InReview);

        if (request.TypeFilter.HasValue)
        {
            query = query.Where(t => t.TaskType == request.TypeFilter.Value);
        }

        var tasks = await query
            .OrderBy(t => t.CreatedAt)
            .Select(t => new AssistantTaskDto(
                t.Id,
                t.TaskType,
                t.StudentId,
                t.Student != null ? t.Student.FullName : "Unknown",
                t.ReferenceEntityId,
                t.Status.ToString(),
                t.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return ApiResponse<List<AssistantTaskDto>>.Ok(tasks);
    }
}
