using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Common.HR;

public enum HrAccessScope
{
    Self = 0,
    DirectTeam = 1,
    OrganizationSubtree = 2,
    All = 3
}

public interface IHrAuthorizedRequest
{
    string RequiredPermission { get; }
    HrAccessScope RequiredScope { get; }
    Guid? ResourceEmployeeId => null;
    Guid? ResourceUserId => null;
}

public interface IHrAuthorizationService
{
    Task EnsureAuthorizedAsync(IHrAuthorizedRequest request, CancellationToken ct);
}

public sealed class HrAuthorizationService : IHrAuthorizationService
{
    private readonly IAppDbContext _db;
    private readonly IHrRequestContext _context;

    public HrAuthorizationService(IAppDbContext db, IHrRequestContext context)
    {
        _db = db;
        _context = context;
    }

    public async Task EnsureAuthorizedAsync(IHrAuthorizedRequest request, CancellationToken ct)
    {
        var actorUserId = _context.RequireActorUserId();
        var roles = await _db.UserRoles.AsNoTracking()
            .Where(item => item.UserId == actorUserId)
            .Select(item => new { item.Role.Name, item.Role.PermissionsJson })
            .ToListAsync(ct);
        if (roles.Any(item => item.Name == "Admin")) return;

        var grantedScope = roles.SelectMany(item => ParsePermissions(item.PermissionsJson))
            .Where(item => string.Equals(item.Permission, request.RequiredPermission, StringComparison.OrdinalIgnoreCase))
            .Select(item => (HrAccessScope?)item.Scope)
            .OrderByDescending(item => item)
            .FirstOrDefault();
        if (!grantedScope.HasValue || grantedScope.Value < request.RequiredScope)
            throw new UnauthorizedAccessException($"Missing HR permission: {request.RequiredPermission}");

        if (request.RequiredScope == HrAccessScope.All || grantedScope == HrAccessScope.All) return;
        var actorEmployee = await _db.EmployeeProfiles.AsNoTracking()
            .Where(item => item.UserId == actorUserId)
            .Select(item => new { item.Id, item.UserId })
            .SingleOrDefaultAsync(ct)
            ?? throw new UnauthorizedAccessException("HR scoped permission requires an employee profile.");
        var targetEmployeeId = request.ResourceEmployeeId;
        if (!targetEmployeeId.HasValue && request.ResourceUserId.HasValue)
            targetEmployeeId = await _db.EmployeeProfiles.AsNoTracking()
                .Where(item => item.UserId == request.ResourceUserId.Value)
                .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(ct);
        if (!targetEmployeeId.HasValue)
            throw new UnauthorizedAccessException("HR scoped request requires a target employee.");
        if (targetEmployeeId.Value == actorEmployee.Id) return;
        if (request.RequiredScope == HrAccessScope.Self)
            throw new UnauthorizedAccessException("HR self scope cannot access another employee.");

        var today = CairoTime.GetCurrentDate();
        if (grantedScope >= HrAccessScope.DirectTeam && await _db.EmploymentAssignments.AsNoTracking().AnyAsync(item =>
                item.EmployeeId == targetEmployeeId.Value && item.ManagerEmployeeId == actorEmployee.Id &&
                item.EffectiveFrom <= today && (!item.EffectiveTo.HasValue || item.EffectiveTo > today), ct))
            return;
        if (grantedScope >= HrAccessScope.OrganizationSubtree)
        {
            var actorUnit = await _db.EmploymentAssignments.AsNoTracking()
                .Where(item => item.EmployeeId == actorEmployee.Id && item.EffectiveFrom <= today && (!item.EffectiveTo.HasValue || item.EffectiveTo > today))
                .OrderByDescending(item => item.EffectiveFrom).Select(item => (Guid?)item.OrganizationUnitId).FirstOrDefaultAsync(ct);
            var targetUnit = await _db.EmploymentAssignments.AsNoTracking()
                .Where(item => item.EmployeeId == targetEmployeeId.Value && item.EffectiveFrom <= today && (!item.EffectiveTo.HasValue || item.EffectiveTo > today))
                .OrderByDescending(item => item.EffectiveFrom).Select(item => (Guid?)item.OrganizationUnitId).FirstOrDefaultAsync(ct);
            if (actorUnit.HasValue && targetUnit.HasValue && await IsUnitInSubtreeAsync(actorUnit.Value, targetUnit.Value, ct)) return;
        }
        throw new UnauthorizedAccessException("Employee is outside the actor organization scope.");
    }

    private async Task<bool> IsUnitInSubtreeAsync(Guid rootId, Guid targetId, CancellationToken ct)
    {
        if (rootId == targetId) return true;
        var parents = await _db.OrganizationUnits.AsNoTracking().Select(item => new { item.Id, item.ParentId }).ToListAsync(ct);
        var cursor = parents.FirstOrDefault(item => item.Id == targetId)?.ParentId;
        var visited = new HashSet<Guid>();
        while (cursor.HasValue && visited.Add(cursor.Value))
        {
            if (cursor.Value == rootId) return true;
            cursor = parents.FirstOrDefault(item => item.Id == cursor.Value)?.ParentId;
        }
        return false;
    }

    private static IEnumerable<(string Permission, HrAccessScope Scope)> ParsePermissions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) yield break;
        string[] values;
        try { values = JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { yield break; }
        foreach (var value in values)
        {
            var parts = value.Split('@', 2, StringSplitOptions.TrimEntries);
            var scope = parts.Length == 1 ? HrAccessScope.All : parts[1].ToLowerInvariant() switch
            {
                "self" => HrAccessScope.Self,
                "direct-team" => HrAccessScope.DirectTeam,
                "organization-subtree" => HrAccessScope.OrganizationSubtree,
                "all" => HrAccessScope.All,
                _ => (HrAccessScope)(-1)
            };
            if ((int)scope >= 0) yield return (parts[0], scope);
        }
    }
}

public sealed class HrAuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IHrAuthorizationService _authorization;
    public HrAuthorizationBehavior(IHrAuthorizationService authorization) => _authorization = authorization;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is IHrAuthorizedRequest protectedRequest)
            await _authorization.EnsureAuthorizedAsync(protectedRequest, cancellationToken);
        return await next();
    }
}
