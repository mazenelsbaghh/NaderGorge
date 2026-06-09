using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Finance.Queries;

public record GetPayoutsQuery(PayoutStatus? Status = null) : IRequest<ApiResponse<List<AdminPayoutDto>>>;

public record AdminPayoutDto(
    Guid Id,
    Guid TeacherId,
    string TeacherName,
    decimal Amount,
    string Status,
    string? RejectionReason,
    Guid? HandledByUserId,
    string? HandledByName,
    DateTime? HandledAt,
    DateTime CreatedAt
);

public class GetPayoutsQueryHandler : IRequestHandler<GetPayoutsQuery, ApiResponse<List<AdminPayoutDto>>>
{
    private readonly IAppDbContext _db;

    public GetPayoutsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<AdminPayoutDto>>> Handle(GetPayoutsQuery request, CancellationToken ct)
    {
        var query = _db.TeacherPayouts
            .Include(tp => tp.Teacher).ThenInclude(t => t.User)
            .Include(tp => tp.HandledByUser)
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(tp => tp.Status == request.Status.Value);
        }

        var payouts = await query
            .OrderByDescending(tp => tp.CreatedAt)
            .ToListAsync(ct);

        var dtos = payouts.Select(tp => new AdminPayoutDto(
            tp.Id,
            tp.TeacherId,
            tp.Teacher.User?.FullName ?? "Unknown Teacher",
            tp.Amount,
            tp.Status.ToString(),
            tp.RejectionReason,
            tp.HandledByUserId,
            tp.HandledByUser?.FullName,
            tp.HandledAt,
            tp.CreatedAt
        )).ToList();

        return ApiResponse<List<AdminPayoutDto>>.Ok(dtos);
    }
}
