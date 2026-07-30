using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Admin.Reports.Queries;

public record GetAdminAuditLogsQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    Guid? PerformedByUserId,
    string? EntityType,
    int Page = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<AuditLogDetailDto>>>;

public record AuditLogDetailDto(
    Guid Id,
    string Action,
    string EntityType,
    Guid? EntityId,
    Guid? PerformedByUserId,
    string? PerformedByUserName,
    string? PerformedByUserPhone,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    DateTime CreatedAt
);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public class GetAdminAuditLogsQueryHandler : IRequestHandler<GetAdminAuditLogsQuery, ApiResponse<PagedResult<AuditLogDetailDto>>>
{
    private readonly IAppDbContext _db;

    public GetAdminAuditLogsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<PagedResult<AuditLogDetailDto>>> Handle(GetAdminAuditLogsQuery request, CancellationToken ct)
    {
        var query = _db.AuditLogs
            .Include(a => a.PerformedByUser)
            .AsQueryable();

        if (request.StartDate.HasValue)
        {
            var startUtc = request.StartDate.Value.ToUniversalTime();
            query = query.Where(a => a.CreatedAt >= startUtc);
        }

        if (request.EndDate.HasValue)
        {
            var endUtc = request.EndDate.Value.ToUniversalTime();
            query = query.Where(a => a.CreatedAt <= endUtc);
        }

        if (request.PerformedByUserId.HasValue && request.PerformedByUserId.Value != Guid.Empty)
        {
            query = query.Where(a => a.PerformedByUserId == request.PerformedByUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            var typeLower = request.EntityType.Trim().ToLower();
            query = query.Where(a => a.EntityType.ToLower() == typeLower);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(a => new AuditLogDetailDto(
            a.Id,
            a.Action,
            a.EntityType,
            a.EntityId,
            a.PerformedByUserId,
            a.PerformedByUser?.FullName,
            a.PerformedByUser?.PhoneNumber,
            a.OldValues,
            a.NewValues,
            a.IpAddress,
            a.CreatedAt
        )).ToList();

        var result = new PagedResult<AuditLogDetailDto>(dtos, totalCount, request.Page, request.PageSize);
        return ApiResponse<PagedResult<AuditLogDetailDto>>.Ok(result);
    }
}
