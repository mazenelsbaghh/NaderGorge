using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Media.Queries;

public record GetMediaPipelinesQuery(
    string? Search = null,
    MediaStage? Stage = null,
    Guid? AssigneeId = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<ApiResponse<MediaPipelineListResponse>>;

public record MediaPipelineDto(
    Guid Id,
    string Title,
    string? Description,
    string Stage,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    string? AssetFolderUrl,
    int EditingErrorCount,
    DateTime? PublishedAt,
    DateTime CreatedAt
);

public record MediaPipelineListResponse(
    List<MediaPipelineDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public class GetMediaPipelinesQueryHandler : IRequestHandler<GetMediaPipelinesQuery, ApiResponse<MediaPipelineListResponse>>
{
    private readonly IAppDbContext _db;

    public GetMediaPipelinesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<MediaPipelineListResponse>> Handle(GetMediaPipelinesQuery request, CancellationToken ct)
    {
        var query = _db.MediaProductionPipelines
            .Include(mp => mp.AssignedAgent)
            .AsQueryable();

        // Apply Search Filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            query = query.Where(mp => mp.Title.ToLower().Contains(searchLower) || (mp.Description != null && mp.Description.ToLower().Contains(searchLower)));
        }

        // Apply Stage Filter
        if (request.Stage.HasValue)
        {
            query = query.Where(mp => mp.Stage == request.Stage.Value);
        }

        // Apply Assignee Filter
        if (request.AssigneeId.HasValue && request.AssigneeId.Value != Guid.Empty)
        {
            query = query.Where(mp => mp.AssignedAgentId == request.AssigneeId.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(mp => mp.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(mp => new MediaPipelineDto(
                mp.Id,
                mp.Title,
                mp.Description,
                mp.Stage.ToString(),
                mp.AssignedAgentId,
                mp.AssignedAgent != null ? mp.AssignedAgent.FullName : null,
                mp.AssetFolderUrl,
                mp.EditingErrorCount,
                mp.PublishedAt,
                mp.CreatedAt
            ))
            .ToListAsync(ct);

        var response = new MediaPipelineListResponse(items, totalCount, request.Page, request.PageSize);
        return ApiResponse<MediaPipelineListResponse>.Ok(response);
    }
}
