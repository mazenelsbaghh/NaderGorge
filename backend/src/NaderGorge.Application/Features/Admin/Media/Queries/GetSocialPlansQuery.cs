using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Media.Queries;

public record GetSocialPlansQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IRequest<ApiResponse<List<SocialPlanDto>>>;

public record SocialPlanDto(
    Guid Id,
    string Title,
    string? Description,
    string? Script,
    string Platform,
    string Status,
    DateTime ScheduledDate,
    Guid? MediaProductionPipelineId,
    string? MediaProductionPipelineTitle,
    string? MediaProductionPipelineStage,
    DateTime CreatedAt
);

public class GetSocialPlansQueryHandler : IRequestHandler<GetSocialPlansQuery, ApiResponse<List<SocialPlanDto>>>
{
    private readonly IAppDbContext _db;

    public GetSocialPlansQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<SocialPlanDto>>> Handle(GetSocialPlansQuery request, CancellationToken ct)
    {
        var query = _db.SocialMediaPlans
            .Include(sp => sp.MediaProductionPipeline)
            .AsQueryable();

        if (request.StartDate.HasValue)
        {
            query = query.Where(sp => sp.ScheduledDate >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(sp => sp.ScheduledDate <= request.EndDate.Value);
        }

        var items = await query
            .OrderBy(sp => sp.ScheduledDate)
            .Select(sp => new SocialPlanDto(
                sp.Id,
                sp.Title,
                sp.Description,
                sp.Script,
                sp.Platform.ToString(),
                sp.Status.ToString(),
                sp.ScheduledDate,
                sp.MediaProductionPipelineId,
                sp.MediaProductionPipeline != null ? sp.MediaProductionPipeline.Title : null,
                sp.MediaProductionPipeline != null ? sp.MediaProductionPipeline.Stage.ToString() : null,
                sp.CreatedAt
            ))
            .ToListAsync(ct);

        return ApiResponse<List<SocialPlanDto>>.Ok(items);
    }
}
