using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Catalog;

public sealed class AdminAICapabilityRegistry : IAdminAICapabilityRegistry
{
    private const string EmptyObjectInputSchema = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";
    private const string ObjectOutputSchema = "{\"type\":\"object\"}";
    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal) { "read", "action" };
    private static readonly HashSet<string> Risks = new(StringComparer.Ordinal) { "read", "ordinary", "strong" };
    private static readonly HashSet<string> Confirmations = new(StringComparer.Ordinal) { "none", "ordinary", "strong" };
    private readonly IReadOnlyDictionary<string, AdminAICapabilityDefinition> _definitions;

    public AdminAICapabilityRegistry(IEnumerable<AdminAICapabilityDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ordered = definitions.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        Validate(ordered);
        _definitions = new ReadOnlyDictionary<string, AdminAICapabilityDefinition>(
            ordered.ToDictionary(x => x.Key, StringComparer.Ordinal));
        All = Array.AsReadOnly(ordered);
        BaselineHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(ordered, CanonicalOptions))));
    }

    public string BaselineHash { get; }
    public IReadOnlyCollection<AdminAICapabilityDefinition> All { get; }
    public bool TryGet(string key, out AdminAICapabilityDefinition definition) =>
        _definitions.TryGetValue(key, out definition!);

    public static AdminAICapabilityRegistry CreateProductionReadRegistry()
    {
        string[] keys =
        [
            "assessment.summary", "codes.summary", "community.summary", "content.summary",
            "forms-settings.summary", "hr-lifecycle.summary", "hr-operations.summary",
            "hr-people.summary", "identity.users.summary", "legacy-finance.summary",
            "live-support.summary", "operations.summary", "platform-finance.summary",
            "reporting.summary", "sales.summary", "teacher-finance.summary",
            "teacher.summary", "wallet-recharge.summary"
        ];

        return new AdminAICapabilityRegistry(keys.Select(key => new AdminAICapabilityDefinition(
            key,
            "1.0.0",
            "read",
            "read",
            "none",
            EmptyObjectInputSchema,
            ObjectOutputSchema,
            1,
            131_072,
            5_000,
            $"AdminAI.Reads.{key}",
            [])));
    }

    private static JsonSerializerOptions CanonicalOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static void Validate(IReadOnlyCollection<AdminAICapabilityDefinition> definitions)
    {
        var duplicates = definitions.GroupBy(x => x.Key, StringComparer.Ordinal).Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
        if (duplicates.Length > 0)
            throw new InvalidOperationException($"Duplicate Admin AI capability keys: {string.Join(", ", duplicates)}");

        foreach (var item in definitions)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || item.Key.Length > 160 || string.IsNullOrWhiteSpace(item.Version))
                throw new InvalidOperationException("Every capability requires a bounded key and version.");
            if (!Kinds.Contains(item.Kind) || !Risks.Contains(item.Risk) || !Confirmations.Contains(item.Confirmation))
                throw new InvalidOperationException($"Capability '{item.Key}' has an unknown closed-union value.");
            if (item.MaxRows < 0 || item.MaxBytes is <= 0 or > 1_048_576 || item.TimeoutMs is <= 0 or > 30_000)
                throw new InvalidOperationException($"Capability '{item.Key}' has unsafe execution limits.");
            if (string.IsNullOrWhiteSpace(item.InputSchema) || string.IsNullOrWhiteSpace(item.OutputSchema) || string.IsNullOrWhiteSpace(item.AuthoritativeOperation))
                throw new InvalidOperationException($"Capability '{item.Key}' is missing its source contract.");

            var expectedConfirmation = item.Kind == "read" ? "none" : item.Risk == "strong" ? "strong" : "ordinary";
            if (!StringComparer.Ordinal.Equals(item.Confirmation, expectedConfirmation))
                throw new InvalidOperationException($"Capability '{item.Key}' confirmation does not match its kind and risk.");
            if (item.Kind == "read" && item.Risk != "read")
                throw new InvalidOperationException($"Read capability '{item.Key}' must use read risk.");
            if (item.Kind == "action" && item.Risk == "read")
                throw new InvalidOperationException($"Action capability '{item.Key}' cannot use read risk.");
        }
    }
}
