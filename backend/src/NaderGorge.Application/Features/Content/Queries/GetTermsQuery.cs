using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetTermsQuery(Guid PackageId, Guid? UserId = null, bool IncludeSystemContainers = false) : IRequest<ApiResponse<List<TermDto>>>;

public class GetTermsQueryHandler : IRequestHandler<GetTermsQuery, ApiResponse<List<TermDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IAcademicScopeService _academicScope;
    private readonly IContentArchiveAccessService _archiveAccess;

    public GetTermsQueryHandler(IAppDbContext db, IAcademicScopeService academicScope, IContentArchiveAccessService? archiveAccess = null)
    {
        _db = db;
        _academicScope = academicScope;
        _archiveAccess = archiveAccess ?? new NaderGorge.Application.Services.ContentArchiveAccessService(db);
    }

    public async Task<ApiResponse<List<TermDto>>> Handle(GetTermsQuery request, CancellationToken ct)
    {
        var terms = await _db.Terms
            .Where(t => t.PackageId == request.PackageId && (request.IncludeSystemContainers || !t.IsSystemContainer))
            .OrderBy(t => t.Order)
            .Select(t => new { t.Id, t.Title, t.Order, t.Price, t.ImageUrl, t.ArchiveMode, t.ArchivedAt })
            .ToListAsync(ct);

        if (request.UserId.HasValue && !await IsPrivilegedUserAsync(request.UserId.Value, ct))
        {
            var eligibleTerms = new List<Guid>();
            foreach (var term in terms)
            {
                if (await _academicScope.IsOwnerEligibleForStudentAsync(
                        StudentFacingScopeOwnerType.Term,
                        term.Id,
                        request.UserId.Value,
                        ct) && await _archiveAccess.CanViewAsync(
                        request.UserId.Value, ContentArchiveTargetType.Term, term.Id, ct))
                {
                    eligibleTerms.Add(term.Id);
                }
            }

            terms = terms.Where(t => eligibleTerms.Contains(t.Id)).ToList();
        }

        // Determine which terms the student has already purchased
        var purchasedTermIds = new HashSet<Guid>();
        var hasPackageAccess = false;

        if (request.UserId.HasValue)
        {
            var now = DateTime.UtcNow;

            // Check if user has package-level access (covers all terms)
            hasPackageAccess = await _db.StudentAccessGrants
                .AnyAsync(g => g.UserId == request.UserId.Value &&
                               g.IsActive &&
                               g.GrantType == CodeType.Package &&
                               g.PackageId == request.PackageId &&
                               (g.ExpiresAt == null || g.ExpiresAt > now), ct);

            if (!hasPackageAccess)
            {
                // Check term-level grants
                var termIds = terms.Select(t => t.Id).ToList();
                purchasedTermIds = (await _db.StudentAccessGrants
                    .Where(g => g.UserId == request.UserId.Value &&
                                g.IsActive &&
                                g.GrantType == CodeType.Term &&
                                g.TermId != null &&
                                termIds.Contains(g.TermId!.Value) &&
                                (g.ExpiresAt == null || g.ExpiresAt > now))
                    .Select(g => g.TermId!.Value)
                    .ToListAsync(ct))
                    .ToHashSet();
            }
        }

        var result = terms.Select(t => new TermDto(
            t.Id, t.Title, t.Order, t.Price, t.ImageUrl,
            hasPackageAccess || purchasedTermIds.Contains(t.Id), t.ArchiveMode, t.ArchivedAt
        )).ToList();

        return ApiResponse<List<TermDto>>.Ok(result);
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
