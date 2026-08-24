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
    string? Governorate = null,
    string? Role = null,
    bool StaffOnly = false
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
    string ParentTrackingCode,
    DateTime? DateOfBirth,
    string Gender,
    string EducationStage,
    bool IsFatherAlive,
    bool IsMotherAlive,
    string Governorate,
    string? District,                    // NEW
    string Address,
    string? SecondaryPhone,              // NEW
    string? SecondaryParentPhone,         // NEW
    string? ParentPhone,
    string? MotherPhone,
    string? SchoolName,
    string? SchoolType,
    string? Nationality,
    DateTime? FatherDateOfBirth,
    DateTime? MotherDateOfBirth,
    string? SuspensionReason,
    string? AvatarSlug,
    decimal CurrentBalance,
    List<AdminStudentScopedBalanceDto> ScopedBalances
);

public record AdminStudentScopedBalanceDto(
    Guid? TeacherId,
    string TeacherName,
    decimal AvailableAmount
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
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var normalizedSearch = request.Search?.Trim();

        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.StudentProfile)
            .Include(u => u.StudentBalance)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(u => u.PhoneNumber.Contains(normalizedSearch) ||
                                     u.FullName.Contains(normalizedSearch) ||
                                     (u.StudentProfile != null && (
                                         (u.StudentProfile.StudentCode != null && u.StudentProfile.StudentCode.Contains(normalizedSearch)) ||
                                         (u.StudentProfile.ParentTrackingCode != null && u.StudentProfile.ParentTrackingCode.Contains(normalizedSearch)))));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var normalizedRole = request.Role.Trim();
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == normalizedRole));
        }

        if (request.StaffOnly)
        {
            query = query.Where(u => u.UserRoles.Any(ur =>
                ur.Role.Type == NaderGorge.Domain.Enums.RoleType.Assistant ||
                ur.Role.Type == NaderGorge.Domain.Enums.RoleType.AssistantReviewer ||
                ur.Role.Type == NaderGorge.Domain.Enums.RoleType.AssistantAcademic ||
                ur.Role.Type == NaderGorge.Domain.Enums.RoleType.Supervisor ||
                ur.Role.Type == NaderGorge.Domain.Enums.RoleType.Staff));
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
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var userIds = users.Select(u => u.Id).ToArray();
        var now = DateTime.UtcNow;
        var scopedBalanceRows = await _db.PromotionalBalanceAllocations
            .AsNoTracking()
            .Where(allocation =>
                userIds.Contains(allocation.StudentId) &&
                allocation.AvailableAmount > 0 &&
                (allocation.ExpiresAt == null || allocation.ExpiresAt > now))
            .Select(allocation => new
            {
                allocation.StudentId,
                allocation.TeacherId,
                TeacherName = allocation.Teacher != null
                    ? allocation.Teacher.User.FullName
                    : "رصيد مخصص عام",
                allocation.AvailableAmount
            })
            .ToListAsync(ct);

        var scopedBalancesByStudent = scopedBalanceRows
            .GroupBy(row => row.StudentId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(row => new { row.TeacherId, row.TeacherName })
                    .Select(balanceGroup => new AdminStudentScopedBalanceDto(
                        balanceGroup.Key.TeacherId,
                        balanceGroup.Key.TeacherName,
                        balanceGroup.Sum(row => row.AvailableAmount)))
                    .OrderBy(balance => balance.TeacherName)
                    .ToList());

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
            u.StudentProfile?.ParentTrackingCode ?? "",
            u.StudentProfile?.DateOfBirth,
            u.StudentProfile?.Gender.ToString() ?? "Unknown",
            u.StudentProfile?.EducationStage.ToString() ?? "N/A",
            u.StudentProfile?.IsFatherAlive ?? true,
            u.StudentProfile?.IsMotherAlive ?? true,
            u.StudentProfile?.Governorate ?? "N/A",
            u.StudentProfile?.District,
            u.StudentProfile?.Address ?? "",
            u.StudentProfile?.SecondaryPhone,
            u.StudentProfile?.SecondaryParentPhone,
            u.StudentProfile?.ParentPhone,
            u.StudentProfile?.MotherPhone,
            u.StudentProfile?.SchoolName,
            u.StudentProfile?.SchoolType?.ToString(),
            u.StudentProfile?.Nationality,
            u.StudentProfile?.FatherDateOfBirth,
            u.StudentProfile?.MotherDateOfBirth,
            u.SuspensionReason,
            u.StudentProfile?.AvatarSlug,
            u.StudentBalance?.CurrentBalance ?? 0m,
            scopedBalancesByStudent.GetValueOrDefault(u.Id) ?? []
        )).ToList();

        return ApiResponse<PagedResult<AdminUserListDto>>.Ok(new PagedResult<AdminUserListDto>(dtos, total, page, pageSize));
    }
}
