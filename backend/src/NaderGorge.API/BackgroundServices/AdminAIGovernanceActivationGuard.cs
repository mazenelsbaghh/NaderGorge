using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.BackgroundServices;

public static class AdminAIGovernanceActivationGuard
{
    public static async Task RequireReviewedBaselineAsync(
        IAppDbContext db,
        IAdminAICapabilityRegistry registry,
        CancellationToken cancellationToken)
    {
        var capabilities = registry.All.ToArray();
        if (capabilities.Length == 0 || capabilities.All(item => item.Kind != "action"))
            throw new InvalidOperationException("Admin AI requires the complete reviewed action catalog before activation.");

        var active = await db.AdminAICapabilityBaselines.AsNoTracking()
            .Where(item => item.Status == AdminAICapabilityBaselineStatus.Active)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        if (active.Length != 1 || active[0].ApprovedByAdminUserId is null || active[0].ApprovedAt is null)
            throw new InvalidOperationException("Admin AI requires one manually approved active baseline.");

        var baseline = active[0];
        if (baseline.SupportedReadCount != capabilities.Count(item => item.Kind == "read")
            || baseline.SupportedActionCount != capabilities.Count(item => item.Kind == "action")
            || baseline.ManifestHash.Length != 64)
            throw new InvalidOperationException("Admin AI active baseline counts do not match the executable catalog.");
        var computedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(baseline.SafeManifestJson)));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(computedHash),
                Encoding.ASCII.GetBytes(baseline.ManifestHash.ToLowerInvariant())))
            throw new InvalidOperationException("Admin AI active baseline hash does not match its manifest.");

        using var document = JsonDocument.Parse(baseline.SafeManifestJson, new JsonDocumentOptions { MaxDepth = 32 });
        var manifest = document.RootElement;
        RequireUniqueJsonObjectNames(manifest);
        if (manifest.ValueKind != JsonValueKind.Object
            || !manifest.TryGetProperty("activation", out var activation)
            || activation.ValueKind != JsonValueKind.String || activation.GetString() != "ready"
            || !manifest.TryGetProperty("registryHash", out var registryHash)
            || registryHash.ValueKind != JsonValueKind.String
            || !StringComparer.Ordinal.Equals(registryHash.GetString(), registry.BaselineHash))
            throw new InvalidOperationException("Admin AI active baseline does not match the running catalog.");

        RequireSupportedItems(manifest);
        RequireExactCapabilities(manifest, capabilities);
    }

    private static void RequireUniqueJsonObjectNames(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidOperationException("Admin AI active baseline contains duplicate JSON fields.");
                RequireUniqueJsonObjectNames(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                RequireUniqueJsonObjectNames(item);
    }

    private static void RequireSupportedItems(JsonElement manifest)
    {
        if (!manifest.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
            throw new InvalidOperationException("Admin AI active baseline has no reviewed inventory items.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(id.GetString()) || !seen.Add(id.GetString()!)
                || !item.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String
                || status.GetString() != "supported")
                throw new InvalidOperationException("Admin AI active baseline has an unsupported or duplicate inventory item.");
        }
        if (manifest.TryGetProperty("exclusions", out var exclusions))
        {
            if (exclusions.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Admin AI active baseline exclusions are invalid.");
            foreach (var item in exclusions.EnumerateArray())
                if (item.ValueKind != JsonValueKind.Object
                    || item.TryGetProperty("isCurrentAdminBusinessMutation", out var current) && current.ValueKind == JsonValueKind.True
                    || item.TryGetProperty("business", out var business) && business.ValueKind == JsonValueKind.True)
                    throw new InvalidOperationException("Admin AI cannot exclude a current business mutation.");
        }
    }

    private static void RequireExactCapabilities(
        JsonElement manifest,
        IReadOnlyCollection<AdminAICapabilityDefinition> catalog)
    {
        if (!manifest.TryGetProperty("capabilities", out var listed) || listed.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Admin AI active baseline has no exact capability list.");
        var expected = catalog.ToDictionary(item => item.Key, item => item.Version, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in listed.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String
                || !item.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String
                || key.GetString() is not { } name || !seen.Add(name)
                || !expected.TryGetValue(name, out var currentVersion)
                || !StringComparer.Ordinal.Equals(currentVersion, version.GetString()))
                throw new InvalidOperationException("Admin AI active baseline capability coverage differs from the running catalog.");
        }
        if (seen.Count != expected.Count)
            throw new InvalidOperationException("Admin AI active baseline is missing executable capabilities.");
    }
}
