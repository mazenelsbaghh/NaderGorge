using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services.AdminAI.Reads;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAIReadCapabilityExecutor : IAdminAIReadExecutor
{
    private static readonly JsonSerializerOptions InputJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions OutputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IReadOnlyDictionary<string, IAdminAIReadCapability> _adapters;
    private readonly IAdminAICapabilityRegistry _catalog;
    private readonly IAdminAISensitiveDataPolicy _policy;
    private readonly IAdminAIAccessGate _access;
    private readonly IAppDbContext? _db;
    private readonly IAdminAIDataProtector? _protector;

    public AdminAIReadCapabilityExecutor(
        IEnumerable<IAdminAIReadCapability> adapters,
        IAdminAICapabilityRegistry catalog,
        IAdminAISensitiveDataPolicy policy,
        IAdminAIAccessGate access,
        IAppDbContext? db = null,
        IAdminAIDataProtector? protector = null)
    {
        var activeAdapters = AdminAIReadCapabilityRegistration.Validate(adapters, catalog, policy);
        _adapters = activeAdapters.ToDictionary(x => x.Key, StringComparer.Ordinal);
        _catalog = catalog;
        _policy = policy;
        _access = access;
        _db = db;
        _protector = protector;
    }

    public async Task<object> ExecuteAsync(Guid actorId, AdminAIReadCall call, CancellationToken ct)
    {
        await _access.RequireCurrentAdminAsync(actorId, null, ct);
        if (!_catalog.TryGet(call.CapabilityKey, out var definition) || definition.Kind != "read" || definition.Version != call.CapabilityVersion)
            throw new NotSupportedException("Read capability is not active.");
        if (!_adapters.TryGetValue(call.CapabilityKey, out var adapter)) throw new NotSupportedException("Read adapter is unavailable.");
        var safeInput = _policy.RedactJson(JsonSerializer.Serialize(call.Input, InputJsonOptions));
        if (Encoding.UTF8.GetByteCount(safeInput) > 16_384) throw new InvalidOperationException("Read arguments exceeded their byte budget.");
        ValidateInputShape(safeInput, definition.InputSchema);
        var inputHash = _protector?.Digest("admin-ai-read-input", Encoding.UTF8.GetBytes($"{definition.Key}\n{definition.Version}\n{safeInput}"))
            ?? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(safeInput))).ToLowerInvariant();

        var durable = call.TurnId.HasValue && call.TurnStepId.HasValue && call.InvocationSequence.HasValue && _db is not null && _protector is not null;
        if (durable)
        {
            var existing = await _db!.AdminAIReadInvocations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TurnId == call.TurnId && x.InvocationSequence == call.InvocationSequence, ct);
            if (existing is not null)
            {
                if (existing.CapabilityKey != call.CapabilityKey || existing.CapabilityVersion != call.CapabilityVersion || existing.InputHash != inputHash)
                    throw new InvalidOperationException("Read replay identity did not match the durable invocation.");
                if (existing.ProtectedResult is null || existing.ProtectedResultHash is null || existing.ProtectedResultExpiresAt <= DateTime.UtcNow)
                    throw new InvalidOperationException("Durable read result is unavailable for replay.");
                var replay = _protector!.Unprotect(ReadPurpose(existing.Id), new AdminAIProtectedValue(existing.ProtectedResult, existing.ProtectedResultHash));
                return RevalidateReplay(replay, definition);
            }
        }

        var invocation = durable ? new AdminAIReadInvocation
        {
            TurnId = call.TurnId!.Value,
            TurnStepId = call.TurnStepId!.Value,
            InvocationSequence = call.InvocationSequence!.Value,
            CapabilityKey = call.CapabilityKey,
            CapabilityVersion = call.CapabilityVersion,
            SafeInputJson = safeInput,
            InputHash = inputHash,
            SafeScopeJson = safeInput,
            Status = AdminAIReadInvocationStatus.Running,
            DataAsOf = DateTime.UtcNow,
            TraceId = BoundedTrace(call.TraceId)
        } : null;
        if (invocation is not null) { _db!.AdminAIReadInvocations.Add(invocation); await _db.SaveChangesAsync(ct); }

        var timer = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(definition.TimeoutMs, 1, 5_000)));
        try
        {
            var projection = await adapter.ExecuteAsync(actorId, call.Input, timeout.Token);
            if (!adapter.OutputType.IsInstanceOfType(projection.Data)) throw new InvalidOperationException("Read adapter returned an undeclared projection type.");
            if (projection.ResultCount < 0 || projection.ResultCount > definition.MaxRows) throw new InvalidOperationException("Read result exceeded its record budget.");
            var redacted = _policy.RedactJson(JsonSerializer.Serialize(projection.Data, OutputJsonOptions));
            ValidateJsonShape(redacted, definition.OutputSchema, "result");
            if (Encoding.UTF8.GetByteCount(redacted) > definition.MaxBytes) throw new InvalidOperationException("Redacted read result exceeded its byte budget.");
            var envelopeJson = JsonSerializer.Serialize(
                new
                {
                    data = JsonSerializer.Deserialize<JsonElement>(redacted),
                    evidence = new
                    {
                        invocationId = invocation?.Id,
                        projection.ResultCount,
                        projection.IsComplete,
                        projection.IsTruncated,
                        projection.DataAsOf,
                        projection.References
                    }
                },
                InputJsonOptions);
            var envelope = JsonSerializer.Deserialize<JsonElement>(envelopeJson);
            if (invocation is not null)
            {
                var protectedResult = _protector!.Protect(ReadPurpose(invocation.Id), Encoding.UTF8.GetBytes(envelopeJson));
                invocation.Status = projection.ResultCount == 0 ? AdminAIReadInvocationStatus.Empty : projection.IsTruncated ? AdminAIReadInvocationStatus.Truncated : AdminAIReadInvocationStatus.Succeeded;
                invocation.ResultCount = projection.ResultCount; invocation.IsComplete = projection.IsComplete; invocation.IsTruncated = projection.IsTruncated;
                invocation.DataAsOf = projection.DataAsOf; invocation.SafeEvidenceJson = JsonSerializer.Serialize(new { invocationId = invocation.Id, projection.ResultCount, projection.IsComplete, projection.IsTruncated, projection.DataAsOf, projection.References }, InputJsonOptions);
                invocation.ProtectedResult = protectedResult.Ciphertext; invocation.ProtectedResultHash = protectedResult.Digest; invocation.ProtectedResultExpiresAt = DateTime.UtcNow.AddHours(24);
                invocation.LatencyMs = ElapsedMs(timer); invocation.CompletedAt = DateTime.UtcNow;
                await _db!.SaveChangesAsync(ct);
            }
            return envelope;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await RecordFailure(invocation, AdminAIReadInvocationStatus.Failed, "READ_TIMEOUT", timer, CancellationToken.None);
            throw;
        }
        catch (OperationCanceledException)
        {
            await RecordFailure(invocation, AdminAIReadInvocationStatus.Cancelled, "CANCELLED", timer, CancellationToken.None);
            throw;
        }
        catch
        {
            await RecordFailure(invocation, AdminAIReadInvocationStatus.Rejected, "READ_REJECTED", timer, CancellationToken.None);
            throw;
        }
    }

    private async Task RecordFailure(AdminAIReadInvocation? invocation, AdminAIReadInvocationStatus status, string code, Stopwatch timer, CancellationToken ct)
    {
        if (invocation is null || _db is null) return;
        invocation.Status = status; invocation.FailureCode = code; invocation.LatencyMs = ElapsedMs(timer); invocation.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static int ElapsedMs(Stopwatch timer) => (int)Math.Min(int.MaxValue, timer.ElapsedMilliseconds);
    private static string BoundedTrace(string? trace) => string.IsNullOrWhiteSpace(trace) ? Guid.NewGuid().ToString("N") : trace[..Math.Min(trace.Length, 64)];
    private static string ReadPurpose(Guid id) => $"read-result:{id:N}";

    private JsonElement RevalidateReplay(byte[] replay, AdminAICapabilityDefinition definition)
    {
        var safeReplay = _policy.RedactJson(Encoding.UTF8.GetString(replay));
        using var document = JsonDocument.Parse(safeReplay);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("data", out var replayData) ||
            replayData.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("evidence", out var replayEvidence) ||
            replayEvidence.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Durable read result has an invalid envelope.");

        var safeData = replayData.GetRawText();
        ValidateJsonShape(safeData, definition.OutputSchema, "result");
        if (Encoding.UTF8.GetByteCount(safeData) > definition.MaxBytes)
            throw new InvalidOperationException("Durable read result exceeded its byte budget.");
        return JsonSerializer.Deserialize<JsonElement>(safeReplay);
    }

    private static void ValidateInputShape(string safeInput, string schemaJson)
        => ValidateJsonShape(safeInput, schemaJson, "arguments");

    private static void ValidateJsonShape(string json, string schemaJson, string rootPath)
    {
        using var input = JsonDocument.Parse(json);
        using var schema = JsonDocument.Parse(schemaJson);
        ValidateSchemaValue(input.RootElement, schema.RootElement, rootPath, schema.RootElement);
    }

    private static void ValidateSchemaValue(
        JsonElement argumentValue,
        JsonElement schemaRule,
        string path,
        JsonElement rootSchema)
    {
        schemaRule = ResolveSchemaReference(schemaRule, rootSchema);
        if (schemaRule.TryGetProperty("enum", out var allowedValues) &&
            allowedValues.ValueKind == JsonValueKind.Array &&
            !allowedValues.EnumerateArray().Any(allowed => JsonElement.DeepEquals(allowed, argumentValue)))
            throw new InvalidOperationException($"Read argument '{path}' has an unsupported value.");

        var expectedType = schemaRule.TryGetProperty("type", out var typeElement) &&
                           typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString()
            : null;
        switch (expectedType)
        {
            case "object":
                ValidateObject(argumentValue, schemaRule, path, rootSchema);
                break;
            case "array":
                ValidateArray(argumentValue, schemaRule, path, rootSchema);
                break;
            case "string":
                ValidateString(argumentValue, schemaRule, path);
                break;
            case "integer":
                if (argumentValue.ValueKind != JsonValueKind.Number ||
                    !argumentValue.TryGetInt64(out var integer))
                    throw new InvalidOperationException($"Read argument '{path}' must be an integer.");
                ValidateNumericRange(integer, schemaRule, path);
                break;
            case "number":
                if (argumentValue.ValueKind != JsonValueKind.Number ||
                    !argumentValue.TryGetDecimal(out var number))
                    throw new InvalidOperationException($"Read argument '{path}' must be a finite number.");
                ValidateNumericRange(number, schemaRule, path);
                break;
            case "boolean" when argumentValue.ValueKind is not JsonValueKind.True and not JsonValueKind.False:
                throw new InvalidOperationException($"Read argument '{path}' must be a boolean.");
        }
    }

    private static JsonElement ResolveSchemaReference(JsonElement schemaRule, JsonElement rootSchema)
    {
        if (!schemaRule.TryGetProperty("$ref", out var referenceElement))
            return schemaRule;
        if (referenceElement.ValueKind != JsonValueKind.String ||
            referenceElement.GetString() is not { } reference ||
            !reference.StartsWith("#/$defs/", StringComparison.Ordinal) ||
            reference.Length == "#/$defs/".Length ||
            reference["#/$defs/".Length..].Contains('/'))
            throw new InvalidOperationException("Read schema contains an unsupported reference.");

        var definitionName = reference["#/$defs/".Length..];
        if (!rootSchema.TryGetProperty("$defs", out var definitions) ||
            definitions.ValueKind != JsonValueKind.Object ||
            !definitions.TryGetProperty(definitionName, out var resolved) ||
            resolved.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Read schema contains an unresolved reference.");
        return resolved;
    }

    private static void ValidateObject(
        JsonElement argumentObject,
        JsonElement schemaRule,
        string path,
        JsonElement rootSchema)
    {
        if (argumentObject.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Read argument '{path}' must be an object.");

        ValidateRequiredProperties(argumentObject, schemaRule);
        var propertyCount = argumentObject.EnumerateObject().Count();
        if (schemaRule.TryGetProperty("minProperties", out var minimum) &&
            minimum.TryGetInt32(out var minProperties) &&
            propertyCount < minProperties)
            throw new InvalidOperationException($"Read argument '{path}' has too few properties.");
        if (schemaRule.TryGetProperty("maxProperties", out var maximum) &&
            maximum.TryGetInt32(out var maxProperties) &&
            propertyCount > maxProperties)
            throw new InvalidOperationException($"Read argument '{path}' has too many properties.");
        var hasProperties = schemaRule.TryGetProperty("properties", out var properties) &&
                            properties.ValueKind == JsonValueKind.Object;

        foreach (var property in argumentObject.EnumerateObject())
        {
            if (hasProperties && properties.TryGetProperty(property.Name, out var propertySchema))
            {
                ValidateSchemaValue(property.Value, propertySchema, $"{path}.{property.Name}", rootSchema);
                continue;
            }

            if (schemaRule.TryGetProperty("additionalProperties", out var additionalProperties) &&
                additionalProperties.ValueKind == JsonValueKind.False)
                throw new InvalidOperationException($"Unknown read argument '{property.Name}'.");
        }
    }

    private static void ValidateRequiredProperties(JsonElement argumentObject, JsonElement schemaRule)
    {
        if (!schemaRule.TryGetProperty("required", out var requiredProperties) ||
            requiredProperties.ValueKind != JsonValueKind.Array)
            return;

        foreach (var requiredName in requiredProperties
                     .EnumerateArray()
                     .Select(required => required.GetString())
                     .Where(required => required is not null))
            if (!argumentObject.TryGetProperty(requiredName!, out _))
                throw new InvalidOperationException($"Required read argument '{requiredName}' is missing.");
    }

    private static void ValidateArray(
        JsonElement argumentArray,
        JsonElement schemaRule,
        string path,
        JsonElement rootSchema)
    {
        if (argumentArray.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Read argument '{path}' must be an array.");

        var arrayItems = argumentArray.EnumerateArray().ToArray();
        if (schemaRule.TryGetProperty("minItems", out var minimum) &&
            minimum.TryGetInt32(out var minItems) &&
            arrayItems.Length < minItems)
            throw new InvalidOperationException($"Read argument '{path}' has too few items.");
        if (schemaRule.TryGetProperty("maxItems", out var maximum) &&
            maximum.TryGetInt32(out var maxItems) &&
            arrayItems.Length > maxItems)
            throw new InvalidOperationException($"Read argument '{path}' has too many items.");
        if (schemaRule.TryGetProperty("uniqueItems", out var uniqueItems) &&
            uniqueItems.ValueKind == JsonValueKind.True &&
            arrayItems.Select(arrayItem => arrayItem.GetRawText()).Distinct(StringComparer.Ordinal).Count() !=
            arrayItems.Length)
            throw new InvalidOperationException($"Read argument '{path}' contains duplicate items.");
        if (schemaRule.TryGetProperty("items", out var itemSchema) && itemSchema.ValueKind == JsonValueKind.Object)
            for (var index = 0; index < arrayItems.Length; index++)
                ValidateSchemaValue(arrayItems[index], itemSchema, $"{path}[{index}]", rootSchema);
    }

    private static void ValidateString(JsonElement argumentValue, JsonElement schemaRule, string path)
    {
        if (argumentValue.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Read argument '{path}' must be a string.");
        var argumentText = argumentValue.GetString() ?? string.Empty;
        if (schemaRule.TryGetProperty("minLength", out var minimum) &&
            minimum.TryGetInt32(out var minLength) &&
            argumentText.Length < minLength)
            throw new InvalidOperationException($"Read argument '{path}' is too short.");
        if (schemaRule.TryGetProperty("maxLength", out var maximum) &&
            maximum.TryGetInt32(out var maxLength) &&
            argumentText.Length > maxLength)
            throw new InvalidOperationException($"Read argument '{path}' is too long.");
        if (schemaRule.TryGetProperty("format", out var format) && format.GetString() == "uuid" &&
            (!Guid.TryParseExact(argumentText, "D", out var id) || id == Guid.Empty))
            throw new InvalidOperationException($"Read argument '{path}' must be a non-empty UUID.");
    }

    private static void ValidateNumericRange(decimal numericValue, JsonElement schemaRule, string path)
    {
        if (schemaRule.TryGetProperty("minimum", out var minimum) &&
            minimum.TryGetDecimal(out var minValue) &&
            numericValue < minValue)
            throw new InvalidOperationException($"Read argument '{path}' is below its minimum.");
        if (schemaRule.TryGetProperty("maximum", out var maximum) &&
            maximum.TryGetDecimal(out var maxValue) &&
            numericValue > maxValue)
            throw new InvalidOperationException($"Read argument '{path}' is above its maximum.");
    }
}
