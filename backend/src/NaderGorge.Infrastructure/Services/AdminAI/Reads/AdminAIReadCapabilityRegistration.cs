using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public static class AdminAIReadCapabilityRegistration
{
    public static IReadOnlyList<IAdminAIReadCapability> Validate(IEnumerable<IAdminAIReadCapability> adapters, IAdminAICapabilityRegistry catalog, IAdminAISensitiveDataPolicy policy)
    {
        var list = adapters.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        var duplicates = list.GroupBy(x => x.Key, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicates.Length != 0) throw new InvalidOperationException($"Duplicate read adapters: {string.Join(", ", duplicates)}");
        var activeReads = catalog.All.Where(x => x.Kind == "read").Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var registered = list.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var missing = activeReads.Except(registered, StringComparer.Ordinal).ToArray();
        var inactive = registered.Except(activeReads, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0 || inactive.Length != 0) throw new InvalidOperationException($"Read registration mismatch. Missing=[{string.Join(',', missing)}] Inactive=[{string.Join(',', inactive)}]");
        foreach (var adapter in list) policy.AssertSafeSchema(adapter.OutputType);
        return Array.AsReadOnly(list);
    }
}
