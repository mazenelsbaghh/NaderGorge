namespace NaderGorge.Application.Features.HR.Organization;

using NaderGorge.Domain.Enums;

public static class HrOrganizationRules
{
    public static bool CanTransitionContract(EmploymentContractStatus current, EmploymentContractStatus next) =>
        (current, next) switch
        {
            (EmploymentContractStatus.Draft, EmploymentContractStatus.Active) => true,
            (EmploymentContractStatus.Active, EmploymentContractStatus.Renewed or EmploymentContractStatus.Expired or EmploymentContractStatus.Suspended or EmploymentContractStatus.Terminated) => true,
            (EmploymentContractStatus.Suspended, EmploymentContractStatus.Active or EmploymentContractStatus.Terminated) => true,
            (EmploymentContractStatus.Renewed, EmploymentContractStatus.Active or EmploymentContractStatus.Expired or EmploymentContractStatus.Terminated) => true,
            _ => false
        };

    public static string? ValidateManager(Guid employeeId, Guid? managerEmployeeId) =>
        managerEmployeeId == employeeId ? "EMPLOYEE_SELF_MANAGER" : null;

    public static bool PeriodsOverlap(
        DateOnly firstStart,
        DateOnly? firstEnd,
        DateOnly secondStart,
        DateOnly? secondEnd)
    {
        var firstEffectiveEnd = firstEnd ?? DateOnly.MaxValue;
        var secondEffectiveEnd = secondEnd ?? DateOnly.MaxValue;
        return firstStart <= secondEffectiveEnd && secondStart <= firstEffectiveEnd;
    }

    public static string? ValidateParent(
        Guid unitId,
        Guid? proposedParentId,
        IReadOnlyDictionary<Guid, Guid?> currentParents)
    {
        var cursor = proposedParentId;
        var visited = new HashSet<Guid>();
        while (cursor.HasValue)
        {
            if (cursor.Value == unitId || !visited.Add(cursor.Value))
            {
                return "ORGANIZATION_CYCLE";
            }

            cursor = currentParents.TryGetValue(cursor.Value, out var parentId) ? parentId : null;
        }

        return null;
    }
}
