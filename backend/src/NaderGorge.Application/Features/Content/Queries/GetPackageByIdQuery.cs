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
    List<TermDto> Terms,
    PackageContentMode ContentMode,
    Guid? RootTermId,
    Guid? RootSectionId,
    List<PackageDirectSectionDto> DirectSections,
    List<PackageDirectLessonDto> DirectLessons,
    ContentArchiveMode ArchiveMode = ContentArchiveMode.None,
    DateTime? ArchivedAt = null);
public record TermDto(Guid Id, string Title, int Order, decimal Price, string? ImageUrl, bool IsPurchased = false, ContentArchiveMode ArchiveMode = ContentArchiveMode.None, DateTime? ArchivedAt = null);
public record PackageDirectSectionDto(Guid Id, string Title, int Order, decimal Price, string? ImageUrl, bool IsPurchased = false, ContentArchiveMode ArchiveMode = ContentArchiveMode.None, DateTime? ArchivedAt = null);
public record PackageDirectLessonDto(Guid Id, string Title, string Summary, int Order, decimal Price, bool HasAccess = false, ContentArchiveMode ArchiveMode = ContentArchiveMode.None, DateTime? ArchivedAt = null);

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

        var visibleTerms = package.Terms
            .Where(t => !t.IsSystemContainer)
            .OrderBy(t => t.Order)
            .ToList();

        var rootTerm = package.Terms.FirstOrDefault(t => t.IsSystemContainer);
        var directSections = rootTerm == null
            ? []
            : await _db.ContentSections
                .Where(section => section.TermId == rootTerm.Id && !section.IsSystemContainer)
                .OrderBy(section => section.Order)
                .Select(section => new PackageDirectSectionDto(section.Id, section.Title, section.Order, section.Price, section.ImageUrl, false, section.ArchiveMode, section.ArchivedAt))
                .ToListAsync(ct);

        var rootSection = rootTerm == null
            ? null
            : await _db.ContentSections
                .Where(section => section.TermId == rootTerm.Id && section.IsSystemContainer)
                .Select(section => new { section.Id })
                .FirstOrDefaultAsync(ct);

        var directLessons = rootSection == null
            ? []
            : await _db.Lessons
                .Where(lesson => lesson.ContentSectionId == rootSection.Id)
                .OrderBy(lesson => lesson.Order)
                .Select(lesson => new PackageDirectLessonDto(lesson.Id, lesson.Title, lesson.Summary, lesson.Order, lesson.Price, false, lesson.ArchiveMode, lesson.ArchivedAt))
                .ToListAsync(ct);

        var dtos = visibleTerms
            .Select(t => new TermDto(t.Id, t.Title, t.Order, t.Price, t.ImageUrl, false, t.ArchiveMode, t.ArchivedAt))
            .ToList();
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
            dtos,
            package.ContentMode,
            rootTerm?.Id,
            rootSection?.Id,
            directSections,
            directLessons,
            package.ArchiveMode,
            package.ArchivedAt);

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
