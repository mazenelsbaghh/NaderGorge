using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Queries;

public record GetMyVacationsQuery(Guid UserId) : IRequest<ApiResponse<List<VacationDto>>>;

public record VacationDto(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    string Reason,
    string? HandledByName,
    DateTime? HandledAt
);

public class GetMyVacationsQueryHandler : IRequestHandler<GetMyVacationsQuery, ApiResponse<List<VacationDto>>>
{
    private readonly IAppDbContext _db;

    public GetMyVacationsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<VacationDto>>> Handle(GetMyVacationsQuery request, CancellationToken ct)
    {
        var profile = await _db.EmployeeProfiles
            .FirstOrDefaultAsync(ep => ep.UserId == request.UserId, ct);

        if (profile == null)
        {
            return ApiResponse<List<VacationDto>>.Ok(new List<VacationDto>());
        }

        var vacations = await _db.EmployeeVacations
            .Include(ev => ev.HandledByUser)
            .Where(ev => ev.EmployeeId == profile.Id)
            .OrderByDescending(ev => ev.StartDate)
            .ToListAsync(ct);

        var dtos = vacations.Select(ev => new VacationDto(
            ev.Id,
            ev.StartDate,
            ev.EndDate,
            ev.Status.ToString(),
            ev.Reason,
            ev.HandledByUser?.FullName,
            ev.HandledAt
        )).ToList();

        return ApiResponse<List<VacationDto>>.Ok(dtos);
    }
}
