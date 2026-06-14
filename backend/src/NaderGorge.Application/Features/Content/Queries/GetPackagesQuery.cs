using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetPackagesQuery(Guid UserId) : IRequest<ApiResponse<List<PackageDto>>>;

public record PackageDto(
    Guid Id, 
    string Name, 
    string Description, 
    decimal Price, 
    Guid ProgramId, 
    bool IsEnrolled, 
    Guid TeacherId, 
    Guid SubjectId,
    string TeacherName,
    string? TeacherProfileImageUrl,
    string SubjectName,
    string? TeacherBio,
    string? TeacherSpecialization,
    string TargetGrade,
    string? ImageUrl
);

public class GetPackagesQueryHandler : IRequestHandler<GetPackagesQuery, ApiResponse<List<PackageDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public GetPackagesQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<List<PackageDto>>> Handle(GetPackagesQuery request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.TeacherProfile)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);

        var query = _db.Packages
            .Include(p => p.Subject)
            .Include(p => p.Teacher).ThenInclude(t => t.User)
            .AsQueryable();

        bool isAdminOrStaff = user != null && user.UserRoles.Any(ur =>
            ur.Role.Type == RoleType.Admin ||
            ur.Role.Type == RoleType.Assistant ||
            ur.Role.Type == RoleType.AssistantReviewer ||
            ur.Role.Type == RoleType.AssistantAcademic ||
            ur.Role.Type == RoleType.Supervisor ||
            ur.Role.Type == RoleType.Staff);

        bool isTeacher = user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher);

        if (isAdminOrStaff)
        {
            // Admins/Staff see ALL packages in the system regardless of IsActive
        }
        else if (isTeacher && user!.TeacherProfile != null)
        {
            // Teachers see their own packages (both active & inactive)
            query = query.Where(p => p.TeacherId == user.TeacherProfile.Id);
        }
        else
        {
            // Students only see active packages
            query = query.Where(p => p.IsActive);
        }

        var packages = await query.ToListAsync(ct);

        var dtos = new List<PackageDto>();
        foreach (var pk in packages)
        {
            var isEnrolled = await _access.HasAccessToPackageAsync(request.UserId, pk.Id, ct);
            dtos.Add(new PackageDto(
                pk.Id, 
                pk.Name, 
                pk.Description, 
                pk.Price, 
                pk.SubjectId, 
                isEnrolled, 
                pk.TeacherId, 
                pk.SubjectId,
                pk.Teacher?.User?.FullName ?? "Unknown",
                pk.Teacher?.ProfileImageUrl,
                pk.Subject?.Name ?? "Unknown",
                pk.Teacher?.Bio,
                pk.Teacher?.Specialization,
                pk.TargetGrade,
                pk.ImageUrl
            ));
        }

        return ApiResponse<List<PackageDto>>.Ok(dtos);
    }
}
