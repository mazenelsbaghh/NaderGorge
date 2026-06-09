using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Media.Queries;

public record GetMediaKpisQuery : IRequest<ApiResponse<MediaKpiResponse>>;

public record MediaKpiResponse(
    int TotalPublished,
    double AverageEditingDays,
    List<EditorKpiDto> EditorLeaderboard
);

public record EditorKpiDto(
    Guid EditorId,
    string EditorName,
    int TotalProduced,
    int TotalErrors
);

public class GetMediaKpisQueryHandler : IRequestHandler<GetMediaKpisQuery, ApiResponse<MediaKpiResponse>>
{
    private readonly IAppDbContext _db;

    public GetMediaKpisQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<MediaKpiResponse>> Handle(GetMediaKpisQuery request, CancellationToken ct)
    {
        // Total Published
        var totalPublished = await _db.MediaProductionPipelines
            .CountAsync(mp => mp.Stage == MediaStage.Published, ct);

        // Average Editing Days
        var publishedItems = await _db.MediaProductionPipelines
            .Where(mp => mp.Stage == MediaStage.Published && mp.PublishedAt != null)
            .Select(mp => new { mp.CreatedAt, PublishedAt = mp.PublishedAt })
            .ToListAsync(ct);

        double averageEditingDays = 0;
        if (publishedItems.Any())
        {
            var totalDays = publishedItems.Sum(item => ((item.PublishedAt ?? item.CreatedAt) - item.CreatedAt).TotalDays);
            averageEditingDays = Math.Round(totalDays / publishedItems.Count, 1);
        }

        // Editor Leaderboard
        var editorStats = await _db.MediaProductionPipelines
            .Where(mp => mp.AssignedAgentId != null)
            .GroupBy(mp => new { mp.AssignedAgentId, Name = mp.AssignedAgent != null ? mp.AssignedAgent.FullName : "محرر غير معروف" })
            .Select(g => new EditorKpiDto(
                g.Key.AssignedAgentId!.Value,
                g.Key.Name,
                g.Count(),
                g.Sum(mp => mp.EditingErrorCount)
            ))
            .OrderByDescending(e => e.TotalProduced)
            .ToListAsync(ct);

        var response = new MediaKpiResponse(totalPublished, averageEditingDays, editorStats);
        return ApiResponse<MediaKpiResponse>.Ok(response);
    }
}
