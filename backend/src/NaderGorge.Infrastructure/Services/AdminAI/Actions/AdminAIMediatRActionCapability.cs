using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

/// <summary>
/// Typed bridge between an Admin AI capability and an existing authoritative
/// MediatR command. Preview remains a separate read-only application service;
/// this bridge never queries a DbContext or invokes a controller.
/// </summary>
public abstract class AdminAIMediatRActionCapability<TInput, TResponse>(
    IMediator mediator,
    IAdminAIActionPreviewSource previewSource) : IAdminAIActionCapability
    where TInput : class
{
    public abstract string Key { get; }

    public async Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct)
    {
        var typed = Deserialize(input);
        return await previewSource.PreviewAsync(Key, actorId, typed, ct);
    }

    public async Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 200)
            throw new ArgumentException("A bounded authoritative operation id is required.", nameof(operationId));
        var response = await mediator.Send(CreateCommand(Deserialize(input), actorId, operationId), ct);
        return ToOutcome(response);
    }

    protected abstract IRequest<TResponse> CreateCommand(TInput input, Guid actorId, string operationId);
    protected abstract AdminAIActionOutcome ToOutcome(TResponse response);

    private static TInput Deserialize(object input)
    {
        if (input is TInput typed) return typed;
        if (input is JsonElement json)
            return json.Deserialize<TInput>(JsonOptions) ?? throw new ArgumentException("Action input is empty.", nameof(input));
        return JsonSerializer.Deserialize<TInput>(JsonSerializer.Serialize(input), JsonOptions)
            ?? throw new ArgumentException("Action input is empty.", nameof(input));
    }

    private static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
}

public interface IAdminAIActionPreviewSource
{
    Task<AdminAIActionPreview> PreviewAsync<TInput>(string capabilityKey, Guid actorId, TInput input, CancellationToken ct)
        where TInput : class;
}

/// <summary>
/// Typed bridge for authoritative application services that intentionally are
/// not MediatR requests. It preserves the same closed JSON input contract and
/// server-owned preview boundary as command-backed capabilities.
/// </summary>
public abstract class AdminAIServiceActionCapability<TInput>(IAdminAIActionPreviewSource previewSource)
    : IAdminAIActionCapability where TInput : class
{
    public abstract string Key { get; }

    public Task<AdminAIActionPreview> PreviewAsync(Guid actorId, object input, CancellationToken ct)
    {
        var typed = Deserialize(input);
        return previewSource.PreviewAsync(Key, actorId, typed, ct);
    }

    public Task<AdminAIActionOutcome> ExecuteAsync(Guid actorId, object input, string operationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 200)
            throw new ArgumentException("A bounded authoritative operation id is required.", nameof(operationId));
        return ExecuteAuthoritativelyAsync(actorId, Deserialize(input), operationId, ct);
    }

    protected abstract Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(
        Guid actorId, TInput input, string operationId, CancellationToken ct);

    private static TInput Deserialize(object input)
    {
        if (input is TInput typed) return typed;
        if (input is JsonElement json)
            return json.Deserialize<TInput>(JsonOptions) ?? throw new ArgumentException("Action input is empty.", nameof(input));
        return JsonSerializer.Deserialize<TInput>(JsonSerializer.Serialize(input), JsonOptions)
            ?? throw new ArgumentException("Action input is empty.", nameof(input));
    }

    private static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
}

public static class AdminAIActionOutcomeFactory
{
    public static AdminAIActionOutcome Success(object? safeResult, int? affectedCount, IReadOnlyList<string> refreshScopes, Guid? auditLogId = null) =>
        new(AdminAIExecutionStatus.Succeeded, safeResult ?? new { }, affectedCount, refreshScopes, auditLogId);

    public static AdminAIActionOutcome Rejected(object? safeResult, IReadOnlyList<string> refreshScopes) =>
        new(AdminAIExecutionStatus.Rejected, safeResult ?? new { }, 0, refreshScopes);
}
