using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetPendingWatchRequestCountQuery : IRequest<ApiResponse<int>>;

public sealed class GetPendingWatchRequestCountQueryHandler(IAppDbContext db)
    : IRequestHandler<GetPendingWatchRequestCountQuery, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(GetPendingWatchRequestCountQuery request, CancellationToken ct)
    {
        var count = await db.ExtraWatchRequests.CountAsync(item => item.Status == RequestStatus.Pending, ct);
        return ApiResponse<int>.Ok(count);
    }
}
