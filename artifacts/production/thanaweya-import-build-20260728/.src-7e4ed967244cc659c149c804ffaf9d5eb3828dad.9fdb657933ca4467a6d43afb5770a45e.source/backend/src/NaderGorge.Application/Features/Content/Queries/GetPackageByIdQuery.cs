using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetPackageByIdQuery(Guid Id, Guid? CurrentUserId = null) : IRequest<ApiResponse<PackageDetailDto>>;

public record PackageDetailDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    Guid ProgramId,
    bool IsActive,
    string? ImageUrl,
    string TargetGrade,
    IReadOnlyList<AcademicScopeSummaryDto> AcademicScopes,
    List<TermDto> Terms);
public record TermDto(Guid Id, string Title, int Order, decimal Price, string? ImageUrl, bool IsPurchased = false);

public class GetPackageByIdQueryHandler : IRequestHandler<GetPackageByIdQuery, ApiResponse<PackageDetailDto>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;
    private readonly IAcademicScopeService _academicScope;

    public GetPackageByIdQueryHandler(IAppDbContext db, TeacherAuthorizationService auth, IAcademicScopeService academicScope)
    {
        _db = db;
        _auth = auth;
        _academicScope = academicScope;
    }

    public async Task<ApiResponse<PackageDetailDto>> Handle(GetPackageByIdQuery request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessPackageAsync(request.CurrentUserId.Value, request.Id, ct);
            if (!canAccess)
            {
                return ApiResponse<PackageDetailDto>.Fail("Unauthorized access to this package.");
            }
        }

        var package = await _db.Packages
            .Include(p => p.Terms)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);

        if (package == null)
            return ApiResponse<PackageDetailDto>.Fail("Package not found");

        if (request.CurrentUserId.HasValue &&
            !await IsPrivilegedUserAsync(request.CurrentUserId.Value, ct) &&
            !await _db.TeacherProfiles.AnyAsync(
                teacher => teacher.Id == package.TeacherId && teacher.IsContentVisibleToStudents,
                ct))
        {
            return ApiResponse<PackageDetailDto>.Fail("Package not found");
        }

        if (request.CurrentUserId.HasValue &&
            !await IsPrivilegedUserAsync(request.CurrentUserId.Value, ct) &&
            !await _academicScope.IsOwnerEligibleForStudentAsync(
                StudentFacingScopeOwnerType.Package,
                package.Id,
                request.CurrentUserId.Value,
                ct))
        {
            return ApiResponse<PackageDetailDto>.Fail(
                "هذا المحتوى غير متاح لنطاقك الدراسي الحالي.",
                ["ACADEMIC_SCOPE_DENIED"]);
        }

        var dtos = package.Terms.OrderBy(t => t.Order).Select(t => new TermDto(t.Id, t.Title, t.Order, t.Price, t.ImageUrl)).ToList();
        var scopes = await _db.StudentFacingAcademicScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == StudentFacingScopeOwnerType.Package && x.OwnerId == package.Id)
            .ToListAsync(ct);

        var packageDto = new PackageDetailDto(
            package.Id,
            package.Name,
            package.Description,
            package.Price,
            package.SubjectId,
            package.IsActive,
            package.ImageUrl,
            package.TargetGrade,
            AcademicScopeService.ToScopeSummaries(scopes),
            dtos);

        return ApiResponse<PackageDetailDto>.Ok(packageDto);
    }

    private async Task<bool> IsPrivilegedUserAsync(Guid userId, CancellationToken ct)
    {
        return await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .AnyAsync(ur =>
                ur.Role.Type == RoleType.Admin ||
                ur.Role.Type == RoleType.Assistant ||
                ur.Role.Type == RoleType.AssistantReviewer ||
                ur.Role.Type == RoleType.AssistantAcademic ||
                ur.Role.Type == RoleType.Supervisor ||
                ur.Role.Type == RoleType.Staff ||
                ur.Role.Type == RoleType.Teacher,
                ct);
    }
}
