using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetUserAuditLogsQuery(Guid UserId) : IRequest<ApiResponse<List<UserAuditLogDto>>>;

public record UserAuditLogDto(
    Guid Id,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    DateTime CreatedAt
);

public class GetUserAuditLogsQueryHandler : IRequestHandler<GetUserAuditLogsQuery, ApiResponse<List<UserAuditLogDto>>>
{
    private readonly IAppDbContext _db;

    public GetUserAuditLogsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<UserAuditLogDto>>> Handle(GetUserAuditLogsQuery request, CancellationToken ct)
    {
        var logs = await _db.AuditLogs
            .AsNoTracking()
            .Where(l => l.PerformedByUserId == request.UserId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        var dtos = logs.Select(l => new UserAuditLogDto(
            l.Id,
            l.Action,
            l.EntityType,
            l.EntityId,
            l.OldValues,
            l.NewValues,
            l.IpAddress,
            l.CreatedAt
        )).ToList();

        return ApiResponse<List<UserAuditLogDto>>.Ok(dtos);
    }
}
