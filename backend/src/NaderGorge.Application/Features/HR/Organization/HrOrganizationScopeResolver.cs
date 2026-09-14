using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Organization;

public sealed class HrOrganizationScopeResolver
{
    private readonly IAppDbContext _db;

    public HrOrganizationScopeResolver(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlySet<Guid>> ResolveUnitScopeAsync(Guid rootUnitId, CancellationToken ct)
    {
        var relations = await _db.OrganizationUnits
            .Where(unit => unit.IsActive)
            .Select(unit => new { unit.Id, unit.ParentId })
            .ToListAsync(ct);
        if (relations.All(unit => unit.Id != rootUnitId))
        {
            return new HashSet<Guid>();
        }

        var result = new HashSet<Guid> { rootUnitId };
        var pending = new Queue<Guid>();
        pending.Enqueue(rootUnitId);
        while (pending.TryDequeue(out var parentId))
        {
            foreach (var childId in relations.Where(unit => unit.ParentId == parentId).Select(unit => unit.Id))
            {
                if (result.Add(childId)) pending.Enqueue(childId);
            }
        }

        return result;
    }

    public async Task<string?> ValidateParentAsync(Guid unitId, Guid? proposedParentId, CancellationToken ct)
    {
        var parents = await _db.OrganizationUnits
            .Select(unit => new { unit.Id, unit.ParentId })
            .ToDictionaryAsync(unit => unit.Id, unit => unit.ParentId, ct);
        return HrOrganizationRules.ValidateParent(unitId, proposedParentId, parents);
    }
}
