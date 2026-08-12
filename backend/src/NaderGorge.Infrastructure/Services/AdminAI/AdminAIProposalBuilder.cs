using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAIProposalBuilder : IAdminAIProposalBuilder
{
    private readonly IAppDbContext _db; private readonly IAdminAIAccessGate _access; private readonly IAdminAICapabilityRegistry _catalog;
    private readonly IAdminAIDataProtector _protector; private readonly IAdminAISensitiveDataPolicy _policy; private readonly IAdminAIConfirmationChallengeService _challenges;
    private readonly IReadOnlyDictionary<string, IAdminAIActionCapability> _adapters; private readonly int _ttlSeconds;

    public AdminAIProposalBuilder(IAppDbContext db, IAdminAIAccessGate access, IAdminAICapabilityRegistry catalog, IAdminAIDataProtector protector, IAdminAISensitiveDataPolicy policy, IAdminAIConfirmationChallengeService challenges, IEnumerable<IAdminAIActionCapability> adapters, IConfiguration configuration)
    {
        _db = db; _access = access; _catalog = catalog; _protector = protector; _policy = policy; _challenges = challenges;
        _adapters = adapters.GroupBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Count() == 1 ? x.Single() : throw new InvalidOperationException($"Duplicate action adapter '{x.Key}'."), StringComparer.Ordinal);
        _ttlSeconds = Math.Clamp(configuration.GetValue("AdminAI:ProposalTtlSeconds", 300), 60, 900);
    }

    public async Task<AdminAIProposalDto> BuildAsync(Guid actorId, Guid turnId, string capabilityKey, object input, CancellationToken ct)
    {
        await _access.RequireCurrentAdminAsync(actorId, null, ct);
        if (string.IsNullOrWhiteSpace(capabilityKey) || !_catalog.TryGet(capabilityKey, out var definition) || definition.Kind != "action" || !_adapters.TryGetValue(capabilityKey, out var adapter)) throw new NotSupportedException("Action capability is unavailable.");
        var turn = await _db.AdminAITurns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == turnId && x.ActorAdminUserId == actorId, ct) ?? throw new KeyNotFoundException();
        var normalizedInput = NormalizeAndValidateInput(input, definition);
        var preview = await adapter.PreviewAsync(actorId, normalizedInput, ct);
        ValidatePreview(preview);
        var protectedPayload = _protector.Protect("proposal-payload", Encoding.UTF8.GetBytes(normalizedInput.GetRawText()));
        var risk = definition.Risk == "strong" ? AdminAIRiskCategory.Security : AdminAIRiskCategory.Ordinary;
        var confirmation = risk == AdminAIRiskCategory.Ordinary ? AdminAIConfirmationType.Explicit : AdminAIConfirmationType.TypedStrong;
        var proposal = new AdminAIActionProposal
        {
            ConversationId = turn.ConversationId, TurnId = turn.Id, ActorAdminUserId = actorId, CapabilityBaselineId = turn.CapabilityBaselineId,
            SensitiveDataPolicyVersionId = turn.SensitiveDataPolicyVersionId, CapabilityKey = definition.Key, CapabilityVersion = definition.Version,
            PrimaryRisk = risk, RiskFlagsJson = JsonSerializer.Serialize(new[] { risk.ToString() }), ConfirmationType = confirmation,
            SafeTargetType = preview.TargetType, SafeTargetReference = preview.TargetReference, ProtectedNormalizedPayload = protectedPayload.Ciphertext,
            PayloadHash = protectedPayload.Digest, StateFingerprint = preview.StateFingerprint, SafeCurrentStateJson = SafeJson(preview.Current),
            SafeRequestedStateJson = SafeJson(preview.Requested), SafeEffectJson = SafeJson(preview.Effect), ValidationSummaryJson = SafeJson(preview.Validation),
            ExpiresAt = DateTime.UtcNow.AddSeconds(_ttlSeconds), Status = AdminAIProposalStatus.PendingConfirmation
        };
        _db.AdminAIActionProposals.Add(proposal); await _db.SaveChangesAsync(ct);
        var phrase = confirmation == AdminAIConfirmationType.TypedStrong ? await _challenges.IssueAsync(actorId, proposal.Id, capabilityKey, ct) : null;
        return Dto(proposal, preview, phrase);
    }

    public async Task<IReadOnlyList<AdminAIProposalDto>> BuildManyAsync(Guid actorId, Guid turnId, IReadOnlyList<AdminAIActionSuggestion> suggestions, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        if (suggestions.Count is < 1 or > 5 || suggestions.Select(x => x.ClientActionId).Distinct(StringComparer.Ordinal).Count() != suggestions.Count) throw new ArgumentException("One to five independent uniquely identified actions are required.", nameof(suggestions));
        if (suggestions.Any(x => string.IsNullOrWhiteSpace(x.ClientActionId) || x.ClientActionId.Length > 100)) throw new ArgumentException("Every action requires a bounded client action id.", nameof(suggestions));
        var proposals = new List<AdminAIProposalDto>(suggestions.Count);
        foreach (var suggestion in suggestions) proposals.Add(await BuildAsync(actorId, turnId, suggestion.CapabilityKey, suggestion.Input, ct));
        return proposals;
    }

    private string SafeJson(object value) => _policy.RedactJson(JsonSerializer.Serialize(value));

    private JsonElement NormalizeAndValidateInput(object input, AdminAICapabilityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(input);
        var raw = JsonSerializer.Serialize(input);
        if (Encoding.UTF8.GetByteCount(raw) > definition.MaxBytes) throw new ArgumentException("Action input exceeds its capability limit.", nameof(input));

        var redacted = _policy.RedactJson(raw);
        if (!JsonEquivalent(raw, redacted)) throw new ArgumentException("Action input contains a prohibited field.", nameof(input));

        using var inputDocument = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 16 });
        if (inputDocument.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Action input must be a JSON object.", nameof(input));
        ValidateAgainstClosedObjectSchema(inputDocument.RootElement, definition.InputSchema);
        return JsonSerializer.Deserialize<JsonElement>(Canonicalize(inputDocument.RootElement));
    }

    private static void ValidateAgainstClosedObjectSchema(JsonElement input, string schemaJson)
    {
        using var schema = JsonDocument.Parse(schemaJson, new JsonDocumentOptions { MaxDepth = 16 });
        var root = schema.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Capability input schema is invalid.");
        if (root.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() != "object") throw new InvalidOperationException("Action capability input schema must describe an object.");

        var properties = root.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object
            ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (root.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False)
            foreach (var supplied in input.EnumerateObject())
                if (!properties.ContainsKey(supplied.Name)) throw new ArgumentException($"Unknown action input field '{supplied.Name}'.", nameof(input));
        if (root.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            foreach (var name in required.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!))
                if (!input.TryGetProperty(name, out _)) throw new ArgumentException($"Required action input field '{name}' is missing.", nameof(input));
        foreach (var supplied in input.EnumerateObject())
            if (properties.TryGetValue(supplied.Name, out var propertySchema) && propertySchema.TryGetProperty("type", out var expected) && expected.ValueKind == JsonValueKind.String && !MatchesType(supplied.Value, expected.GetString()!))
                throw new ArgumentException($"Action input field '{supplied.Name}' has the wrong type.", nameof(input));
    }

    private static bool MatchesType(JsonElement value, string expected) => expected switch
    {
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => throw new InvalidOperationException($"Unsupported action schema type '{expected}'.")
    };

    private static string Canonicalize(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => new JsonObject(value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => KeyValuePair.Create<string, JsonNode?>(x.Name, JsonNode.Parse(Canonicalize(x.Value))))).ToJsonString(),
        JsonValueKind.Array => new JsonArray(value.EnumerateArray().Select(x => JsonNode.Parse(Canonicalize(x))).ToArray()).ToJsonString(),
        _ => value.GetRawText()
    };

    private static bool JsonEquivalent(string left, string right) => StringComparer.Ordinal.Equals(Canonicalize(JsonDocument.Parse(left).RootElement), Canonicalize(JsonDocument.Parse(right).RootElement));

    private static void ValidatePreview(AdminAIActionPreview preview)
    {
        if (string.IsNullOrWhiteSpace(preview.TargetType) || preview.TargetType.Length > 100 || string.IsNullOrWhiteSpace(preview.TargetReference) || preview.TargetReference.Length > 200 || string.IsNullOrWhiteSpace(preview.StateFingerprint) || preview.StateFingerprint.Length > 64)
            throw new InvalidOperationException("Authoritative action preview returned an unsafe contract.");
    }
    private static AdminAIProposalDto Dto(AdminAIActionProposal p, AdminAIActionPreview v, string? phrase) => new(p.Id, p.CapabilityKey, p.SafeTargetType, p.SafeTargetReference, p.PrimaryRisk, p.ConfirmationType, v.Current, v.Requested, v.Effect, p.ExpiresAt, p.Status, p.Version, phrase);
}
