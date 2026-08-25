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
    bool HasRootContentAccess,
    Guid TeacherId, 
    Guid SubjectId,
    string TeacherName,
    string? TeacherProfileImageUrl,
    string SubjectName,
    string? TeacherBio,
    string? TeacherSpecialization,
    string TargetGrade,
    string? ImageUrl,
    PackageContentMode ContentMode,
    Guid? RootTermId,
    Guid? RootSectionId,
    IReadOnlyList<PackageDirectSectionDto> DirectSections,
    IReadOnlyList<PackageDirectLessonDto> DirectLessons,
    ContentArchiveMode ArchiveMode = ContentArchiveMode.None,
    DateTime? ArchivedAt = null,
    AiOutputLanguage AiOutputLanguage = AiOutputLanguage.Auto,
    bool AllowFullPackagePurchase = true
);

public class GetPackagesQueryHandler : IRequestHandler<GetPackagesQuery, ApiResponse<List<PackageDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;
    private readonly IAcademicScopeService _academicScope;

    public GetPackagesQueryHandler(IAppDbContext db, IAccessCheckService access, IAcademicScopeService academicScope)
    {
        _db = db;
        _access = access;
        _academicScope = academicScope;
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
        Guid? teacherId = user?.TeacherProfile?.Id;
        if (teacherId == null && isTeacher)
        {
            teacherId = await _db.TeacherStaffMembers
                .Where(member => member.UserId == request.UserId && member.IsActive && member.User.IsActive)
                .Select(member => (Guid?)member.TeacherId)
                .FirstOrDefaultAsync(ct);
        }

        if (isAdminOrStaff)
        {
            // Admins/Staff see ALL packages in the system regardless of IsActive
        }
        else if (isTeacher && teacherId.HasValue)
        {
            // Teachers see their own packages (both active & inactive)
            query = query.Where(p => p.TeacherId == teacherId.Value);
        }
        else if (isTeacher)
        {
            query = query.Where(p => false);
        }
        else
        {
            // Students only see active packages
            query = query.Where(p => p.IsActive && p.Teacher.IsContentVisibleToStudents);
        }

        var packages = await query
            .AsNoTracking()
            .ToListAsync(ct);

        if (!isAdminOrStaff && !isTeacher)
        {
            var eligiblePackageIds = await _academicScope.GetEligiblePackageIdsForStudentAsync(
                packages.Select(package => package.Id).ToList(),
                request.UserId,
                ct);
            packages = packages.Where(package => eligiblePackageIds.Contains(package.Id)).ToList();
        }

        bool hasGlobalAccess = user?.UserRoles.Any(role => role.Role.Name is "Admin" or "Teacher") == true;

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

        var rootTerms = await _db.Terms
            .Where(term => packageIds.Contains(term.PackageId) && term.IsSystemContainer)
            .Select(term => new { term.Id, term.PackageId })
            .ToListAsync(ct);

        var rootTermIds = rootTerms.Select(term => term.Id).ToList();
        var rootSections = await _db.ContentSections
            .Where(section => rootTermIds.Contains(section.TermId) && section.IsSystemContainer)
            .Select(section => new { section.Id, section.TermId })
            .ToListAsync(ct);

        var directSections = await _db.ContentSections
            .Where(section => rootTermIds.Contains(section.TermId) && !section.IsSystemContainer)
            .OrderBy(section => section.Order)
            .Select(section => new
            {
                section.Id,
                section.Title,
                section.Order,
                section.Price,
                section.ImageUrl,
                section.TermId,
                PackageId = section.Term.PackageId,
                section.ArchiveMode,
                section.ArchivedAt
            })
            .ToListAsync(ct);

        var rootSectionIds = rootSections.Select(section => section.Id).ToList();
        var directLessons = await _db.Lessons
            .Where(lesson => rootSectionIds.Contains(lesson.ContentSectionId))
            .OrderBy(lesson => lesson.Order)
            .Select(lesson => new
            {
                lesson.Id,
                lesson.Title,
                lesson.Summary,
                lesson.Order,
                lesson.Price,
                lesson.ContentSectionId,
                PackageId = lesson.ContentSection.Term.PackageId,
                lesson.ArchiveMode,
                lesson.ArchivedAt
            })
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

            if (!hasGlobalAccess && (pk.ArchiveMode == ContentArchiveMode.HiddenFromEveryone ||
                (pk.ArchiveMode == ContentArchiveMode.ActiveSubscribersOnly && !isEnrolled)))
                continue;

            var packageRootTerm = rootTerms.FirstOrDefault(term => term.PackageId == pk.Id);
            var packageRootSection = packageRootTerm == null
                ? null
                : rootSections.FirstOrDefault(section => section.TermId == packageRootTerm.Id);

            var directSectionDtos = directSections
                .Where(section => section.PackageId == pk.Id)
                .Select(section => new
                {
                    Section = section,
                    HasAccess = hasDirectPackageAccess || activeGrants.Any(grant =>
                        (grant.GrantType == CodeType.Month && grant.ContentSectionId == section.Id) ||
                        (grant.GrantType == CodeType.Term && grant.TermId == packageRootTerm?.Id))
                })
                .Where(row => hasGlobalAccess || row.Section.ArchiveMode == ContentArchiveMode.None ||
                    (row.Section.ArchiveMode == ContentArchiveMode.ActiveSubscribersOnly && row.HasAccess))
                .Where(row => hasGlobalAccess || row.Section.ArchiveMode != ContentArchiveMode.HiddenFromEveryone)
                .Select(row => new PackageDirectSectionDto(
                    row.Section.Id, row.Section.Title, row.Section.Order, row.Section.Price, row.Section.ImageUrl,
                    row.HasAccess, row.Section.ArchiveMode, row.Section.ArchivedAt))
                .ToList();

            var directLessonDtos = directLessons
                .Where(lesson => lesson.PackageId == pk.Id)
                .Select(lesson => new
                {
                    Lesson = lesson,
                    HasAccess = hasDirectPackageAccess || activeGrants.Any(grant =>
                        (grant.GrantType == CodeType.Lesson && grant.LessonId == lesson.Id) ||
                        (grant.GrantType == CodeType.Month && grant.ContentSectionId == lesson.ContentSectionId) ||
                        (grant.GrantType == CodeType.Term && grant.TermId == packageRootTerm?.Id))
                })
                .Where(row => hasGlobalAccess || row.Lesson.ArchiveMode == ContentArchiveMode.None ||
                    (row.Lesson.ArchiveMode == ContentArchiveMode.ActiveSubscribersOnly && row.HasAccess))
                .Where(row => hasGlobalAccess || row.Lesson.ArchiveMode != ContentArchiveMode.HiddenFromEveryone)
                .Select(row => new PackageDirectLessonDto(
                    row.Lesson.Id, row.Lesson.Title, row.Lesson.Summary, row.Lesson.Order, row.Lesson.Price,
                    row.HasAccess, row.Lesson.ArchiveMode, row.Lesson.ArchivedAt))
                .ToList();

            var hasRootContentAccess = pk.ContentMode switch
            {
                PackageContentMode.SectionWithLessons => hasGlobalAccess || activeGrants.Any(grant =>
                    grant.GrantType == CodeType.Term && grant.TermId == packageRootTerm?.Id),
                PackageContentMode.LessonsOnly => hasGlobalAccess || activeGrants.Any(grant =>
                    grant.GrantType == CodeType.Month && grant.ContentSectionId == packageRootSection?.Id),
                PackageContentMode.SingleLesson => hasGlobalAccess || directLessonDtos.Any(lesson => lesson.HasAccess),
                _ => hasDirectPackageAccess
            };

            dtos.Add(new PackageDto(
                pk.Id, 
                pk.Name, 
                pk.Description, 
                pk.Price, 
                pk.SubjectId, 
                isEnrolled, 
                hasDirectPackageAccess,
                hasRootContentAccess,
                pk.TeacherId, 
                pk.SubjectId,
                pk.Teacher?.User?.FullName ?? "Unknown",
                pk.Teacher?.ProfileImageUrl,
                pk.Subject?.Name ?? "Unknown",
                pk.Teacher?.Bio,
                pk.Teacher?.Specialization,
                pk.TargetGrade,
                pk.ImageUrl,
                pk.ContentMode,
                packageRootTerm?.Id,
                packageRootSection?.Id,
                directSectionDtos,
                directLessonDtos,
                pk.ArchiveMode,
                pk.ArchivedAt,
                pk.AiOutputLanguage,
                pk.AllowFullPackagePurchase
            ));
        }

        return ApiResponse<List<PackageDto>>.Ok(dtos);
    }
}
