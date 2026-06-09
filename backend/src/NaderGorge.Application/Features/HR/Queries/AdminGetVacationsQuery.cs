using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Queries;

public record AdminGetVacationsQuery(
    string? Search = null,
    string? Status = null
) : IRequest<ApiResponse<List<AdminVacationDto>>>;

public record AdminVacationDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string EmployeePhone,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    string Reason,
    string? HandledByName,
    DateTime? HandledAt
);

public class AdminGetVacationsQueryHandler : IRequestHandler<AdminGetVacationsQuery, ApiResponse<List<AdminVacationDto>>>
{
    private readonly IAppDbContext _db;

    public AdminGetVacationsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<AdminVacationDto>>> Handle(AdminGetVacationsQuery request, CancellationToken ct)
    {
        var query = _db.EmployeeVacations
            .Include(ev => ev.HandledByUser)
            .Include(ev => ev.Employee).ThenInclude(ep => ep!.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (Enum.TryParse<VacationStatus>(request.Status, true, out var statusEnum))
            {
                query = query.Where(ev => ev.Status == statusEnum);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            query = query.Where(ev =>
                ev.Employee != null && ev.Employee.User != null &&
                (ev.Employee.User.FullName.ToLower().Contains(searchLower) || ev.Employee.User.PhoneNumber.Contains(searchLower)));
        }

        var vacations = await query
            .OrderByDescending(ev => ev.Status == VacationStatus.Pending) // Show pending first
            .ThenByDescending(ev => ev.StartDate)
            .ToListAsync(ct);

        var dtos = vacations.Select(ev => new AdminVacationDto(
            ev.Id,
            ev.EmployeeId,
            ev.Employee?.User?.FullName ?? "Unknown",
            ev.Employee?.User?.PhoneNumber ?? string.Empty,
            ev.StartDate,
            ev.EndDate,
            ev.Status.ToString(),
            ev.Reason,
            ev.HandledByUser?.FullName,
            ev.HandledAt
        )).ToList();

        return ApiResponse<List<AdminVacationDto>>.Ok(dtos);
    }
}
