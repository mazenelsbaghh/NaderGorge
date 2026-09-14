using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetSectionsQuery(Guid TermId, Guid? UserId = null) : IRequest<ApiResponse<List<ContentSectionDto>>>;

public record ContentSectionDto(Guid Id, string Title, int Order, decimal Price, string? ImageUrl, bool IsPurchased = false, ContentArchiveMode ArchiveMode = ContentArchiveMode.None, DateTime? ArchivedAt = null);

public class GetSectionsQueryHandler : IRequestHandler<GetSectionsQuery, ApiResponse<List<ContentSectionDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService _academicScope;
    private readonly IContentArchiveAccessService _archiveAccess;

    public GetSectionsQueryHandler(IAppDbContext db, IAcademicScopeService academicScope, IContentArchiveAccessService? archiveAccess = null)
    {
        _db = db;
        _academicScope = academicScope;
        _archiveAccess = archiveAccess ?? new NaderGorge.Application.Services.ContentArchiveAccessService(db);
    }

    public async Task<ApiResponse<List<ContentSectionDto>>> Handle(GetSectionsQuery request, CancellationToken ct)
    {
        var sections = await _db.ContentSections
            .Where(cs => cs.TermId == request.TermId)
            .OrderBy(cs => cs.Order)
            .Select(cs => new { cs.Id, cs.Title, cs.Order, cs.Price, cs.ImageUrl, cs.ArchiveMode, cs.ArchivedAt })
            .ToListAsync(ct);

        if (request.UserId.HasValue && !await IsPrivilegedUserAsync(request.UserId.Value, ct))
        {
            var eligibleSections = new List<Guid>();
            foreach (var section in sections)
            {
                if (await _academicScope.IsOwnerEligibleForStudentAsync(
                        StudentFacingScopeOwnerType.ContentSection,
                        section.Id,
                        request.UserId.Value,
                        ct) && await _archiveAccess.CanViewAsync(
                        request.UserId.Value, ContentArchiveTargetType.Section, section.Id, ct))
                {
                    eligibleSections.Add(section.Id);
                }
            }

            sections = sections.Where(s => eligibleSections.Contains(s.Id)).ToList();
        }

        // Determine which sections the student has already purchased (or has parent-level access)
        var purchasedSectionIds = new HashSet<Guid>();
        var hasParentAccess = false;

        if (request.UserId.HasValue)
        {
            var now = DateTime.UtcNow;

            // Get the term's parent package
            var term = await _db.Terms
                .FirstOrDefaultAsync(t => t.Id == request.TermId, ct);

            if (term != null)
            {
                // Check package-level access
                hasParentAccess = await _db.StudentAccessGrants
                    .AnyAsync(g => g.UserId == request.UserId.Value &&
                                   g.IsActive &&
                                   g.GrantType == CodeType.Package &&
                                   g.PackageId == term.PackageId &&
                                   (g.ExpiresAt == null || g.ExpiresAt > now), ct);

                if (!hasParentAccess)
                {
                    // Check term-level access
                    hasParentAccess = await _db.StudentAccessGrants
                        .AnyAsync(g => g.UserId == request.UserId.Value &&
                                       g.IsActive &&
                                       g.GrantType == CodeType.Term &&
                                       g.TermId == request.TermId &&
                                       (g.ExpiresAt == null || g.ExpiresAt > now), ct);
                }

                if (!hasParentAccess)
                {
                    // Check section-level grants
                    var sectionIds = sections.Select(s => s.Id).ToList();
                    purchasedSectionIds = (await _db.StudentAccessGrants
                        .Where(g => g.UserId == request.UserId.Value &&
                                    g.IsActive &&
                                    g.GrantType == CodeType.Month &&
                                    g.ContentSectionId != null &&
                                    sectionIds.Contains(g.ContentSectionId!.Value) &&
                                    (g.ExpiresAt == null || g.ExpiresAt > now))
                        .Select(g => g.ContentSectionId!.Value)
                        .ToListAsync(ct))
                        .ToHashSet();
                }
            }
        }

        var result = sections.Select(s => new ContentSectionDto(
            s.Id, s.Title, s.Order, s.Price, s.ImageUrl,
            hasParentAccess || purchasedSectionIds.Contains(s.Id), s.ArchiveMode, s.ArchivedAt
        )).ToList();

        return ApiResponse<List<ContentSectionDto>>.Ok(result);
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
