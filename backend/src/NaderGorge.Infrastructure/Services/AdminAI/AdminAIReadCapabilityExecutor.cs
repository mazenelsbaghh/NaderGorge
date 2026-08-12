using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
        var safeInput = _policy.RedactJson(JsonSerializer.Serialize(call.Input));
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
                return JsonSerializer.Deserialize<JsonElement>(replay);
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
            var redacted = _policy.RedactJson(JsonSerializer.Serialize(projection.Data));
            if (Encoding.UTF8.GetByteCount(redacted) > definition.MaxBytes) throw new InvalidOperationException("Redacted read result exceeded its byte budget.");
            var envelopeJson = JsonSerializer.Serialize(new { data = JsonSerializer.Deserialize<JsonElement>(redacted), evidence = new { projection.ResultCount, projection.IsComplete, projection.IsTruncated, projection.DataAsOf, projection.References } });
            var envelope = JsonSerializer.Deserialize<JsonElement>(envelopeJson);
            if (invocation is not null)
            {
                var protectedResult = _protector!.Protect(ReadPurpose(invocation.Id), Encoding.UTF8.GetBytes(envelopeJson));
                invocation.Status = projection.ResultCount == 0 ? AdminAIReadInvocationStatus.Empty : projection.IsTruncated ? AdminAIReadInvocationStatus.Truncated : AdminAIReadInvocationStatus.Succeeded;
                invocation.ResultCount = projection.ResultCount; invocation.IsComplete = projection.IsComplete; invocation.IsTruncated = projection.IsTruncated;
                invocation.DataAsOf = projection.DataAsOf; invocation.SafeEvidenceJson = JsonSerializer.Serialize(new { projection.ResultCount, projection.IsComplete, projection.IsTruncated, projection.DataAsOf, projection.References });
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

    private static void ValidateInputShape(string safeInput, string schemaJson)
    {
        using var input = JsonDocument.Parse(safeInput);
        using var schema = JsonDocument.Parse(schemaJson);
        if (input.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Read arguments must be an object.");
        if (!schema.RootElement.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object) return;
        var allowed = properties.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var property in input.RootElement.EnumerateObject())
            if (!allowed.Contains(property.Name)) throw new InvalidOperationException($"Unknown read argument '{property.Name}'.");
        if (schema.RootElement.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            foreach (var name in required.EnumerateArray().Select(x => x.GetString()).Where(x => x is not null))
                if (!input.RootElement.TryGetProperty(name!, out _)) throw new InvalidOperationException($"Required read argument '{name}' is missing.");
    }
}
