using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Queries;

public record AdminGetEmployeesQuery(string? Search = null) : IRequest<ApiResponse<List<EmployeeDto>>>;

public record EmployeeDto(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string[] Roles,
    decimal? BasicSalary,
    string? StandardStartTime,
    int? TargetDailyHours,
    bool HasProfile
);

public class AdminGetEmployeesQueryHandler : IRequestHandler<AdminGetEmployeesQuery, ApiResponse<List<EmployeeDto>>>
{
    private readonly IAppDbContext _db;

    public AdminGetEmployeesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<EmployeeDto>>> Handle(AdminGetEmployeesQuery request, CancellationToken ct)
    {
        var usersQuery = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.EmployeeProfile)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name != "Student")); // Non-students

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            usersQuery = usersQuery.Where(u => u.FullName.ToLower().Contains(searchLower) || u.PhoneNumber.Contains(searchLower));
        }

        var users = await usersQuery.ToListAsync(ct);

        var dtos = users.Select(u => new EmployeeDto(
            u.Id,
            u.FullName,
            u.PhoneNumber,
            u.UserRoles.Select(ur => ur.Role.Name).ToArray(),
            u.EmployeeProfile?.BasicSalary,
            u.EmployeeProfile?.StandardStartTime.ToString(@"hh\:mm\:ss"),
            u.EmployeeProfile?.TargetDailyHours,
            u.EmployeeProfile != null
        )).ToList();

        return ApiResponse<List<EmployeeDto>>.Ok(dtos);
    }
}
