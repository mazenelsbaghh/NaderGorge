using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetPackagesQuery(Guid UserId) : IRequest<ApiResponse<List<PackageDto>>>;

public record PackageDto(
    Guid Id, 
    string Name, 
    string Description, 
    decimal Price, 
    Guid ProgramId, 
    bool IsEnrolled, 
    bool HasDirectPackageAccess,
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

        var userRoles = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == request.UserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        bool hasGlobalAccess = userRoles.Contains("Admin") || userRoles.Contains("Teacher");

        var activeGrants = hasGlobalAccess 
            ? new List<StudentAccessGrant>()
            : await _db.StudentAccessGrants
                .Where(g => g.UserId == request.UserId && g.IsActive && (g.ExpiresAt == null || g.ExpiresAt > DateTime.UtcNow))
                .ToListAsync(ct);

        var packageIds = packages.Select(p => p.Id).ToList();

        var packageTerms = await _db.Terms
            .Where(t => packageIds.Contains(t.PackageId))
            .Select(t => new { t.Id, t.PackageId })
            .ToListAsync(ct);

        var packageSections = await _db.ContentSections
            .Where(cs => packageIds.Contains(cs.Term.PackageId))
            .Select(cs => new { cs.Id, cs.Term.PackageId })
            .ToListAsync(ct);

        var packageLessons = await _db.Lessons
            .Where(l => packageIds.Contains(l.ContentSection.Term.PackageId))
            .Select(l => new { l.Id, PackageId = l.ContentSection.Term.PackageId })
            .ToListAsync(ct);

        var dtos = new List<PackageDto>();
        foreach (var pk in packages)
        {
            bool isEnrolled = hasGlobalAccess;
            if (!isEnrolled)
            {
                // 1. Direct package grant
                isEnrolled = activeGrants.Any(g => g.GrantType == CodeType.Package && g.PackageId == pk.Id);

                if (!isEnrolled)
                {
                    // 2. Term grant within this package
                    var termIds = packageTerms.Where(t => t.PackageId == pk.Id).Select(t => t.Id).ToList();
                    isEnrolled = activeGrants.Any(g => g.GrantType == CodeType.Term && g.TermId.HasValue && termIds.Contains(g.TermId.Value));
                }

                if (!isEnrolled)
                {
                    // 3. Section (Month) grant within this package
                    var sectionIds = packageSections.Where(cs => cs.PackageId == pk.Id).Select(cs => cs.Id).ToList();
                    isEnrolled = activeGrants.Any(g => g.GrantType == CodeType.Month && g.ContentSectionId.HasValue && sectionIds.Contains(g.ContentSectionId.Value));
                }

                if (!isEnrolled)
                {
                    // 4. Lesson grant within this package
                    var lessonIds = packageLessons.Where(l => l.PackageId == pk.Id).Select(l => l.Id).ToList();
                    isEnrolled = activeGrants.Any(g => g.GrantType == CodeType.Lesson && g.LessonId.HasValue && lessonIds.Contains(g.LessonId.Value));
                }
            }

            bool hasDirectPackageAccess = hasGlobalAccess || activeGrants.Any(g => g.GrantType == CodeType.Package && g.PackageId == pk.Id);

            dtos.Add(new PackageDto(
                pk.Id, 
                pk.Name, 
                pk.Description, 
                pk.Price, 
                pk.SubjectId, 
                isEnrolled, 
                hasDirectPackageAccess,
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
