using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.VideoTypes;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.VideoTypes.Queries;

public record GetVideoTypesQuery(bool IncludeInactive = false) : IRequest<ApiResponse<List<VideoTypeDto>>>;

public sealed class GetVideoTypesQueryHandler : IRequestHandler<GetVideoTypesQuery, ApiResponse<List<VideoTypeDto>>>
{
    private readonly IAppDbContext _db;

    public GetVideoTypesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<VideoTypeDto>>> Handle(GetVideoTypesQuery request, CancellationToken ct)
    {
        var query = _db.VideoTypes.AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(type => type.IsActive);
        }

        var types = await query
            .OrderBy(type => type.SortOrder)
            .ThenBy(type => type.Name)
            .Select(VideoTypeRules.Projection)
            .ToListAsync(ct);

        return ApiResponse<List<VideoTypeDto>>.Ok(types);
    }
}
