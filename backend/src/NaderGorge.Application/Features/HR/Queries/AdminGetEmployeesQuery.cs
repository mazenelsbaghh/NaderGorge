using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Queries;

public record AdminGetEmployeesQuery(string? Search = null) : IRequest<ApiResponse<List<EmployeeDto>>>;

public record EmployeeProfileDto(
    Guid Id,
    string EmployeeNumber,
    string EmploymentStatus,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    string WorkMode,
    decimal BasicSalary,
    string StandardStartTime,
    int TargetDailyHours,
    DateTime? UpdatedAt,
    string? RowVersion
);

public record EmployeeDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string[] Roles,
    decimal? BasicSalary,
    string? StandardStartTime,
    int? TargetDailyHours,
    bool HasProfile,
    EmployeeProfileDto? EmployeeProfile,
    string? RowVersion
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
        var profilesQuery = _db.EmployeeProfiles
            .Include(profile => profile.User)!
                .ThenInclude(user => user!.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            profilesQuery = profilesQuery.Where(profile =>
                profile.User != null &&
                (profile.User.FullName.ToLower().Contains(searchLower) ||
                 profile.User.PhoneNumber.Contains(searchLower)));
        }

        var profiles = await profilesQuery.ToListAsync(ct);

        var dtos = profiles
            .Where(profile => profile.User != null)
            .Select(profile => new EmployeeDto(
                profile.UserId,
                profile.UserId,
                profile.User!.FullName,
                profile.User.PhoneNumber,
                profile.User.UserRoles.Select(userRole => userRole.Role.Name).ToArray(),
                profile.BasicSalary,
                profile.StandardStartTime.ToString(@"hh\:mm\:ss"),
                profile.TargetDailyHours,
                true,
                new EmployeeProfileDto(
                    profile.Id,
                    profile.EmployeeNumber,
                    profile.EmploymentStatus.ToString(),
                    profile.HireDate,
                    profile.TerminationDate,
                    profile.WorkMode.ToString(),
                    profile.BasicSalary,
                    profile.StandardStartTime.ToString(@"hh\:mm\:ss"),
                    profile.TargetDailyHours,
                    profile.UpdatedAt,
                    profile.UpdatedAt?.ToString("O")),
                profile.UpdatedAt?.ToString("O")))
            .ToList();

        return ApiResponse<List<EmployeeDto>>.Ok(dtos);
    }
}
