using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Queries;

public record AdminGetAttendanceQuery(
    string? Search = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null
) : IRequest<ApiResponse<List<AdminAttendanceLogDto>>>;

public record AdminAttendanceLogDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string EmployeePhone,
    DateOnly Date,
    DateTime ClockIn,
    DateTime? ClockOut,
    int LateMinutes,
    string Status,
    string IpAddress,
    string UserAgent,
    double? DurationMinutes
);

public class AdminGetAttendanceQueryHandler : IRequestHandler<AdminGetAttendanceQuery, ApiResponse<List<AdminAttendanceLogDto>>>
{
    private readonly IAppDbContext _db;

    public AdminGetAttendanceQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<AdminAttendanceLogDto>>> Handle(AdminGetAttendanceQuery request, CancellationToken ct)
    {
        var query = _db.AttendanceLogs
            .Include(al => al.Employee).ThenInclude(ep => ep!.User)
            .AsQueryable();

        if (request.StartDate.HasValue)
        {
            query = query.Where(al => al.Date >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(al => al.Date <= request.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            query = query.Where(al =>
                al.Employee != null && al.Employee.User != null &&
                (al.Employee.User.FullName.ToLower().Contains(searchLower) || al.Employee.User.PhoneNumber.Contains(searchLower)));
        }

        var logs = await query
            .OrderByDescending(al => al.Date)
            .ThenByDescending(al => al.ClockIn)
            .ToListAsync(ct);

        var dtos = logs.Select(al => new AdminAttendanceLogDto(
            al.Id,
            al.EmployeeId,
            al.Employee?.User?.FullName ?? "Unknown",
            al.Employee?.User?.PhoneNumber ?? string.Empty,
            al.Date,
            al.ClockIn,
            al.ClockOut,
            al.LateMinutes,
            al.Status.ToString(),
            al.IpAddress,
            al.UserAgent,
            al.ClockOut.HasValue ? (al.ClockOut.Value - al.ClockIn).TotalMinutes : null
        )).ToList();

        return ApiResponse<List<AdminAttendanceLogDto>>.Ok(dtos);
    }
}
