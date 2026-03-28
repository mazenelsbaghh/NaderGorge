using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record ListUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? EducationStage = null,
    string? GradeLevel = null,
    string? StudyTrack = null,
    string? Gender = null,
    string? Governorate = null
) : IRequest<ApiResponse<PagedResult<AdminUserListDto>>>;

public record AdminUserListDto(
    Guid Id, 
    string PhoneNumber, 
    string Status, 
    string FullName, 
    string Grade, 
    string Track, 
    DateTime CreatedAt, 
    string[] Roles,
    string StudentCode,
    DateTime? DateOfBirth,
    string Gender,
    string EducationStage,
    bool IsFatherAlive,
    bool IsMotherAlive,
    string Governorate,
    string? District,                    // NEW
    string Address,
    string? SecondaryPhone,              // NEW
    string? SecondaryParentPhone         // NEW
);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, ApiResponse<PagedResult<AdminUserListDto>>>
{
    private readonly IAppDbContext _db;

    public ListUsersQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<PagedResult<AdminUserListDto>>> Handle(ListUsersQuery request, CancellationToken ct)
    {
        var query = _db.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
             query = query.Where(u => u.PhoneNumber.Contains(request.Search) || 
                                      u.FullName.Contains(request.Search) ||
                                      (u.StudentProfile != null && u.StudentProfile.StudentCode != null && u.StudentProfile.StudentCode.Contains(request.Search)));
        }
        
        if (!string.IsNullOrWhiteSpace(request.EducationStage) && Enum.TryParse<NaderGorge.Domain.Enums.EducationStage>(request.EducationStage, true, out var stage))
        {
            query = query.Where(u => u.StudentProfile != null && u.StudentProfile.EducationStage == stage);
        }

        if (!string.IsNullOrWhiteSpace(request.GradeLevel) && Enum.TryParse<NaderGorge.Domain.Enums.GradeLevel>(request.GradeLevel, true, out var grade))
        {
            query = query.Where(u => u.StudentProfile != null && u.StudentProfile.GradeLevel == grade);
        }

        if (!string.IsNullOrWhiteSpace(request.StudyTrack) && Enum.TryParse<NaderGorge.Domain.Enums.StudyTrack>(request.StudyTrack, true, out var track))
        {
            query = query.Where(u => u.StudentProfile != null && u.StudentProfile.StudyTrack == track);
        }
        
        if (!string.IsNullOrWhiteSpace(request.Gender) && Enum.TryParse<NaderGorge.Domain.Enums.Gender>(request.Gender, true, out var gender))
        {
            query = query.Where(u => u.StudentProfile != null && u.StudentProfile.Gender == gender);
        }
        
        if (!string.IsNullOrWhiteSpace(request.Governorate))
        {
             query = query.Where(u => u.StudentProfile != null && u.StudentProfile.Governorate.Contains(request.Governorate));
        }

        var total = await query.CountAsync(ct);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = users.Select(u => new AdminUserListDto(
            u.Id,
            u.PhoneNumber,
            u.IsActive ? "Active" : "Disabled",
            u.FullName,
            u.StudentProfile?.GradeLevel.ToString() ?? "N/A",
            u.StudentProfile?.StudyTrack?.ToString() ?? "N/A",
            u.CreatedAt,
            u.UserRoles.Select(ur => ur.Role.Name).ToArray(),
            u.StudentProfile?.StudentCode ?? "",
            u.StudentProfile?.DateOfBirth,
            u.StudentProfile?.Gender.ToString() ?? "Unknown",
            u.StudentProfile?.EducationStage.ToString() ?? "N/A",
            u.StudentProfile?.IsFatherAlive ?? true,
            u.StudentProfile?.IsMotherAlive ?? true,
            u.StudentProfile?.Governorate ?? "N/A",
            u.StudentProfile?.District,
            u.StudentProfile?.Address ?? "",
            u.StudentProfile?.SecondaryPhone,
            u.StudentProfile?.SecondaryParentPhone
        )).ToList();

        return ApiResponse<PagedResult<AdminUserListDto>>.Ok(new PagedResult<AdminUserListDto>(dtos, total, request.Page, request.PageSize));
    }
}
